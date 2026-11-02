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

        /// <summary>Raised for every report made on this client (not for one received from another client), once it is
        /// applied: a shared task system sends it to the others.</summary>
        internal static event System.Action<Task, ProcedureNetworkEvent> LocalReported;

        private static ProcedureNetworkEvent EventOf(int kind, TaskResult result) => new()
        {
            Kind = kind,
            Outcome = result != null ? (int)result.Outcome : 0,
            Score = result != null ? result.Score : 0f,
            Reason = result != null ? result.Reason : "",
            CountsAsError = result != null && result.CountsAsError,
        };

        private static ProcedureRun ProcedureOf(Task task)
        {
            TaskSystemVirtuademy system = task.GetComponentInParent<TaskSystemVirtuademy>(true);
            return system != null ? system.Procedure : null;
        }

        public static void Complete(Task task, TaskResult result) => Complete(task, result, false);

        /// <summary>A completion; <paramref name="remote"/>: received from another client, applied here and not sent on.</summary>
        public static void Complete(Task task, TaskResult result, bool remote)
        {
            ProcedureRun procedure = ProcedureOf(task);
            if ((!remote && OutOfOrder(task, result, procedure)) || !Accepts(task, "completion"))
            {
                return;
            }
            Raise(task, result, procedure);
            // A completion with a Wrong outcome still completes the task; a procedure scores it 0.
            pending[task] = result;
            if (!remote)
            {
                task.CompleteTask();
                // Applied here first, then sent: the room's snapshot never counts it before this client does.
                LocalReported?.Invoke(task, EventOf(ProcedureNetworkEvent.Complete, result));
            }
            else if (task is TaskVirtuademy shared)
            {
                // As the task network's own completion does: no echo to the room.
                shared.ForceTaskComplete(shared.TaskID);
            }
            else
            {
                task.CompleteTask();
            }
            // A task that did not complete (CompleteTask completes To Do tasks only) keeps no stale result.
            pending.Remove(task);
        }

        public static void Attempt(Task task, TaskResult result) => Attempt(task, result, false);

        public static void Attempt(Task task, TaskResult result, bool remote)
        {
            ProcedureRun procedure = ProcedureOf(task);
            if ((!remote && OutOfOrder(task, result, procedure)) || !Accepts(task, "attempt"))
            {
                return;
            }
            Raise(task, result, procedure);
            if (task.TryGetComponent(out TaskStepPlaceholder step))
            {
                step.RaiseAttempted(result);
            }
            procedure?.OnAttempt(task, result);
            if (!remote)
            {
                LocalReported?.Invoke(task, EventOf(ProcedureNetworkEvent.Attempt, result));
            }
        }

        public static void Fail(Task task, string reason) => Fail(task, reason, false);

        public static void Fail(Task task, string reason, bool remote)
        {
            if (!Accepts(task, "failure"))
            {
                return;
            }
            failReasons[task] = reason ?? "";
            task.FailTask();
            failReasons.Remove(task);
            if (!remote)
            {
                LocalReported?.Invoke(task, new ProcedureNetworkEvent { Kind = ProcedureNetworkEvent.Fail, Reason = reason ?? "" });
            }
        }

        /// <summary>A distractor used, with or without a task: raised, and charged by the task's procedure.</summary>
        public static void Distractor(Task task, ProcedureRun procedure, TaskResult result, float penaltyPoints, bool countsAsError)
            => Distractor(task, procedure, result, penaltyPoints, countsAsError, false);

        public static void Distractor(Task task, ProcedureRun procedure, TaskResult result, float penaltyPoints,
            bool countsAsError, bool remote)
        {
            if (task != null)
            {
                TasksApi.Instance.RaiseResultReported(task.gameObject, result);
            }
            procedure?.OnDistractor(task, result, penaltyPoints, countsAsError);
            if (!remote && (task != null || procedure != null))
            {
                ProcedureNetworkEvent e = EventOf(ProcedureNetworkEvent.Distractor, result);
                e.Penalty = penaltyPoints;
                e.CountsAsError = countsAsError;
                LocalReported?.Invoke(task, e);
                if (task == null)
                {
                    procedure.ReportDistractorWithoutTask(e);
                }
            }
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
            // Sent as the wrong attempt it counts as: another client may have the task unlocked already.
            LocalReported?.Invoke(task, EventOf(ProcedureNetworkEvent.Attempt, wrong));
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
