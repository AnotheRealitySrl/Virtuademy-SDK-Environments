using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: End Informative Item")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("EndInformativeItem")]
    [UnitCategory("Virtuademy\\Flow")]
    public class EndInformativeItemNode : Unit
    {
        [DoNotSerialize]
        public ValueInput InformativeItemToEnd { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput inputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput outputTrigger { get; private set; }

        protected override void Definition()
        {
            inputTrigger = ControlInput(nameof(inputTrigger), Output);
            outputTrigger = ControlOutput("outputTrigger");
            InformativeItemToEnd = ValueInput<InformativeItem_Abstract>(nameof(InformativeItemToEnd), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<InformativeItem_Abstract>(InformativeItemToEnd).CompleteTask();
            return outputTrigger;
        }
    }
}
