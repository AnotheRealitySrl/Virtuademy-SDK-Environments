using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Task: Show Introduction")]
    [UnitSurtitle("Tasks")]
    [UnitShortTitle("Show introduction")]
    [UnitCategory("Virtuademy\\Flow")]
    public class TaskUIShowIntroNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput TaskUIPlaceholder { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput ShowUI { get; private set; }

        protected override void Definition()
        {
            TaskUIPlaceholder = ValueInput<TaskUIPlaceholder>(nameof(TaskUIPlaceholder), null).NullMeansSelf();
            ShowUI = ValueInput<bool>(nameof(ShowUI), false);

            InputTrigger = ControlInput(nameof(InputTrigger), Output);
            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<TaskUIPlaceholder>(TaskUIPlaceholder).DisplayIntroduction(flow.GetValue<bool>(ShowUI));
            return OutputTrigger;
        }
    }
}
