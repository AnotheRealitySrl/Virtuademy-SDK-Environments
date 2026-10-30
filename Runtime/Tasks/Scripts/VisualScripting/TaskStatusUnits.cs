using SPACS.Tasks;
using SPACS.VisualScripting;

using Unity.VisualScripting;

using UnityEngine.Events;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Resolves a node's task input: no task is the graph's error, not something to skip (review S8).</summary>
    internal static class TaskNodeInput
    {
        public static Task Get(Flow flow, ValueInput input, string node)
        {
            Task task = flow.GetValue<Task>(input);
            if (task == null)
            {
                throw new System.ArgumentNullException("Task", "[" + node + "] no Task: connect one, or put the graph on " +
                    "the task's object");
            }
            return task;
        }
    }

    /// <summary>Fails the task (<see cref="Task.FailTask"/>): its status becomes Failed.</summary>
    [UnitTitle("Virtuademy Tasks: Fail Task")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Fail Task")]
    [UnitCategory("Virtuademy\\Flow")]
    public class FailTaskUnit : Unit
    {
        [DoNotSerialize]
        public ValueInput TaskToFail { get; private set; }

        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                TaskNodeInput.Get(flow, TaskToFail, nameof(FailTaskUnit)).FailTask();
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            TaskToFail = ValueInput<Task>(nameof(TaskToFail), null).NullMeansSelf();
            Requirement(TaskToFail, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>Unlocks the task (<see cref="Task.UnlockTask"/>): its status becomes To Do.</summary>
    [UnitTitle("Virtuademy Tasks: Unlock Task")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Unlock Task")]
    [UnitCategory("Virtuademy\\Flow")]
    public class UnlockTaskUnit : Unit
    {
        [DoNotSerialize]
        public ValueInput TaskToUnlock { get; private set; }

        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                TaskNodeInput.Get(flow, TaskToUnlock, nameof(UnlockTaskUnit)).UnlockTask();
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            TaskToUnlock = ValueInput<Task>(nameof(TaskToUnlock), null).NullMeansSelf();
            Requirement(TaskToUnlock, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>Raised when the task is unlocked (status To Do).</summary>
    [UnitTitle("Virtuademy Tasks: On Task Unlocked")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("On Task Unlocked")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTaskUnlockedEventUnit : UnityEventUnit<Task>
    {
        protected override bool register => true;

        [DoNotSerialize]
        public ValueInput TaskReference { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            TaskReference = ValueInput<Task>(nameof(TaskReference), null).NullMeansSelf();
        }

        public override EventHook GetHook(GraphReference reference) => new("Task" + nameof(OnTaskUnlockedEventUnit));

        protected override UnityEvent GetEvent(GraphReference reference)
        {
            Task task = Flow.New(reference).GetValue<Task>(TaskReference);
            return task == null ? new UnityEvent() : task.OnTaskUnlocked;
        }

        protected override Task GetArguments(GraphReference reference) => Flow.New(reference).GetValue<Task>(TaskReference);
    }

    /// <summary>Raised when the task fails (status Failed). On Task Completed is raised for a failed task too, with
    /// Completed false; this one only for failures.</summary>
    [UnitTitle("Virtuademy Tasks: On Task Failed")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("On Task Failed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTaskFailedEventUnit : UnityEventUnit<Task, bool>
    {
        protected override bool register => true;

        [DoNotSerialize]
        public ValueInput TaskReference { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            TaskReference = ValueInput<Task>(nameof(TaskReference), null).NullMeansSelf();
        }

        public override EventHook GetHook(GraphReference reference) => new("Task" + nameof(OnTaskFailedEventUnit));

        protected override UnityEvent<bool> GetEvent(GraphReference reference)
        {
            Task task = Flow.New(reference).GetValue<Task>(TaskReference);
            return task == null ? new UnityEvent<bool>() : task.OnTaskCompleted;
        }

        // Task.OnTaskCompleted carries true for completed and false for failed: only failures trigger this node.
        protected override UnityAction<bool> GetData(GraphReference reference)
        {
            return completed =>
            {
                if (!completed)
                {
                    Trigger(reference, GetArguments(reference, completed));
                }
            };
        }

        protected override Task GetArguments(GraphReference reference, bool completed) =>
            Flow.New(reference).GetValue<Task>(TaskReference);
    }
}
