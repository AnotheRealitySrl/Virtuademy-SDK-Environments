using System;
using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The SDK's implementation of <see cref="ITasksApi"/>: the application's scripting backend returns
    /// <see cref="Instance"/>, and the task nodes call it directly (so they work in a creator's play mode too).
    /// </summary>
    public sealed class TasksApi : ITasksApi
    {
        public static TasksApi Instance { get; } = new();

        private TasksApi()
        {
        }

        // Task systems awake in the world (IsAvailable).
        private int awakeSystems;

        // Objects already warned about, so a wrong object logs once and not on every call.
        private readonly HashSet<int> warned = new();

        public bool IsAvailable => awakeSystems > 0;

        public event TaskStateChangedHandler TaskStateChanged;

        public event TaskResultHandler TaskResultReported;

        public ETaskState GetTaskState(GameObject task)
        {
            return Resolve(task, nameof(GetTaskState), out Task t) ? (ETaskState)t.Node.Status : ETaskState.Locked;
        }

        public void CompleteTask(GameObject task)
        {
            if (Resolve(task, nameof(CompleteTask), out Task t))
            {
                t.CompleteTask();
            }
        }

        public void CompleteTask(GameObject task, TaskResult result)
        {
            if (Resolve(task, nameof(CompleteTask), out Task t))
            {
                TaskReports.Complete(t, result ?? new TaskResult(ETaskOutcome.Right));
            }
        }

        public void ReportAttempt(GameObject task, TaskResult result)
        {
            if (Resolve(task, nameof(ReportAttempt), out Task t))
            {
                TaskReports.Attempt(t, result ?? new TaskResult(ETaskOutcome.Wrong));
            }
        }

        public void FailTask(GameObject task, string reason)
        {
            if (Resolve(task, nameof(FailTask), out Task t))
            {
                TaskReports.Fail(t, reason ?? "");
            }
        }

        public void UnlockTask(GameObject task)
        {
            if (Resolve(task, nameof(UnlockTask), out Task t))
            {
                t.UnlockTask();
            }
        }

        private bool Resolve(GameObject task, string member, out Task resolved)
        {
            resolved = null;
            if (task != null && task.TryGetComponent(out resolved) && resolved.Node != null)
            {
                return true;
            }
            int id = task == null ? 0 : task.GetInstanceID();
            if (warned.Add(id))
            {
                Debug.LogWarning("[Tasks] " + member + ": " + (task == null ? "no object" : task.name + " has no Task")
                    + ": the call does nothing. Pass the object of a task (a step of the task graph).", task);
            }
            return false;
        }

        internal void SystemAwake() => awakeSystems++;

        internal void SystemDestroyed() => awakeSystems = Math.Max(0, awakeSystems - 1);

        internal void RaiseStateChanged(GameObject task, ETaskState previous, ETaskState current)
        {
            TaskStateChangedHandler handlers = TaskStateChanged;
            if (handlers == null)
            {
                return;
            }
            List<Exception> failures = null;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((TaskStateChangedHandler)handler)(task, previous, current);
                }
                catch (Exception e)
                {
                    (failures ??= new List<Exception>()).Add(e);
                }
            }
            Rethrow(failures, nameof(TaskStateChanged));
        }

        internal void RaiseResultReported(GameObject task, TaskResult result)
        {
            TaskResultHandler handlers = TaskResultReported;
            if (handlers == null)
            {
                return;
            }
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
            Rethrow(failures, nameof(TaskResultReported));
        }

        // Every handler runs, then a failure surfaces (platform errors fail loudly), as for the Interactable's events.
        internal static void Rethrow(List<Exception> failures, string eventName)
        {
            if (failures == null)
            {
                return;
            }
            if (failures.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }
            throw new AggregateException("[Tasks] " + failures.Count + " " + eventName + " handlers failed", failures);
        }
    }
}
