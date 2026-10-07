using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Reset Highlight Item")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("Reset Highlight")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ResetInformativeHighlightNode : Unit
    {
        [DoNotSerialize]
        public ValueInput Informative_Highlight { get; private set; }

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
            Informative_Highlight = ValueInput<Informative_Highlight>(nameof(Informative_Highlight), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<Informative_Highlight>(Informative_Highlight).ResetMaterials();
            return outputTrigger;
        }
    }

}
