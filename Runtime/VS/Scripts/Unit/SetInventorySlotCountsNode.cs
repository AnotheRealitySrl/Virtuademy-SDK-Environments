using Unity.VisualScripting;
using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Gives the inventory more wear and inventory slots during the experience
    /// (<see cref="InventoryPlaceholder.SetSlotCounts"/>): counts only grow.
    /// </summary>
    [UnitTitle("Virtuademy Inventory: Set Slot Counts")]
    [UnitSurtitle("Inventory")]
    [UnitShortTitle("Set Slot Counts")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetInventorySlotCountsNode : Unit
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
        public ValueInput WearSlots { get; private set; }

        [DoNotSerialize]
        public ValueInput InventorySlots { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), Enter);
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Inventory = ValueInput<InventoryPlaceholder>(nameof(Inventory), null).NullMeansSelf();
            WearSlots = ValueInput(nameof(WearSlots), 1);
            InventorySlots = ValueInput(nameof(InventorySlots), 2);

            Requirement(Inventory, InputTrigger);
            Requirement(WearSlots, InputTrigger);
            Requirement(InventorySlots, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }

        private ControlOutput Enter(Flow flow)
        {
            InventoryPlaceholder inventory = flow.GetValue<InventoryPlaceholder>(Inventory);
            if (inventory != null)
            {
                inventory.SetSlotCounts(flow.GetValue<int>(WearSlots), flow.GetValue<int>(InventorySlots));
            }
            return OutputTrigger;
        }
    }
}
