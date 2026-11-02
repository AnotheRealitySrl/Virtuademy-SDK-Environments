using System;
using System.Collections.Generic;

using SPACS.VisualScripting;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>The result a node builds from its ports: a negative score means the outcome's own (Right 1, Wrong 0,
    /// Partial 0.5).</summary>
    internal static class TaskResultPorts
    {
        public static TaskResult Read(Flow flow, ValueInput outcome, ValueInput score, ValueInput reason, GameObject source)
        {
            ETaskOutcome o = flow.GetValue<ETaskOutcome>(outcome);
            float s = flow.GetValue<float>(score);
            return s < 0f
                ? new TaskResult(o, new TaskResult(o).Score, source, flow.GetValue<string>(reason), "")
                : new TaskResult(o, s, source, flow.GetValue<string>(reason), "");
        }
    }

    /// <summary>Completes the task with a result (<see cref="ITasksApi.CompleteTask(GameObject, TaskResult)"/>).</summary>
    [UnitTitle("Virtuademy Tasks: Complete Task With Result")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Complete Task With Result")]
    [UnitCategory("Virtuademy\\Flow")]
    public class CompleteTaskWithResultUnit : Unit
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Task { get; private set; }
        [DoNotSerialize] public ValueInput Outcome { get; private set; }
        [DoNotSerialize] public ValueInput Score { get; private set; }
        [DoNotSerialize] public ValueInput Reason { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                GameObject task = flow.GetValue<GameObject>(Task);
                TasksApi.Instance.CompleteTask(task, TaskResultPorts.Read(flow, Outcome, Score, Reason, flow.stack.gameObject));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Task = ValueInput<GameObject>(nameof(Task), null).NullMeansSelf();
            Outcome = ValueInput(nameof(Outcome), ETaskOutcome.Right);
            Score = ValueInput(nameof(Score), -1f);
            Reason = ValueInput(nameof(Reason), "");
            Requirement(Task, InputTrigger);
            Requirement(Outcome, InputTrigger);
            Requirement(Score, InputTrigger);
            Requirement(Reason, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>Reports an attempt that does not complete the task (<see cref="ITasksApi.ReportAttempt"/>).</summary>
    [UnitTitle("Virtuademy Tasks: Report Attempt")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Report Attempt")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ReportAttemptUnit : Unit
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Task { get; private set; }
        [DoNotSerialize] public ValueInput Outcome { get; private set; }
        [DoNotSerialize] public ValueInput Score { get; private set; }
        [DoNotSerialize] public ValueInput Reason { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                GameObject task = flow.GetValue<GameObject>(Task);
                TasksApi.Instance.ReportAttempt(task, TaskResultPorts.Read(flow, Outcome, Score, Reason, flow.stack.gameObject));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Task = ValueInput<GameObject>(nameof(Task), null).NullMeansSelf();
            Outcome = ValueInput(nameof(Outcome), ETaskOutcome.Wrong);
            Score = ValueInput(nameof(Score), -1f);
            Reason = ValueInput(nameof(Reason), "");
            Requirement(Task, InputTrigger);
            Requirement(Outcome, InputTrigger);
            Requirement(Score, InputTrigger);
            Requirement(Reason, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>The state of a task (<see cref="ITasksApi.GetTaskState"/>).</summary>
    [UnitTitle("Virtuademy Tasks: Get Task State")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Get Task State")]
    [UnitCategory("Virtuademy\\Tasks")]
    public class GetTaskStateUnit : Unit
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Task { get; private set; }
        [DoNotSerialize] public ValueOutput State { get; private set; }

        protected override void Definition()
        {
            Task = ValueInput<GameObject>(nameof(Task), null).NullMeansSelf();
            State = ValueOutput(nameof(State), flow => TasksApi.Instance.GetTaskState(flow.GetValue<GameObject>(Task)));
            Requirement(Task, State);
        }
    }

    /// <summary>Raised for every result reported on the task, attempts and completions
    /// (<see cref="ITasksApi.TaskResultReported"/>).</summary>
    [UnitTitle("Virtuademy Tasks: On Task Attempt")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("On Task Attempt")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTaskAttemptEventUnit : InstanceDataEventUnit<TaskResult, TaskResultHandler>
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Task { get; private set; }
        [DoNotSerialize] public ValueOutput Outcome { get; private set; }
        [DoNotSerialize] public ValueOutput Score { get; private set; }
        [DoNotSerialize] public ValueOutput Reason { get; private set; }
        [DoNotSerialize] public ValueOutput IsLocalActor { get; private set; }

        private readonly Dictionary<GraphReference, GameObject> tasks = new();

        protected override void Definition()
        {
            base.Definition();
            Task = ValueInput<GameObject>(nameof(Task), null).NullMeansSelf();
            Outcome = ValueOutput<ETaskOutcome>(nameof(Outcome));
            Score = ValueOutput<float>(nameof(Score));
            Reason = ValueOutput<string>(nameof(Reason));
            IsLocalActor = ValueOutput<bool>(nameof(IsLocalActor));
        }

        public override EventHook GetHook(GraphReference reference) => new("Task" + nameof(OnTaskAttemptEventUnit));

        protected override TaskResultHandler GetData(GraphReference instance)
        {
            return (task, result) =>
            {
                if (tasks.TryGetValue(instance, out GameObject watched) && task == watched)
                {
                    Trigger(instance, result);
                }
            };
        }

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);
            tasks[instance] = Flow.New(instance).GetValue<GameObject>(Task);
            TasksApi.Instance.TaskResultReported += instanceData[instance];
        }

        public override void Uninstantiate(GraphReference instance)
        {
            TasksApi.Instance.TaskResultReported -= instanceData[instance];
            tasks.Remove(instance);
            base.Uninstantiate(instance);
        }

        protected override void AssignArguments(Flow flow, TaskResult result)
        {
            flow.SetValue(Outcome, result.Outcome);
            flow.SetValue(Score, result.Score);
            flow.SetValue(Reason, result.Reason);
            flow.SetValue(IsLocalActor, result.IsLocalActor);
        }
    }
}
