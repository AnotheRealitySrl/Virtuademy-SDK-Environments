using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>Empties the local player's hand.</summary>
    [UnitTitle("Virtuademy Tools: Unequip Tool")]
    [UnitSurtitle("Tools")]
    [UnitShortTitle("Unequip Tool")]
    [UnitCategory("Virtuademy\\Flow")]
    public class UnequipToolNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Tools.UnequipTool();

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
