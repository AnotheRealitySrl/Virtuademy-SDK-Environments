using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>
    /// The tasks of the world, by their objects: read their state, complete them with a result, report an attempt, fail
    /// or unlock them, and hear about every change. <c>IVirtuademyGameplay.Current.Tasks</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A task is the GameObject that carries the task (the step in the task graph). Anything else logs one warning and
    /// does nothing. A task with a Task Step placeholder raises the same results on its events.
    /// </para>
    /// <para>
    /// Without a procedure, <see cref="CompleteTask(GameObject, TaskResult)"/> completes the task (the flow goes on, as
    /// <see cref="CompleteTask(GameObject)"/>), and <see cref="ReportAttempt"/> only raises
    /// <see cref="TaskResultReported"/>. With a procedure on the task system, results are scored (errors, hints, time)
    /// as the procedure says. A report on a completed or failed task is ignored; on a locked one too, unless the
    /// procedure counts out-of-order actions as errors.
    /// </para>
    /// <para>Main thread only. Events are named delegates (usable from interpreted scripts).</para>
    /// </remarks>
    public interface ITasksApi
    {
        /// <summary>Whether the world has at least one task system awake.</summary>
        bool IsAvailable { get; }

        /// <summary>The state of <paramref name="task"/>. Node: <c>Virtuademy Tasks: Get Task State</c>.</summary>
        ETaskState GetTaskState(GameObject task);

        /// <summary>Completes <paramref name="task"/> when it is to do. Node: <c>Virtuademy Tasks: CompleteTask</c>.</summary>
        void CompleteTask(GameObject task);

        /// <summary>Completes <paramref name="task"/> with <paramref name="result"/>: scored by a procedure, if any.
        /// Node: <c>Virtuademy Tasks: Complete Task With Result</c>.</summary>
        void CompleteTask(GameObject task, TaskResult result);

        /// <summary>Reports an attempt that does not complete the task: a wrong one counts as an error in a procedure.
        /// Node: <c>Virtuademy Tasks: Report Attempt</c>.</summary>
        void ReportAttempt(GameObject task, TaskResult result);

        /// <summary>Fails <paramref name="task"/>, with a reason for the summary. Node: <c>Virtuademy Tasks: Fail Task</c>.</summary>
        void FailTask(GameObject task, string reason);

        /// <summary>Unlocks <paramref name="task"/> (to do). Node: <c>Virtuademy Tasks: Unlock Task</c>.</summary>
        void UnlockTask(GameObject task);

        /// <summary>Shows the hint of the current task of the procedure on <paramref name="procedure"/> (its object, or
        /// the task system's). Node: <c>Virtuademy Procedure: Request Hint</c>.</summary>
        void RequestHint(GameObject procedure);

        /// <summary>Raised for every task whose state changes, in every task system of the world.</summary>
        event TaskStateChangedHandler TaskStateChanged;

        /// <summary>Raised for every result reported (attempts and completions), with the task's object.
        /// Node: <c>Virtuademy Tasks: On Task Attempt</c>.</summary>
        event TaskResultHandler TaskResultReported;
    }
}
