using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Hover Informative Item")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("HoverInformativeItem")]
    [UnitCategory("Virtuademy\\Flow")]
    public class HoverInformativeItem : Unit
    {
        [DoNotSerialize]
        public ValueInput InformativeItemToHover { get; private set; }

        [DoNotSerialize]
        public ValueInput Hovering { get; private set; }

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
            InformativeItemToHover = ValueInput<InformativeItem_Abstract>(nameof(InformativeItemToHover), null).NullMeansSelf();
            Hovering = ValueInput<bool>(nameof(Hovering)).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<InformativeItem_Abstract>(InformativeItemToHover).Hover(flow.GetValue<bool>(Hovering));
            return outputTrigger;
        }
    }

}
