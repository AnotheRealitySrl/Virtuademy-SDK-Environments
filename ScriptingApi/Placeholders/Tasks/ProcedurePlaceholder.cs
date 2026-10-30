using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Turns the task system it sits on into a procedure: tasks are scored (weights, wrong attempts, hints), errors and
    /// time are counted, fail conditions end it, and a summary shows at the end. Put it on the object with the task
    /// system (<c>TasksLogic_Procedure</c> has one); a task system without it works as before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The procedure knows tasks and results, not scene objects: elements and scripts report results on tasks
    /// (Task Step placeholder, <see cref="ITasksApi"/>, the task elements), and the procedure scores them. A task's
    /// own settings (weight, whether it is scored, its step id, its maximum attempts) are on its Task Step
    /// placeholder; its texts (description, hint, right and wrong feedback) on the task node and the Localize Task
    /// placeholder.
    /// </para>
    /// <para>
    /// Scoring: each scored task is worth <c>maxScore x weight / total weight</c> points (rounded so that they add up to
    /// <see cref="MaxScore"/>), and earns them times the result's score, minus <c>errorPenalty</c> per wrong attempt and
    /// <c>hintPenalty</c> per charged hint. Distractors take their own points off the total. Passed: the score reaches
    /// <see cref="PassingScore"/> and no fail condition happened.
    /// </para>
    /// <para>
    /// It starts when the task system is ready and the scene is shown, and finishes when every scored task is completed
    /// or failed, on a fail condition that ends it, or on <see cref="Finish"/>. Call members on the main thread.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class ProcedurePlaceholder : MonoBehaviour
    {
        #region Settings

        [Header("Rules")]
        [SerializeField, Tooltip("Guided: the next target is highlighted, right and wrong texts show at once. Assessed: no " +
            "highlight, the texts wait for the summary (marks and sounds still show).")]
        private EProcedureMode mode = EProcedureMode.Guided;
        [SerializeField, Min(1)] private int maxScore = 100;
        [SerializeField, Range(0f, 100f), Tooltip("Percentage of the maximum score needed to pass.")]
        private float passPercent = 60f;
        [SerializeField, Min(0), Tooltip("Wrong attempts that fail the procedure. 0: no limit.")]
        private int maxErrors = 0;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of a task's points lost for each wrong attempt on it.")]
        private float errorPenalty = 0.25f;

        [Header("Hints")]
        [SerializeField] private bool hintsEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of a task's points a charged hint costs.")]
        private float hintPenalty = 0.25f;
        [SerializeField] private EHintCharge hintCharge = EHintCharge.OncePerTask;
        [SerializeField, Tooltip("A hint also highlights the task's targets for a moment (useful in Assessed mode).")]
        private bool hintShowsTarget = true;

        [Header("Time")]
        [SerializeField, Min(0f), Tooltip("Seconds to finish; running out is a fail condition. 0: no limit.")]
        private float timeLimitSeconds = 0f;
        [SerializeField, Tooltip("Show the clock on the task panel when there is a time limit.")]
        private bool showTimer = true;

        [Header("Failure")]
        [SerializeField, Tooltip("What a fail condition (too many errors, time up, a failed task that ends the procedure) does.")]
        private EFailBehaviour onFailCondition = EFailBehaviour.EndNow;
        [SerializeField, Tooltip("Continue: a failed task scores 0 and the flow goes on. End Procedure: a fail condition.")]
        private ETaskFailedBehaviour onTaskFailed = ETaskFailedBehaviour.Continue;
        [SerializeField, Tooltip("An action on a task that is not unlocked yet counts as a wrong attempt, instead of being ignored.")]
        private bool outOfOrderIsError = false;

        [Header("Feedback and summary")]
        [SerializeField] private EFeedbackPresentation feedbackPresentation = EFeedbackPresentation.Toast;
        [SerializeField, Tooltip("Panel presentation: the panel shown for a right action (a Virtuademy Panel, Success variant).")]
        private GameObject successPanel;
        [SerializeField, Tooltip("Panel presentation: the panel shown for a wrong action (a Virtuademy Panel, Error variant).")]
        private GameObject errorPanel;
        [SerializeField] private bool playSounds = true;
        [SerializeField, Tooltip("The sounds of right and wrong actions, steps done and the summary (the interaction cue " +
            "settings). The SDK's asset by default.")]
        private InteractionCueSettings sounds;
        [SerializeField, Tooltip("Feedback objects (mark, flash) moved to a target whose cue has none of its own.")]
        private Transform sharedFeedback;
        [SerializeField, Tooltip("Show the summary at the end (the task panel's own end panel is not shown).")]
        private bool showSummary = true;
        [SerializeField, Tooltip("The summary panel (ProcedureSummary.prefab, in TasksLogic_Procedure).")]
        private GameObject summaryPanel;
        [SerializeField] private ETimelineMode timeline = ETimelineMode.LostPointsOnly;
        [SerializeField, Min(1)] private int timelineMaxRows = 13;
        [SerializeField, Tooltip("A shared procedure always retries in place.")]
        private ERetryMode retryMode = ERetryMode.ReloadSession;
        [SerializeField, Tooltip("Retry and Exit only raise their events; the environment does the rest.")]
        private bool overrideButtonActions = false;

        [Header("Analytics")]
        [SerializeField] private bool sendAnalytics = true;
        [SerializeField, Tooltip("Letters and digits, at most 7. Empty: from the environment's name.")]
        private string analyticsKeyPrefix = "";
        [SerializeField, Tooltip("Context of the experience statements. Empty: the environment's name.")]
        private string analyticsContext = "";

        [Header("Events")]
        [SerializeField] private UnityEvent onAttempt = new();
        [SerializeField] private UnityEvent onHintUsed = new();
        [SerializeField] private UnityEvent onScoreChanged = new();
        [SerializeField] private UnityEvent onPassed = new();
        [SerializeField] private UnityEvent onFailed = new();
        [SerializeField] private UnityEvent onFinished = new();
        [SerializeField] private UnityEvent onRetry = new();
        [SerializeField] private UnityEvent onExit = new();

        public EProcedureMode Mode { get => mode; set => mode = value; }
        public int MaxScoreSetting => maxScore;
        public float PassPercent => passPercent;
        public int MaxErrors => maxErrors;
        public float ErrorPenalty => errorPenalty;
        public bool HintsEnabled { get => hintsEnabled; set => hintsEnabled = value; }
        public float HintPenalty => hintPenalty;
        public EHintCharge HintCharge => hintCharge;
        public bool HintShowsTarget => hintShowsTarget;
        public float TimeLimitSeconds => timeLimitSeconds;
        public bool ShowTimer => showTimer;
        public EFailBehaviour OnFailCondition => onFailCondition;
        public ETaskFailedBehaviour OnTaskFailed => onTaskFailed;
        public bool OutOfOrderIsError => outOfOrderIsError;
        public EFeedbackPresentation FeedbackPresentation => feedbackPresentation;
        public bool PlaySounds => playSounds;
        public GameObject SuccessPanel => successPanel;
        public GameObject ErrorPanel => errorPanel;
        public InteractionCueSettings Sounds => sounds;
        public Transform SharedFeedback => sharedFeedback;
        public bool ShowSummaryAtEnd => showSummary;
        public GameObject SummaryPanel => summaryPanel;
        public ETimelineMode Timeline => timeline;
        public int TimelineMaxRows => timelineMaxRows;
        public ERetryMode RetryMode => retryMode;
        public bool OverrideButtonActions => overrideButtonActions;
        public bool SendAnalytics => sendAnalytics;
        public string AnalyticsKeyPrefix => analyticsKeyPrefix;
        public string AnalyticsContext => analyticsContext;

        public UnityEvent OnAttempt => onAttempt;
        public UnityEvent OnHintUsed => onHintUsed;
        public UnityEvent OnScoreChanged => onScoreChanged;
        public UnityEvent OnPassed => onPassed;
        public UnityEvent OnFailed => onFailed;
        public UnityEvent OnFinished => onFinished;
        public UnityEvent OnRetry => onRetry;
        public UnityEvent OnExit => onExit;

        #endregion

        #region Live state

        public bool IsRunning => Sink?.IsRunning ?? false;
        public bool IsFinished => Sink?.IsFinished ?? false;
        public int Score => Sink?.Score ?? 0;
        /// <summary>The score of the scored tasks (the setting, minus the tasks skipped by starting from a later step).</summary>
        public int MaxScore => Sink?.MaxScore ?? maxScore;
        public int PassingScore => Sink?.PassingScore ?? 0;
        public float Percent => Sink?.Percent ?? 0f;
        public int Errors => Sink?.Errors ?? 0;
        public int HintsUsed => Sink?.HintsUsed ?? 0;
        /// <summary>Seconds since the start (computed when read).</summary>
        public float ElapsedSeconds => Sink?.ElapsedSeconds ?? 0f;
        /// <summary>Seconds left with a time limit; -1 without one.</summary>
        public float RemainingSeconds => Sink?.RemainingSeconds ?? -1f;
        public EProcedureOutcome Outcome => Sink?.Outcome ?? EProcedureOutcome.Abandoned;
        /// <summary>The key of the current run's analytics (its experience id key); empty before it starts.</summary>
        public string RunKey => Sink?.RunKey ?? "";
        /// <summary>The summary, once finished; null before.</summary>
        public ProcedureSummary Summary => Sink?.Summary;

        #endregion

        #region Methods

        /// <summary>Shows the hint of the current task, as the hint settings say.</summary>
        public void RequestHint() => RequireSink().RequestHint();

        /// <summary>Finishes now: passed or failed on the score so far.</summary>
        public void Finish() => RequireSink().Finish();

        /// <summary>Ends now, abandoned.</summary>
        public void Abort() => RequireSink().Abort();

        /// <summary>Starts again (raises <see cref="OnRetry"/> first).</summary>
        public void Retry() => RequireSink().Retry();

        /// <summary>Leaves to the lobby (raises <see cref="OnExit"/> first).</summary>
        public void Exit() => RequireSink().Exit();

        /// <summary>Shows the summary again, once finished.</summary>
        public void ShowSummary() => RequireSink().ShowSummary();

        #endregion

        #region Events

        /// <summary>Every result reported on a task of the procedure.</summary>
        public event TaskResultHandler Attempted;
        public event ProcedureHintHandler HintUsed;
        public event ProcedureEventHandler ScoreChanged;
        public event ProcedureEventHandler Passed;
        public event ProcedureEventHandler Failed;
        public event ProcedureFinishedHandler Finished;

        #endregion

        #region Runtime hooks (internal)

        internal static event Action<ProcedurePlaceholder> Created;

        internal IProcedureSink Sink { get; set; }

        private IProcedureSink RequireSink() => Sink ?? throw new InvalidOperationException(
            "[Procedure] " + name + ": no procedure runtime on this object yet (it is added when the object wakes up)");

        internal void RaiseAttempted(GameObject task, TaskResult result)
        {
            Raise(Attempted, h => ((TaskResultHandler)h)(task, result), nameof(Attempted));
            onAttempt.Invoke();
        }

        internal void RaiseHintUsed(GameObject task, int hintsUsed)
        {
            Raise(HintUsed, h => ((ProcedureHintHandler)h)(task, hintsUsed), nameof(HintUsed));
            onHintUsed.Invoke();
        }

        internal void RaiseScoreChanged()
        {
            Raise(ScoreChanged, h => ((ProcedureEventHandler)h)(gameObject), nameof(ScoreChanged));
            onScoreChanged.Invoke();
        }

        internal void RaiseFinished(ProcedureSummary summary)
        {
            bool passed = summary.Outcome == EProcedureOutcome.Passed;
            Raise(passed ? Passed : Failed, h => ((ProcedureEventHandler)h)(gameObject), passed ? nameof(Passed) : nameof(Failed));
            (passed ? onPassed : onFailed).Invoke();
            Raise(Finished, h => ((ProcedureFinishedHandler)h)(summary), nameof(Finished));
            onFinished.Invoke();
        }

        internal void RaiseRetry() => onRetry.Invoke();

        internal void RaiseExit() => onExit.Invoke();

        // Every handler runs, then a failure surfaces (platform errors fail loudly), as for the Interactable's events.
        private void Raise(Delegate handlers, Action<Delegate> call, string eventName)
        {
            if (handlers == null)
            {
                return;
            }
            List<Exception> failures = null;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    call(handler);
                }
                catch (Exception e)
                {
                    (failures ??= new List<Exception>()).Add(e);
                }
            }
            if (failures == null)
            {
                return;
            }
            if (failures.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }
            throw new AggregateException("[Procedure] " + name + ": " + failures.Count + " " + eventName + " handlers failed",
                failures);
        }

        #endregion

        private void Awake()
        {
            Created?.Invoke(this);
        }

        private void Reset()
        {
#if UNITY_EDITOR
            sounds = UnityEditor.AssetDatabase.LoadAssetAtPath<InteractionCueSettings>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(InteractionCuePlaceholder.DefaultSettingsGuid));
#endif
        }
    }
}
