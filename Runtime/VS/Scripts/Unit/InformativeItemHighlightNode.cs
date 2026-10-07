using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Highlight Item with material")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("Highlight item")]
    [UnitCategory("Virtuademy\\Flow")]
    public class InformativeItemHighlightNode : Unit
    {
        [DoNotSerialize]
        public ValueInput Informative_Highlight { get; private set; }

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
            Informative_Highlight = ValueInput<Informative_Highlight>(nameof(Informative_Highlight), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<Informative_Highlight>(Informative_Highlight).Highlight(flow.GetValue<int>(Index));
            return outputTrigger;
        }
    }

}
