using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.ScriptingApi;
using Virtuademy.SDK.Environments.UIKit;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The running procedure of a task system with a <see cref="ProcedurePlaceholder"/>: added by the runtime to that
    /// object (never by a creator). Scores the results the task system receives, counts errors, hints and time, applies
    /// the fail conditions, and builds the summary. Driven by the task system's events and one one-shot timer (the time
    /// limit); no per-frame code.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ProcedureRun : MonoBehaviour, IProcedureSink
    {
        /// <summary>One task of the procedure, scored or not.</summary>
        internal sealed class Step
        {
            public Task Task;
            public TaskStepPlaceholder Placeholder;
            public string StepId;
            public bool Scored;
            public int MaxAttempts;
            public ProcedureScoring.TaskScore Score;
            public bool Started;
            public string Name => Task.Node.Description ?? Task.name;
        }

        private const string RightTextKey = "Procedure/Feedback/Right";
        private const string WrongTextKey = "Procedure/Feedback/Wrong";

        private ProcedurePlaceholder procedure;
        private TaskSystemVirtuademy system;
        private readonly List<Step> steps = new();
        private readonly Dictionary<Task, Step> stepsByTask = new();
        private readonly ProcedureScoring scoring = new();
        private readonly List<ProcedureTimelineEntry> timeline = new();

        private bool sceneReady;
        private bool systemReady;
        private bool running;
        private bool finished;
        private float startTime;
        private float finishTime;
        private int errors;
        private int hintsUsed;
        private EProcedureFailReason failCondition = EProcedureFailReason.None;
        private EProcedureOutcome outcome = EProcedureOutcome.Abandoned;
        private ProcedureSummary summary;
        private Coroutine timeLimit;
        private string runKey = "";

        /// <summary>Raised when a run starts (also after a retry in place).</summary>
        internal event Action RunStarted;

        /// <summary>What a scored result was.</summary>
        internal enum EResultKind { Attempt, Completion, Distractor, Failure }

        /// <summary>Raised for every result scored, after the score changed: the step (null for a distractor without a
        /// task), the result (for a failure: a wrong one with the reason), the points it cost, and what it was.</summary>
        internal event Action<Step, TaskResult, float, EResultKind> ResultScored;

        /// <summary>Raised for every hint used, with its step.</summary>
        internal event Action<Step> HintGiven;

        /// <summary>Raised when the run finishes, after the placeholder's events.</summary>
        internal event Action<ProcedureSummary> RunFinished;

        public ProcedurePlaceholder Procedure => procedure;
        internal TaskSystemVirtuademy System => system;
        internal IReadOnlyList<Step> Steps => steps;

        #region IProcedureSink

        public bool IsRunning => running && !finished;
        public bool IsFinished => finished;
        public int Score => scoring.Score;
        public int MaxScore => scoring.MaxScore;
        public int PassingScore => scoring.PassingScore;
        public float Percent => scoring.Percent;
        public int Errors => errors;
        public int HintsUsed => hintsUsed;
        public float ElapsedSeconds => !running ? 0f : (finished ? finishTime : Time.realtimeSinceStartup) - startTime;
        public float RemainingSeconds => procedure.TimeLimitSeconds <= 0f ? -1f
            : Mathf.Max(0f, procedure.TimeLimitSeconds - ElapsedSeconds);
        public EProcedureOutcome Outcome => outcome;
        public string RunKey => runKey;
        public ProcedureSummary Summary => summary;

        #endregion

        internal void Bind(ProcedurePlaceholder placeholder)
        {
            procedure = placeholder;
            procedure.Sink = this;
            if (!TryGetComponent(out system))
            {
                Debug.LogError("[Procedure] " + name + ": the Procedure placeholder must be on the object of a task system " +
                    "(TaskSystemVirtuademy). It does nothing here.", this);
                return;
            }
            system.Procedure = this;
            scoring.MaxScoreSetting = procedure.MaxScoreSetting;
            scoring.PassPercent = procedure.PassPercent;
            scoring.ErrorPenalty = procedure.ErrorPenalty;
            scoring.HintPenalty = procedure.HintPenalty;
            scoring.HintCharge = procedure.HintCharge;
            CollectSteps();
            RefreshStepTexts();
            listeningLanguage = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            system.OnTaskSystemReady.AddListener(OnSystemReady);
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Scene.Ready += OnSceneReady;
            }
            else
            {
                // A creator's play mode: no world runtime to wait for.
                sceneReady = true;
            }
        }

        private bool listeningLanguage;

        private void OnLanguageChanged(string _) => RefreshStepTexts();

        private void OnDestroy()
        {
            if (listeningLanguage)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
            }
            if (system != null)
            {
                system.OnTaskSystemReady.RemoveListener(OnSystemReady);
                if (system.Procedure == this)
                {
                    system.Procedure = null;
                }
            }
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Scene.Ready -= OnSceneReady;
            }
        }

        private void CollectSteps()
        {
            foreach (Task task in GetComponentsInChildren<Task>(true))
            {
                if (task.Node == null)
                {
                    continue;
                }
                task.TryGetComponent(out TaskStepPlaceholder placeholder);
                ETaskScored scored = placeholder != null ? placeholder.Scored : ETaskScored.Auto;
                bool isScored = scored == ETaskScored.Yes
                    || (scored == ETaskScored.Auto && task.Node.Dependencies.Count == 0 && task.Node.ShowInUI);
                Step step = new()
                {
                    Task = task,
                    Placeholder = placeholder,
                    StepId = StepIdOf(task, placeholder),
                    Scored = isScored,
                    MaxAttempts = placeholder != null ? placeholder.MaxAttempts : 0,
                    Score = isScored ? new ProcedureScoring.TaskScore { Weight = placeholder != null ? placeholder.Weight : 1f } : null,
                };
                steps.Add(step);
                stepsByTask[task] = step;
                if (step.Score != null)
                {
                    scoring.Tasks.Add(step.Score);
                }
            }
            scoring.Recompute();
        }

        // Letters, digits, '-' and '_' only (the analytics' step ids).
        private static string StepIdOf(Task task, TaskStepPlaceholder placeholder)
        {
            string raw = placeholder != null && placeholder.StepId.Length > 0 ? placeholder.StepId : task.name;
            char[] chars = raw.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
            return new string(chars);
        }

        #region Start, restart

        private void OnSceneReady()
        {
            sceneReady = true;
            TryStart();
        }

        private void OnSystemReady()
        {
            systemReady = true;
            TryStart();
        }

        private void TryStart()
        {
            if (!sceneReady || !systemReady || (running && !finished))
            {
                return;
            }
            StartRun();
        }

        private void StartRun()
        {
            running = true;
            finished = false;
            outcome = EProcedureOutcome.Abandoned;
            failCondition = EProcedureFailReason.None;
            summary = null;
            errors = 0;
            hintsUsed = 0;
            timeline.Clear();
            scoring.DistractorPenalty = 0f;
            foreach (Step step in steps)
            {
                step.Started = false;
                if (step.Score != null)
                {
                    step.Score.Done = step.Score.Failed = step.Score.Skipped = false;
                    step.Score.ResultScore = 0f;
                    step.Score.WrongAttempts = step.Score.HintRequests = 0;
                }
                // Tasks completed before the run started (a step setter's start step, a restored room) are skipped.
                if (step.Score != null && (step.Task.Node.Status == TaskStatus.Completed || step.Task.Node.Status == TaskStatus.Failed))
                {
                    step.Score.Skipped = true;
                }
            }
            scoring.Recompute();
            startTime = Time.realtimeSinceStartup;
            runKey = "";
            if (timeLimit != null)
            {
                StopCoroutine(timeLimit);
            }
            timeLimit = procedure.TimeLimitSeconds > 0f ? StartCoroutine(TimeLimit(procedure.TimeLimitSeconds)) : null;
            Debug.Log("[Procedure] " + procedure.name + ": run started, " + scoring.Tasks.Count(t => !t.Skipped)
                + " scored steps, max " + scoring.MaxScore + ", passing " + scoring.PassingScore, procedure);
            RunStarted?.Invoke();
            procedure.RaiseScoreChanged();
            foreach (Step step in steps)
            {
                if (step.Task.Node.Status == TaskStatus.Todo || step.Task.Node.Status == TaskStatus.InProgress)
                {
                    step.Started = true;
                }
            }
        }

        /// <summary>The task system was reverted: the run so far is over; a new one starts when it is ready again.</summary>
        internal void OnSystemReverted()
        {
            if (running && !finished)
            {
                Finish(EProcedureOutcome.Abandoned, EProcedureFailReason.None);
            }
            running = false;
            systemReady = false;
        }

        private IEnumerator TimeLimit(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            timeLimit = null;
            if (!finished)
            {
                AddTimeline(null, false, 0f, "Time is up");
                FailCondition(EProcedureFailReason.TimeLimit);
            }
        }

        #endregion

        #region Results (called by TaskReports and the task system)

        /// <summary>Whether reports on this task should be taken while it is locked (they count as wrong attempts).</summary>
        internal bool TakesOutOfOrder => procedure.OutOfOrderIsError && IsRunning;

        /// <summary>Whether a failed task lets the flow go on (On Task Failed: Continue).</summary>
        internal bool FailedTasksAdvance => procedure.OnTaskFailed == ETaskFailedBehaviour.Continue;

        /// <summary>An action on a locked task, with Out Of Order Is Error: <paramref name="wrong"/> is already the wrong
        /// attempt it counts as.</summary>
        internal void OnOutOfOrder(Task task, TaskResult wrong) => OnAttempt(task, wrong);

        /// <summary>An attempt that does not complete the task.</summary>
        internal void OnAttempt(Task task, TaskResult result)
        {
            if (!IsRunning)
            {
                return;
            }
            stepsByTask.TryGetValue(task, out Step step);
            float lost = 0f;
            if (result.Outcome == ETaskOutcome.Wrong)
            {
                errors++;
                if (step?.Score != null)
                {
                    step.Score.WrongAttempts++;
                    // What a wrong attempt costs: a share of the task's points (taken when the task is completed).
                    lost = step.Score.MaxPoints * scoring.ErrorPenalty;
                }
            }
            scoring.Recompute();
            AddTimeline(step, result.Outcome != ETaskOutcome.Wrong, lost, result.Reason, result);
            ResultScored?.Invoke(step, result, lost, EResultKind.Attempt);
            procedure.RaiseScoreChanged();
            if (result.Outcome == ETaskOutcome.Wrong)
            {
                if (step != null && step.MaxAttempts > 0 && step.Score != null && step.Score.WrongAttempts >= step.MaxAttempts)
                {
                    TaskReports.Fail(task, "Too many wrong attempts");
                }
                CheckMaxErrors();
            }
        }

        /// <summary>A distractor was used: errors and points off the total.</summary>
        internal void OnDistractor(Task task, TaskResult result, float penaltyPoints, bool countsAsError)
        {
            if (!IsRunning)
            {
                return;
            }
            Step step = null;
            if (task != null)
            {
                stepsByTask.TryGetValue(task, out step);
            }
            if (countsAsError)
            {
                errors++;
            }
            scoring.DistractorPenalty += Mathf.Max(0f, penaltyPoints);
            scoring.Recompute();
            AddTimeline(step, false, penaltyPoints, result.Reason, result);
            ResultScored?.Invoke(step, result, penaltyPoints, EResultKind.Distractor);
            procedure.RaiseScoreChanged();
            if (countsAsError)
            {
                CheckMaxErrors();
            }
        }

        /// <summary>A task changed status (from the task system's own listener), with the result that completed it if
        /// one was reported, and the reason if it failed.</summary>
        internal void OnTaskStatusChanged(Task task, TaskStatus previous, TaskResult result, string failReason)
        {
            if (!stepsByTask.TryGetValue(task, out Step step))
            {
                return;
            }
            TaskStatus status = task.Node.Status;
            if (status == TaskStatus.Todo || status == TaskStatus.InProgress)
            {
                step.Started = true;
                return;
            }
            if (!IsRunning || step.Score == null || step.Score.Done || step.Score.Skipped)
            {
                return;
            }
            if (status == TaskStatus.Completed)
            {
                if (TaskStepSetter.IsSkipping)
                {
                    step.Score.Skipped = true;
                    scoring.Recompute();
                    procedure.RaiseScoreChanged();
                }
                else
                {
                    // Completed without a reported result (a Complete Task node, a task listener): a right completion.
                    result ??= new TaskResult(ETaskOutcome.Right, 1f, task.gameObject, "", "");
                    step.Score.Done = true;
                    step.Score.ResultScore = result.Score;
                    scoring.Recompute();
                    // What the result itself cost; wrong attempts and hints have their own entries.
                    float lost = Mathf.Round(step.Score.MaxPoints * (1f - result.Score));
                    AddTimeline(step, result.Outcome != ETaskOutcome.Wrong, lost, result.Reason, result);
                    ResultScored?.Invoke(step, result, lost, EResultKind.Completion);
                    procedure.RaiseScoreChanged();
                }
            }
            else if (status == TaskStatus.Failed)
            {
                step.Score.Done = true;
                step.Score.Failed = true;
                scoring.Recompute();
                AddTimeline(step, false, step.Score.MaxPoints, failReason ?? "", null);
                ResultScored?.Invoke(step, new TaskResult(ETaskOutcome.Wrong, 0f, task.gameObject, failReason ?? "", ""),
                    step.Score.MaxPoints, EResultKind.Failure);
                procedure.RaiseScoreChanged();
                if (procedure.OnTaskFailed == ETaskFailedBehaviour.EndProcedure)
                {
                    FailCondition(EProcedureFailReason.TaskFailed);
                }
            }
            CheckAllDone();
        }

        #endregion

        #region Hints

        public void RequestHint()
        {
            Step step = steps.FirstOrDefault(s => s.Scored && IsActive(s) && HintOf(s).Length > 0)
                ?? steps.FirstOrDefault(s => s.Scored && IsActive(s));
            if (step == null)
            {
                Debug.Log("[Procedure] " + procedure.name + ": no step to give a hint for.", procedure);
                return;
            }
            GiveHint(step);
        }

        internal void GiveHint(Task task)
        {
            if (stepsByTask.TryGetValue(task, out Step step))
            {
                GiveHint(step);
            }
        }

        private void GiveHint(Step step)
        {
            if (!procedure.HintsEnabled)
            {
                Debug.Log("[Procedure] " + procedure.name + ": hints are off.", procedure);
                return;
            }
            if (!IsRunning)
            {
                return;
            }
            hintsUsed++;
            float lost = 0f;
            if (step.Score != null)
            {
                int charged = scoring.ChargedHints(step.Score);
                step.Score.HintRequests++;
                if (scoring.ChargedHints(step.Score) > charged)
                {
                    lost = step.Score.MaxPoints * scoring.HintPenalty;
                }
                scoring.Recompute();
            }
            AddTimeline(step, true, lost, "Hint", null);
            step.Placeholder?.RaiseHintShown();
            HintGiven?.Invoke(step);
            procedure.RaiseHintUsed(step.Task.gameObject, hintsUsed);
            procedure.RaiseScoreChanged();
        }

        private static bool IsActive(Step s) => s.Task.Node.Status == TaskStatus.Todo || s.Task.Node.Status == TaskStatus.InProgress;

        #endregion

        #region Texts

        /// <summary>The step's hint: the Localize Task placeholder's key translated, else the node's text, else none.</summary>
        internal static string HintOf(Step step) => TextOf(step.Task, l => l.hintKey, step.Task.Node.Hint, "");

        internal static string RightTextOf(Step step) => TextOf(step.Task, l => l.rightFeedbackKey, step.Task.Node.RightFeedback,
            PanelEnvironment.Text(RightTextKey, "Well done"));

        internal static string WrongTextOf(Step step, string reason) => TextOf(step.Task, l => l.wrongFeedbackKey,
            step.Task.Node.WrongFeedback, reason.Length > 0 ? reason : PanelEnvironment.Text(WrongTextKey, "Not quite: try again"));

        private static string TextOf(Task task, Func<LocalizeTaskPlaceholder, string> key, string nodeText, string generic)
        {
            string fallback = string.IsNullOrEmpty(nodeText) ? generic : nodeText;
            if (task.TryGetComponent(out LocalizeTaskPlaceholder localize))
            {
                string k = key(localize);
                if (!string.IsNullOrEmpty(k))
                {
                    return PanelEnvironment.Text(k, fallback);
                }
            }
            return fallback;
        }

        /// <summary>Fills the Task Step placeholders' read-only texts (when the run starts and when the language changes).</summary>
        internal void RefreshStepTexts()
        {
            foreach (Step step in steps)
            {
                if (step.Placeholder != null)
                {
                    step.Placeholder.HintText = HintOf(step);
                    step.Placeholder.RightFeedbackText = RightTextOf(step);
                    step.Placeholder.WrongFeedbackText = WrongTextOf(step, "");
                }
            }
        }

        #endregion

        #region Fail conditions and finish

        private void CheckMaxErrors()
        {
            if (procedure.MaxErrors > 0 && errors >= procedure.MaxErrors)
            {
                FailCondition(EProcedureFailReason.MaxErrors);
            }
        }

        private void FailCondition(EProcedureFailReason reason)
        {
            if (finished)
            {
                return;
            }
            if (failCondition == EProcedureFailReason.None)
            {
                failCondition = reason;
            }
            if (procedure.OnFailCondition == EFailBehaviour.EndNow)
            {
                Finish(EProcedureOutcome.Failed, failCondition);
            }
        }

        private void CheckAllDone()
        {
            if (!finished && steps.All(s => s.Score == null || s.Score.Done || s.Score.Skipped))
            {
                bool passed = scoring.Passes(failCondition != EProcedureFailReason.None);
                Finish(passed ? EProcedureOutcome.Passed : EProcedureOutcome.Failed, failCondition);
            }
        }

        public void Finish()
        {
            if (!IsRunning)
            {
                return;
            }
            bool passed = scoring.Passes(failCondition != EProcedureFailReason.None);
            Finish(passed ? EProcedureOutcome.Passed : EProcedureOutcome.Failed, failCondition);
        }

        public void Abort()
        {
            if (IsRunning)
            {
                Finish(EProcedureOutcome.Abandoned, EProcedureFailReason.Manual);
            }
        }

        private void Finish(EProcedureOutcome result, EProcedureFailReason reason)
        {
            if (finished)
            {
                return;
            }
            finished = true;
            finishTime = Time.realtimeSinceStartup;
            if (timeLimit != null)
            {
                StopCoroutine(timeLimit);
                timeLimit = null;
            }
            outcome = result;
            summary = BuildSummary(result, reason);
            Debug.Log("[Procedure] " + procedure.name + ": " + result + (reason != EProcedureFailReason.None ? " (" + reason + ")" : "")
                + ", score " + summary.Score + "/" + summary.MaxScore + ", errors " + errors + ", hints " + hintsUsed
                + ", " + summary.ElapsedSeconds.ToString("0.0") + " s", procedure);
            procedure.RaiseFinished(summary);
            RunFinished?.Invoke(summary);
        }

        private ProcedureSummary BuildSummary(EProcedureOutcome result, EProcedureFailReason reason)
        {
            IEnumerable<ProcedureTimelineEntry> entries = procedure.Timeline switch
            {
                ETimelineMode.Off => Enumerable.Empty<ProcedureTimelineEntry>(),
                ETimelineMode.LostPointsOnly => timeline.Where(e => !e.Good || e.LostPoints > 0f),
                _ => timeline,
            };
            return new ProcedureSummary
            {
                Outcome = result,
                FailReason = reason,
                Score = scoring.Score,
                MaxScore = scoring.MaxScore,
                PassingScore = scoring.PassingScore,
                Percent = scoring.Percent,
                ElapsedSeconds = finishTime - startTime,
                Errors = errors,
                HintsUsed = hintsUsed,
                Tasks = steps.Where(s => s.Score != null && !s.Score.Skipped).Select(s => new ProcedureTaskRow
                {
                    Name = s.Name,
                    StepId = s.StepId,
                    Points = s.Score.Points,
                    MaxPoints = s.Score.MaxPoints,
                    State = (ETaskState)s.Task.Node.Status,
                    WrongAttempts = s.Score.WrongAttempts,
                    HintsUsed = s.Score.HintRequests,
                }).ToArray(),
                Timeline = entries.Take(procedure.TimelineMaxRows).ToArray(),
            };
        }

        private void AddTimeline(Step step, bool good, float lostPoints, string reason, TaskResult result = null)
        {
            timeline.Add(new ProcedureTimelineEntry
            {
                Seconds = ElapsedSeconds,
                Step = step?.Name ?? "",
                Actor = result != null && !result.IsLocalActor ? result.ActorName : "",
                Good = good,
                LostPoints = lostPoints,
                Reason = reason ?? "",
            });
        }

        internal static void CopyActor(TaskResult from, TaskResult to)
        {
            to.IsLocalActor = from.IsLocalActor;
            to.ActorName = from.ActorName;
        }

        #endregion

        #region Retry, exit, summary

        public void Retry()
        {
            procedure.RaiseRetry();
            if (procedure.OverrideButtonActions)
            {
                return;
            }
            if (running && !finished)
            {
                Finish(EProcedureOutcome.Abandoned, failCondition);
            }
            if (procedure.RetryMode == ERetryMode.ReloadSession && !system.isNetworked && IVirtuademyFramework.IsAvailable)
            {
                StartCoroutine(IVirtuademyFramework.Current.Session.ReloadSession());
                return;
            }
            // In place: the task system reverts (and a shared one reverts every client), then a new run starts.
            system.Revert();
        }

        public void Exit()
        {
            procedure.RaiseExit();
            if (procedure.OverrideButtonActions)
            {
                return;
            }
            if (running && !finished)
            {
                Finish(EProcedureOutcome.Abandoned, failCondition);
            }
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Scene.ReturnToLobby();
            }
        }

        /// <summary>Raised by <see cref="ShowSummary"/>; the summary panel shows on it (D6).</summary>
        internal event Action SummaryRequested;

        public void ShowSummary()
        {
            if (finished)
            {
                SummaryRequested?.Invoke();
            }
        }

        #endregion
    }
}
