using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Whose action completes a task element in a multiplayer room. It matters only for an element that a teammate's
    /// action can reach on this client: Item On Target, whose detector sees every player's items and tools. The other
    /// elements see only this client's player (their clicks, their body, their inventory, their quiz, their video), so
    /// both values behave alike there. In a shared procedure the tasks are the room's: a teammate's own action, reported
    /// by their client, completes them for everyone with either value.
    /// </summary>
    public enum EElementCompleter
    {
        /// <summary>Any player: a teammate's item on this client's target counts too (in a shared procedure the master
        /// reports it, once).</summary>
        AnyPlayer = 0,
        /// <summary>Only this client's player: a teammate's item on the target is ignored here; in a shared procedure the
        /// client of the player who acted reports it.</summary>
        LocalPlayerOnly = 1,
    }

    /// <summary>
    /// The common part of the task elements (Target, Choice, Distractor, Reach Zone, Watch, Question, Item On Target):
    /// a ready-made way to detect one kind of action and report its result on a task. The element only detects and
    /// reports; the task's procedure, if any, scores. Without a task an element still raises its events and shows its
    /// cue's right or wrong feedback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The element prefabs (<c>Prefabs/Tasks/Elements</c>) carry the element, an Interactable, a Visual Scripting
    /// Interactable, an interaction cue and its feedback objects: drop the model inside and set the task.
    /// </para>
    /// <para>
    /// <see cref="Report"/>: a Right or Partial result completes the task, a Wrong one is an attempt.
    /// </para>
    /// </remarks>
    public abstract class ElementPlaceholderBase : MonoBehaviour, IInteractionCueTaskSource
    {
        [Header("Task")]
        [SerializeField, Tooltip("The task (the object with the Task) this element reports on. Empty: the element only " +
            "raises its events and shows its cue.")]
        private GameObject task;

        [SerializeField, Tooltip("Report only while the task is to do or in progress; otherwise the action is ignored " +
            "(unless the procedure counts out-of-order actions as errors).")]
        private bool reportOnlyWhenTaskActive = true;

        [SerializeField, Tooltip("In a multiplayer room: whether a teammate's action completes the element on this client " +
            "(Any Player) or only this client's player's (Local Player Only). It changes Item On Target, whose detector " +
            "sees every player's items and tools; the other elements see only this client's player, so both behave alike. " +
            "In a shared procedure a teammate completes the room's task from their own client either way.")]
        private EElementCompleter completedBy = EElementCompleter.AnyPlayer;

        [Header("Events")]
        [SerializeField] private UnityEvent onRight = new();
        [SerializeField] private UnityEvent onWrong = new();
        [SerializeField] private UnityEvent onReported = new();

        public GameObject Task { get => task; set => task = value; }
        public bool ReportOnlyWhenTaskActive { get => reportOnlyWhenTaskActive; set => reportOnlyWhenTaskActive = value; }
        public EElementCompleter CompletedBy { get => completedBy; set => completedBy = value; }
        public UnityEvent OnRight => onRight;
        public UnityEvent OnWrong => onWrong;
        public UnityEvent OnReported => onReported;

        /// <summary>Raised for every result the element reports (or would, without a task), with its task.</summary>
        public event TaskResultHandler Reported;

        /// <summary>Reports a result as this element (custom logic on top of it): Right and Partial complete the task,
        /// Wrong is an attempt.</summary>
        public void Report(TaskResult result) => (Sink ?? throw new InvalidOperationException(
            "[" + GetType().Name + "] " + name + ": no element runtime yet (it is added when the object wakes up)")).Report(result);

        GameObject IInteractionCueTaskSource.CueTask => task;

        #region Runtime hooks (internal)

        internal static event Action<ElementPlaceholderBase> Created;

        internal IElementSink Sink { get; set; }

        internal void RaiseReported(TaskResult result)
        {
            TaskResultHandler handlers = Reported;
            if (handlers != null)
            {
                List<Exception> failures = null;
                foreach (Delegate handler in handlers.GetInvocationList())
                {
                    try
                    {
                        ((TaskResultHandler)handler)(task, result);
                    }
                    catch (Exception e)
                    {
                        (failures ??= new List<Exception>()).Add(e);
                    }
                }
                if (failures != null)
                {
                    if (failures.Count == 1)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                    }
                    throw new AggregateException("[" + GetType().Name + "] " + name + ": " + failures.Count +
                        " Reported handlers failed", failures);
                }
            }
            (result.Outcome == ETaskOutcome.Wrong ? onWrong : onRight).Invoke();
            onReported.Invoke();
        }

        #endregion

        protected virtual void Awake()
        {
            Created?.Invoke(this);
        }
    }

    /// <summary>How an element placeholder reports through the runtime. Internal.</summary>
    internal interface IElementSink
    {
        void Report(TaskResult result);
    }
}
