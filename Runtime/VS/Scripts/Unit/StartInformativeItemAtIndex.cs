using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Start Informative Item at index")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("StartInformativeItemAtIndex")]
    [UnitCategory("Virtuademy\\Flow")]
    public class StartInformativeItemAtIndex : Unit
    {
        [DoNotSerialize]
        public ValueInput ControlManager { get; private set; }

        [DoNotSerialize]
        public ValueInput Index { get; private set; }


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
            Index = ValueInput<int>(nameof(Index));
            ControlManager = ValueInput<ControlManager>(nameof(ControlManager), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<ControlManager>(ControlManager).StartTaskAtIndex(flow.GetValue<int>(Index));
            return outputTrigger;
        }
    }

}
