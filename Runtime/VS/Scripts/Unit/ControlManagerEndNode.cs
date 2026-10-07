using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: End Control Manager")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("End Control manager")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ControlManagerEndNode : Unit
    {
        [DoNotSerialize]
        public ValueInput ControlManagerReference { get; private set; }

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
            ControlManagerReference = ValueInput<ControlManager>(nameof(ControlManagerReference), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<ControlManager>(ControlManagerReference).CallControlEnd();
            return outputTrigger;
        }
    }

}
