using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// One step of a procedure: put it on a task's object (beside its Task) for its scoring settings, its events and
    /// methods. Optional: without it a task is scored with the defaults (weight 1, scored when it is a leaf task shown in
    /// the task panel).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The step's texts are not here: the description, hint and right/wrong feedback are written on the task node and,
    /// translated, on the Localize Task placeholder. The resolved texts are readable here (<see cref="HintText"/>,
    /// <see cref="RightFeedbackText"/>, <see cref="WrongFeedbackText"/>) once the task system is up.
    /// </para>
    /// <para>
    /// The methods report on this task as <see cref="ITasksApi"/> does; the events are raised for every report on it,
    /// from any source. Without a procedure, <see cref="Complete()"/> completes the task as the Complete Task node does.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class TaskStepPlaceholder : MonoBehaviour
    {
        [Header("Scoring")]
        [SerializeField, Min(0f), Tooltip("Share of the procedure's score, relative to the other scored steps.")]
        private float weight = 1f;
        [SerializeField, Tooltip("Auto: scored when the task is a leaf (no sub-tasks) shown in the task panel.")]
        private ETaskScored scored = ETaskScored.Auto;
        [SerializeField, Tooltip("The step's id in the analytics. Empty: the task object's name.")]
        private string stepId = "";
        [SerializeField, Min(0), Tooltip("Wrong attempts that fail the step. 0: no limit.")]
        private int maxAttempts = 0;

        [Header("Events")]
        [SerializeField] private UnityEvent onUnlocked = new();
        [SerializeField] private UnityEvent onAttempted = new();
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField] private UnityEvent onFailed = new();
        [SerializeField] private UnityEvent onHintShown = new();

        public float Weight { get => weight; set => weight = Mathf.Max(0f, value); }
        public ETaskScored Scored { get => scored; set => scored = value; }
        public string StepId { get => stepId; set => stepId = value ?? ""; }
        public int MaxAttempts { get => maxAttempts; set => maxAttempts = Mathf.Max(0, value); }

        public UnityEvent OnUnlocked => onUnlocked;
        public UnityEvent OnAttempted => onAttempted;
        public UnityEvent OnCompleted => onCompleted;
        public UnityEvent OnFailed => onFailed;
        public UnityEvent OnHintShown => onHintShown;

        /// <summary>The hint, translated (empty: no hint).</summary>
        public string HintText { get; internal set; } = "";
        /// <summary>The text of a right completion, translated.</summary>
        public string RightFeedbackText { get; internal set; } = "";
        /// <summary>The text of a wrong attempt, translated.</summary>
        public string WrongFeedbackText { get; internal set; } = "";

        public event TaskEventHandler Unlocked;
        public event TaskResultHandler Attempted;
        public event TaskResultHandler Completed;
        public event TaskFailedHandler TaskFailed;
        public event TaskEventHandler HintShown;

        /// <summary>Completes the task (Right).</summary>
        public void Complete() => RequireSink().Complete(new TaskResult(ETaskOutcome.Right, 1f, gameObject, "", ""));

        /// <summary>Completes the task with a result.</summary>
        public void Complete(TaskResult result) => RequireSink().Complete(result);

        /// <summary>Reports an attempt that does not complete the task.</summary>
        public void Attempt(TaskResult result) => RequireSink().Attempt(result);

        /// <summary>Fails the task.</summary>
        public void Fail(string reason) => RequireSink().Fail(reason);

        /// <summary>Unlocks the task.</summary>
        public void Unlock() => RequireSink().Unlock();

        /// <summary>Shows the step's hint (counted as the procedure's hint settings say).</summary>
        public void ShowHint() => RequireSink().ShowHint();

        #region Runtime hooks (internal)

        internal static event Action<TaskStepPlaceholder> Created;

        internal ITaskStepSink Sink { get; set; }

        private ITaskStepSink RequireSink() => Sink ?? throw new InvalidOperationException(
            "[TaskStep] " + name + ": no task on this object, or the task system has not woken up yet");

        internal void RaiseUnlocked()
        {
            Raise(Unlocked, h => ((TaskEventHandler)h)(gameObject), nameof(Unlocked));
            onUnlocked.Invoke();
        }

        internal void RaiseAttempted(TaskResult result)
        {
            Raise(Attempted, h => ((TaskResultHandler)h)(gameObject, result), nameof(Attempted));
            onAttempted.Invoke();
        }

        internal void RaiseCompleted(TaskResult result)
        {
            Raise(Completed, h => ((TaskResultHandler)h)(gameObject, result), nameof(Completed));
            onCompleted.Invoke();
        }

        internal void RaiseFailed(string reason)
        {
            Raise(TaskFailed, h => ((TaskFailedHandler)h)(gameObject, reason), nameof(TaskFailed));
            onFailed.Invoke();
        }

        internal void RaiseHintShown()
        {
            Raise(HintShown, h => ((TaskEventHandler)h)(gameObject), nameof(HintShown));
            onHintShown.Invoke();
        }

        // Every handler runs, then a failure surfaces (platform errors fail loudly).
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
            throw new AggregateException("[TaskStep] " + name + ": " + failures.Count + " " + eventName + " handlers failed",
                failures);
        }

        #endregion

        private void Awake()
        {
            Created?.Invoke(this);
        }
    }
}
