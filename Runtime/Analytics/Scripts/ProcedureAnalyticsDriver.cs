using System;
using System.Collections;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.ScriptingApi;
using Virtuademy.SDK.Environments.Tasks;

namespace Virtuademy.SDK.Environments.Analytics
{
    /// <summary>
    /// Gives a procedure with Send Analytics its <see cref="ProcedureAnalytics"/>: added by the runtime beside
    /// <see cref="ProcedureRun"/>. Turns the run's events into statements, sends them through the platform (or, without
    /// it, logs them), and holds Retry, Exit and the scene's unload until the queue is sent, for at most
    /// <see cref="FlushSeconds"/>. Timers are one-shot coroutines; the waits end on the queue's Drained event.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ProcedureAnalyticsDriver : MonoBehaviour, IProcedureScheduler
    {
        public const float FlushSeconds = 8f;

        private ProcedureRun run;
        private ProcedureAnalytics analytics;

        internal ProcedureAnalytics Analytics => analytics;

        internal void Bind(ProcedureRun procedureRun)
        {
            run = procedureRun;
            ProcedurePlaceholderSettings(out string prefix, out string context);
            IProcedureAnalyticsSender sender = IVirtuademyFramework.IsAvailable
                ? new PlatformSender(this)
                : new LogSender(run.Procedure.name);
            analytics = new ProcedureAnalytics(sender, this, prefix, context);
            run.RunStarted += OnRunStarted;
            run.StepStarted += OnStepStarted;
            run.ResultScored += OnResultScored;
            run.RunFinished += OnRunFinished;
            run.LeaveGate = WaitThen;
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Scene.RunBeforeUnload(FlushBeforeUnload);
            }
        }

        private void OnDestroy()
        {
            if (run != null)
            {
                run.RunStarted -= OnRunStarted;
                run.StepStarted -= OnStepStarted;
                run.ResultScored -= OnResultScored;
                run.RunFinished -= OnRunFinished;
                run.LeaveGate = null;
            }
        }

        private void ProcedurePlaceholderSettings(out string prefix, out string context)
        {
            string environment = gameObject.scene.name;
            prefix = run.Procedure.AnalyticsKeyPrefix.Length > 0 ? run.Procedure.AnalyticsKeyPrefix : environment;
            context = run.Procedure.AnalyticsContext.Length > 0 ? run.Procedure.AnalyticsContext : environment;
        }

        private void OnRunStarted()
        {
            analytics.StartRun();
            run.SetRunKey(analytics.RunKey);
        }

        private void OnStepStarted(ProcedureRun.Step step)
        {
            if (step.Scored)
            {
                analytics.StepStarted(step.StepId, step.Name);
            }
        }

        private void OnResultScored(ProcedureRun.Step step, TaskResult result, float lost, ProcedureRun.EResultKind kind)
        {
            if (step?.Score == null || (kind != ProcedureRun.EResultKind.Completion && kind != ProcedureRun.EResultKind.Failure))
            {
                return;
            }
            int max = step.Score.MaxPoints;
            int passing = Mathf.RoundToInt(max * run.Procedure.PassPercent / 100f);
            EExperienceOutcome outcome = kind == ProcedureRun.EResultKind.Failure || step.Score.Points < passing
                ? EExperienceOutcome.Fail : EExperienceOutcome.Pass;
            analytics.StepCompleted(step.StepId, step.Score.Points, max, passing, outcome);
        }

        // Passed: Pass. Failed: Fail. Ended early: Fail if a fail condition happened, else NA with the score so far.
        private void OnRunFinished(ProcedureSummary summary)
        {
            EExperienceOutcome outcome = summary.Outcome switch
            {
                EProcedureOutcome.Passed => EExperienceOutcome.Pass,
                EProcedureOutcome.Failed => EExperienceOutcome.Fail,
                _ => summary.FailReason != EProcedureFailReason.None && summary.FailReason != EProcedureFailReason.Manual
                    ? EExperienceOutcome.Fail : EExperienceOutcome.NA,
            };
            analytics.FinishRun(summary.Score, summary.MaxScore, summary.PassingScore, outcome);
        }

        /// <summary>Runs <paramref name="then"/> once the queue is sent, or after <see cref="FlushSeconds"/>.</summary>
        private void WaitThen(Action then)
        {
            if (analytics.IsIdle)
            {
                then();
                return;
            }
            bool done = false;
            object timeout = null;
            void Finish()
            {
                if (done)
                {
                    return;
                }
                done = true;
                analytics.Drained -= Finish;
                Cancel(timeout);
                run.SummaryPanelView?.SetWaiting(false);
                then();
            }
            run.SummaryPanelView?.SetWaiting(true);
            analytics.Drained += Finish;
            timeout = After(FlushSeconds, Finish);
        }

        // The platform waits for this before tearing the scene down: a run not finished ends (abandoned), then, if
        // statements are still queued, the queue gets FlushSeconds (a one-shot wait: a coroutine cannot be ended early
        // by an event, and stopping one does not release the coroutine waiting on it).
        private IEnumerator FlushBeforeUnload()
        {
            if (run != null && run.IsRunning)
            {
                run.EndForUnload();
            }
            if (!analytics.IsIdle)
            {
                yield return new WaitForSecondsRealtime(FlushSeconds);
            }
        }

        #region IProcedureScheduler

        public object After(float seconds, Action action) => StartCoroutine(AfterRoutine(seconds, action));

        public void Cancel(object handle)
        {
            if (handle is Coroutine c)
            {
                StopCoroutine(c);
            }
        }

        private static IEnumerator AfterRoutine(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        #endregion

        /// <summary>The platform's analytics.</summary>
        private sealed class PlatformSender : IProcedureAnalyticsSender
        {
            private readonly MonoBehaviour host;

            public PlatformSender(MonoBehaviour host)
            {
                this.host = host;
            }

            public void GenerateId(string key, Action onReady) =>
                host.StartCoroutine(IVirtuademyFramework.Current.Analytics.GenerateExperienceGuid(key, onReady));

            public void Send(EAnalyticVerb verb, AnalyticDTO dto, AnalyticSentHandler onSent) =>
                IVirtuademyFramework.Current.Analytics.Send(verb, dto, onSent);
        }

        /// <summary>No platform (a creator's play mode): the statements are logged and count as sent.</summary>
        private sealed class LogSender : IProcedureAnalyticsSender
        {
            private readonly string owner;

            public LogSender(string owner)
            {
                this.owner = owner;
            }

            public void GenerateId(string key, Action onReady) => onReady();

            public void Send(EAnalyticVerb verb, AnalyticDTO dto, AnalyticSentHandler onSent)
            {
                Debug.Log("[Procedure analytics] " + owner + ": " + verb + " " + JsonUtility.ToJson(dto));
                onSent(true, "");
            }
        }
    }
}
