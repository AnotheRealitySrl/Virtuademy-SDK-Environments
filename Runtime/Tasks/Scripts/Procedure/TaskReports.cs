using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// Where every result on a task goes, whoever reports it (<see cref="TasksApi"/>, the Task Step placeholder, the
    /// task elements): checked against the task's status, then raised and applied. Without a procedure a completion
    /// completes the task and an attempt is only raised; a procedure on the task system takes them over.
    /// </summary>
    internal static class TaskReports
    {
        // Tasks already told they ignore reports in their status, so a repeated report logs once.
        private static readonly HashSet<int> ignoredLogged = new();

        public static void Complete(Task task, TaskResult result)
        {
            if (!Accepts(task, "completion"))
            {
                return;
            }
            TasksApi.Instance.RaiseResultReported(task.gameObject, result);
            task.CompleteTask();
        }

        public static void Attempt(Task task, TaskResult result)
        {
            if (!Accepts(task, "attempt"))
            {
                return;
            }
            TasksApi.Instance.RaiseResultReported(task.gameObject, result);
        }

        public static void Fail(Task task, string reason)
        {
            if (!Accepts(task, "failure"))
            {
                return;
            }
            task.FailTask();
        }

        // Completed and failed tasks take no more reports; locked ones neither (a procedure may count them as errors).
        private static bool Accepts(Task task, string what)
        {
            TaskStatus status = task.Node.Status;
            if (status == TaskStatus.Todo || status == TaskStatus.InProgress)
            {
                return true;
            }
            if (ignoredLogged.Add(task.GetInstanceID()))
            {
                Debug.Log("[Tasks] " + task.name + " is " + status + ": the " + what + " reported on it is ignored "
                    + "(logged once per task).", task);
            }
            return false;
        }
    }
}
