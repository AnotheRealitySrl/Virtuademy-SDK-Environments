using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Tools: Set Alpha")]
    [UnitSurtitle("SetAlpha")]
    [UnitShortTitle("Set Alpha")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetToolAlphaNode : Unit
    {
        [DoNotSerialize]
        public ValueInput Alpha { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            Alpha = ValueInput<float>(nameof(Alpha));

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Tools.SetInventoryAlpha(f.GetValue<float>(Alpha));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

    }
}
