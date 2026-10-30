using System.Collections.Generic;

using SPACS.Tasks;

using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The task events of a shared task system: every report made here (a result, a failure, a distractor) goes to the
    /// other clients, and theirs are applied here, so each client's procedure, cues and feedback see every action of the
    /// room, with the actor's name. Owned by <see cref="TaskSystemVirtuademy"/> once the application gives it a network;
    /// works with or without a procedure (cues show right and wrong for everyone either way).
    /// </summary>
    internal sealed class TaskNetworkSync
    {
        private readonly TaskSystemVirtuademy system;
        private readonly IProcedureNetwork network;
        private readonly List<Task> tasks = new();

        public TaskNetworkSync(TaskSystemVirtuademy system, IProcedureNetwork network)
        {
            this.system = system;
            this.network = network;
            tasks.AddRange(system.GetComponentsInChildren<Task>(true));
            TaskReports.LocalReported += OnLocal;
            network.EventReceived += OnReceived;
        }

        public IProcedureNetwork Network => network;

        public void Dispose()
        {
            TaskReports.LocalReported -= OnLocal;
            network.EventReceived -= OnReceived;
        }

        /// <summary>The task's index in this system (the same on every client of the scene); -1 when not one of its tasks.</summary>
        public int IndexOf(Task task) => task == null ? -1 : tasks.IndexOf(task);

        public Task TaskAt(int index) => index >= 0 && index < tasks.Count ? tasks[index] : null;

        private void OnLocal(Task task, ProcedureNetworkEvent e)
        {
            int index = IndexOf(task);
            if (index < 0)
            {
                return; // another system's task (a distractor without a task is sent by its procedure)
            }
            e.TaskIndex = index;
            network.SendEvent(e);
        }

        /// <summary>Sends an event that is not a task report (a hint, a distractor without a task, the time running out).</summary>
        public void Send(ProcedureNetworkEvent e) => network.SendEvent(e);

        private void OnReceived(ProcedureNetworkEvent e, string actorName)
        {
            Task task = TaskAt(e.TaskIndex);
            ProcedureRun procedure = system.Procedure;
            TaskResult result = new((ETaskOutcome)e.Outcome, e.Score, task != null ? task.gameObject : null, e.Reason ?? "", "")
            {
                IsLocalActor = false,
                ActorName = actorName ?? "",
            };
            switch (e.Kind)
            {
                case ProcedureNetworkEvent.Attempt when task != null:
                    if (task.Node.Status == TaskStatus.Locked && procedure != null && procedure.TakesOutOfOrder)
                    {
                        TasksApi.Instance.RaiseResultReported(task.gameObject, result);
                        procedure.OnOutOfOrder(task, result);
                    }
                    else
                    {
                        TaskReports.Attempt(task, result, true);
                    }
                    break;
                case ProcedureNetworkEvent.Complete when task != null:
                    TaskReports.Complete(task, result, true);
                    break;
                case ProcedureNetworkEvent.Fail when task != null:
                    TaskReports.Fail(task, e.Reason, true);
                    break;
                case ProcedureNetworkEvent.Distractor:
                    TaskReports.Distractor(task, procedure, result, e.Penalty, e.CountsAsError, true);
                    break;
                case ProcedureNetworkEvent.Hint when task != null:
                    procedure?.GiveHint(task, true);
                    break;
                case ProcedureNetworkEvent.TimeUp:
                    procedure?.OnRemoteTimeUp();
                    break;
            }
        }
    }
}
