using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Start Informative Item")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("StartInformativeItem")]
    [UnitCategory("Virtuademy\\Flow")]
    public class StartInformativeItem : Unit
    {
        [DoNotSerialize]
        public ValueInput InformativeItemToStart { get; private set; }

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
            InformativeItemToStart = ValueInput<InformativeItem_Abstract>(nameof(InformativeItemToStart), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<InformativeItem_Abstract>(InformativeItemToStart).StartTask();
            return outputTrigger;
        }
    }

}
