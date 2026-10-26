using Unity.VisualScripting;
using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Switches the inventory placeholder's active system during the experience: none, the tools radial menu or the
    /// inventory of pickable items (<see cref="InventoryPlaceholder.InventoryMode"/>).
    /// </summary>
    [UnitTitle("Virtuademy Inventory: Set Mode")]
    [UnitSurtitle("Inventory")]
    [UnitShortTitle("Set Mode")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetInventoryModeNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Inventory { get; private set; }

        [DoNotSerialize]
        public ValueInput Mode { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), Enter);
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Inventory = ValueInput<InventoryPlaceholder>(nameof(Inventory), null).NullMeansSelf();
            Mode = ValueInput(nameof(Mode), InventoryPlaceholder.EInventoryMode.Inventory);

            Requirement(Inventory, InputTrigger);
            Requirement(Mode, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }

        private ControlOutput Enter(Flow flow)
        {
            InventoryPlaceholder inventory = flow.GetValue<InventoryPlaceholder>(Inventory);
            if (inventory != null)
            {
                inventory.InventoryMode = flow.GetValue<InventoryPlaceholder.EInventoryMode>(Mode);
            }
            return OutputTrigger;
        }
    }
}
