using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// Gives every <see cref="ProcedurePlaceholder"/> its <see cref="ProcedureRun"/>, and every
    /// <see cref="TaskStepPlaceholder"/> the way to report on its task, when they wake up: in the application and in a
    /// creator's play mode alike.
    /// </summary>
    internal static class ProcedureRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Without a domain reload the static events keep their handlers: never subscribe twice.
            ProcedurePlaceholder.Created -= OnProcedureCreated;
            ProcedurePlaceholder.Created += OnProcedureCreated;
            TaskStepPlaceholder.Created -= OnStepCreated;
            TaskStepPlaceholder.Created += OnStepCreated;
        }

        private static void OnProcedureCreated(ProcedurePlaceholder procedure)
        {
            if (!procedure.TryGetComponent(out ProcedureRun run))
            {
                run = procedure.gameObject.AddComponent<ProcedureRun>();
            }
            run.Bind(procedure);
        }

        private static void OnStepCreated(TaskStepPlaceholder step)
        {
            if (!step.TryGetComponent(out Task task))
            {
                Debug.LogWarning("[TaskStep] " + step.name + ": the Task Step placeholder goes on a task's object (with its " +
                    "Task); it does nothing here.", step);
                return;
            }
            step.Sink = new TaskStepSink(task);
        }

        /// <summary>The Task Step placeholder's methods: the same paths as <see cref="ITasksApi"/>.</summary>
        private sealed class TaskStepSink : ITaskStepSink
        {
            private readonly Task task;

            public TaskStepSink(Task task)
            {
                this.task = task;
            }

            public void Complete(TaskResult result) => TaskReports.Complete(task, result ?? new TaskResult(ETaskOutcome.Right));

            public void Attempt(TaskResult result) => TaskReports.Attempt(task, result ?? new TaskResult(ETaskOutcome.Wrong));

            public void Fail(string reason) => TaskReports.Fail(task, reason);

            public void Unlock() => task.UnlockTask();

            public void ShowHint()
            {
                TaskSystemVirtuademy system = task.GetComponentInParent<TaskSystemVirtuademy>(true);
                if (system == null || system.Procedure == null)
                {
                    Debug.Log("[TaskStep] " + task.name + ": hints need a Procedure placeholder on the task system.", task);
                    return;
                }
                system.Procedure.GiveHint(task);
            }
        }
    }
}
