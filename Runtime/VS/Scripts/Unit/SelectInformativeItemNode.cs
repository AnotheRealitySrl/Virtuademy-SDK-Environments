using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Select Informative Item")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("SelectInformativeItem")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SelectInformativeItemNode : Unit
    {
        [DoNotSerialize]
        public ValueInput InformativeItemToSelect { get; private set; }

        [DoNotSerialize]
        public ValueInput Selecting { get; private set; }

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
            InformativeItemToSelect = ValueInput<InformativeItem_Abstract>(nameof(InformativeItemToSelect), null).NullMeansSelf();
            Selecting = ValueInput<bool>(nameof(Selecting)).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<InformativeItem_Abstract>(InformativeItemToSelect).Select(flow.GetValue<bool>(Selecting));
            return outputTrigger;
        }
    }

}
