using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// Where every result on a task goes, whoever reports it (<see cref="TasksApi"/>, the Task Step placeholder, the
    /// task elements): checked against the task's status, raised, and applied. Without a procedure a completion
    /// completes the task and an attempt is only raised; with one, the procedure scores them too. The result that
    /// completes a task is kept until the task system's status listener takes it (<see cref="TakePending"/>), so the
    /// step's Completed event and the procedure see it whatever completed the task.
    /// </summary>
    internal static class TaskReports
    {
        // Tasks already told they ignore reports in their status, so a repeated report logs once.
        private static readonly HashSet<int> ignoredLogged = new();
        private static readonly Dictionary<Task, TaskResult> pending = new();
        private static readonly Dictionary<Task, string> failReasons = new();

        private static ProcedureRun ProcedureOf(Task task)
        {
            TaskSystemVirtuademy system = task.GetComponentInParent<TaskSystemVirtuademy>(true);
            return system != null ? system.Procedure : null;
        }

        public static void Complete(Task task, TaskResult result)
        {
            ProcedureRun procedure = ProcedureOf(task);
            if (OutOfOrder(task, result, procedure) || !Accepts(task, "completion"))
            {
                return;
            }
            Raise(task, result, procedure);
            // A completion with a Wrong outcome still completes the task; a procedure scores it 0.
            pending[task] = result;
            task.CompleteTask();
            // A task that did not complete (CompleteTask completes To Do tasks only) keeps no stale result.
            pending.Remove(task);
        }

        public static void Attempt(Task task, TaskResult result)
        {
            ProcedureRun procedure = ProcedureOf(task);
            if (OutOfOrder(task, result, procedure) || !Accepts(task, "attempt"))
            {
                return;
            }
            Raise(task, result, procedure);
            if (task.TryGetComponent(out TaskStepPlaceholder step))
            {
                step.RaiseAttempted(result);
            }
            procedure?.OnAttempt(task, result);
        }

        public static void Fail(Task task, string reason)
        {
            if (!Accepts(task, "failure"))
            {
                return;
            }
            failReasons[task] = reason ?? "";
            task.FailTask();
            failReasons.Remove(task);
        }

        /// <summary>A distractor used, with or without a task: raised, and charged by the task's procedure.</summary>
        public static void Distractor(Task task, ProcedureRun procedure, TaskResult result, float penaltyPoints, bool countsAsError)
        {
            if (task != null)
            {
                TasksApi.Instance.RaiseResultReported(task.gameObject, result);
            }
            procedure?.OnDistractor(task, result, penaltyPoints, countsAsError);
        }

        public static TaskResult TakePending(Task task)
        {
            if (pending.TryGetValue(task, out TaskResult result))
            {
                pending.Remove(task);
                return result;
            }
            return null;
        }

        public static string TakeFailReason(Task task)
        {
            if (failReasons.TryGetValue(task, out string reason))
            {
                failReasons.Remove(task);
                return reason;
            }
            return null;
        }

        private static void Raise(Task task, TaskResult result, ProcedureRun procedure)
        {
            TasksApi.Instance.RaiseResultReported(task.gameObject, result);
            if (procedure != null && procedure.IsRunning)
            {
                procedure.Procedure.RaiseAttempted(task.gameObject, result);
            }
        }

        // A report on a locked task counts as a wrong attempt when the procedure says so.
        private static bool OutOfOrder(Task task, TaskResult result, ProcedureRun procedure)
        {
            if (task.Node.Status != TaskStatus.Locked || procedure == null || !procedure.TakesOutOfOrder)
            {
                return false;
            }
            TaskResult wrong = new(ETaskOutcome.Wrong, 0f, result.Source,
                result.Reason.Length > 0 ? result.Reason : "Not yet: another step comes first", result.Data);
            ProcedureRun.CopyActor(result, wrong);
            Raise(task, wrong, procedure);
            procedure.OnOutOfOrder(task, wrong);
            return true;
        }

        // Completed and failed tasks take no more reports; locked ones neither (unless out of order counts as an error).
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
