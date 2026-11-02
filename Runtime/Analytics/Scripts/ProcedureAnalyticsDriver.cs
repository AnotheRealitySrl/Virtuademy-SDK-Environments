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
    /// it, logs them), and holds Retry and Exit until the queue is sent, for at most <see cref="FlushSeconds"/>. Timers
    /// are one-shot coroutines; the waits end on the queue's Drained event.
    /// </summary>
    /// <remarks>
    /// ExpComplete is sent by the procedure's own logic only (finish, a fail condition, Finish, Abort, Retry, Exit). A
    /// user who leaves the scene through the application (menu, back to home, a disconnection) sends nothing more and
    /// nobody waits: that run stays incomplete in the analytics (user decision, 2026-10-30). An environment that needs a
    /// completed run calls Finish or Abort from its own logic.
    /// </remarks>
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
            analytics.RunIdReady += OnRunIdReady;
            analytics.Sent += OnSent;
            run.SnapshotApplied += OnSnapshotApplied;
            run.BecameMaster += OnBecameMaster;
            run.RunStarted += OnRunStarted;
            run.StepStarted += OnStepStarted;
            run.ResultScored += OnResultScored;
            run.RunFinished += OnRunFinished;
            run.LeaveGate = WaitThen;
        }

        private void OnDestroy()
        {
            if (analytics != null)
            {
                analytics.RunIdReady -= OnRunIdReady;
                analytics.Sent -= OnSent;
            }
            if (run != null)
            {
                run.SnapshotApplied -= OnSnapshotApplied;
                run.BecameMaster -= OnBecameMaster;
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

        // In a shared procedure the master is the sender of record: it mints the team run and sends ExpStart, the steps
        // and ExpComplete; every participant sends ExpJoin once per run (D.10).
        private bool SendsRun => !run.IsShared || run.IsMaster;

        private void OnRunStarted()
        {
            if (SendsRun)
            {
                analytics.StartRun();
                run.SetRunKey(analytics.RunKey);
            }
        }

        private void OnRunIdReady(string key)
        {
            run.SetRunKey(key);
            if (run.IsShared)
            {
                // The key reaches the room only once its id exists there; then the master joins like everyone.
                run.SetFlag("id." + key);
                analytics.JoinRun(key);
            }
        }

        private void OnSnapshotApplied()
        {
            if (run.RunKey.Length > 0 && run.HasFlag("id." + run.RunKey))
            {
                analytics.JoinRun(run.RunKey);
            }
        }

        // Statements the platform stored, as the room's flags (a new master sends what is not flagged).
        private void OnSent(EAnalyticVerb verb, AnalyticDTO dto)
        {
            if (!run.IsShared || !run.IsMaster)
            {
                return;
            }
            switch (verb)
            {
                case EAnalyticVerb.ExpStart: run.SetFlag("es"); break;
                case EAnalyticVerb.StepStart: run.SetFlag("ss." + ((ExperienceStepDTO)dto).stepId); break;
                case EAnalyticVerb.StepComplete: run.SetFlag("sc." + ((ExperienceStepDTO)dto).stepId); break;
            }
        }

        // The master left during a run: this client takes the run over from the room's flags.
        private void OnBecameMaster()
        {
            if (run.RunKey.Length == 0)
            {
                return;
            }
            analytics.AdoptRun(run.RunKey, run.HasFlag);
            foreach (ProcedureRun.Step step in run.Steps)
            {
                if (!step.Scored || !step.Started)
                {
                    continue;
                }
                if (run.HasFlag("ss." + step.StepId))
                {
                    analytics.StepStartedSent(step.StepId);
                }
                else
                {
                    analytics.StepStarted(step.StepId, step.Name);
                }
                if (step.Score != null && step.Score.Done && !run.HasFlag("sc." + step.StepId))
                {
                    SendStepComplete(step, step.Score.Failed);
                }
            }
            if (run.IsFinished && run.Summary != null)
            {
                OnRunFinished(run.Summary);
            }
        }

        private void OnStepStarted(ProcedureRun.Step step)
        {
            if (step.Scored && SendsRun)
            {
                analytics.StepStarted(step.StepId, step.Name);
            }
        }

        private void OnResultScored(ProcedureRun.Step step, TaskResult result, float lost, ProcedureRun.EResultKind kind)
        {
            if (step?.Score == null || !SendsRun
                || (kind != ProcedureRun.EResultKind.Completion && kind != ProcedureRun.EResultKind.Failure))
            {
                return;
            }
            SendStepComplete(step, kind == ProcedureRun.EResultKind.Failure);
        }

        private void SendStepComplete(ProcedureRun.Step step, bool failed)
        {
            int max = step.Score.MaxPoints;
            int passing = Mathf.RoundToInt(max * run.Procedure.PassPercent / 100f);
            EExperienceOutcome outcome = failed || step.Score.Points < passing ? EExperienceOutcome.Fail : EExperienceOutcome.Pass;
            analytics.StepCompleted(step.StepId, step.Score.Points, max, passing, outcome);
        }

        // Passed: Pass. Failed: Fail. Ended early: Fail if a fail condition happened, else NA with the score so far.
        private void OnRunFinished(ProcedureSummary summary)
        {
            if (!SendsRun)
            {
                return;
            }
            if (run.IsShared)
            {
                // At most once for the room: the flag goes to the room before the statement is sent (a second
                // ExpComplete from another user would add a second row in the Back Office).
                if (run.HasFlag("ec"))
                {
                    return;
                }
                run.SetFlag("ec");
            }
            // A started step without a result when the run ends: 0 against its real maximum and passing score (the
            // analytics' own fallback sends 0/0, which the Back Office shows as a step worth nothing)
            foreach (ProcedureRun.Step step in run.Steps)
            {
                if (step.Scored && step.Started && step.Score != null && !step.Score.Done)
                {
                    int max = step.Score.MaxPoints;
                    analytics.StepCompleted(step.StepId, 0, max, Mathf.RoundToInt(max * run.Procedure.PassPercent / 100f),
                        EExperienceOutcome.Fail);
                }
            }
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
