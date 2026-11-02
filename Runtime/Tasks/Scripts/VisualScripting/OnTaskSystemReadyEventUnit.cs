using SPACS.Tasks;
using Unity.VisualScripting;
using UnityEngine.Events;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.Tasks
{
    [UnitTitle("Virtuademy Tasks: On Task System Ready")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("On Task System Ready")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTaskSystemReadyEventUnit : UnityEventUnit<TaskSystemVirtuademy>
    {
        protected override bool register => true;

        [DoNotSerialize]
        public ValueInput TaskSystemReference { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            // The task system, as GetEvent reads it (the port was declared as a Task).
            TaskSystemReference = ValueInput<TaskSystemVirtuademy>(nameof(TaskSystemReference), null).NullMeansSelf();
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("Task" + this.ToString().Split("EventUnit")[0]);
        }

        protected override UnityEvent GetEvent(GraphReference reference)
        {
            var taskSystemReference = Flow.New(reference).GetValue<TaskSystemVirtuademy>(TaskSystemReference);
            if (taskSystemReference == null)
            {
                return new UnityEvent();
            }
            else
            {
                return taskSystemReference.OnTaskSystemReady;
            }
        }

        protected override TaskSystemVirtuademy GetArguments(GraphReference reference)
        {
            return Flow.New(reference).GetValue<TaskSystemVirtuademy>(TaskSystemReference);
        }
    }
}
