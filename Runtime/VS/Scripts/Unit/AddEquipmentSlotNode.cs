using Unity.VisualScripting;
using UnityEngine;
using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Declares one more equipment slot of the inventory during the experience
    /// (<see cref="InventoryPlaceholder.AddEquipmentSlot"/>): the hand, or a custom wear slot by name.
    /// </summary>
    [UnitTitle("Virtuademy Inventory: Add Equipment Slot")]
    [UnitSurtitle("Inventory")]
    [UnitShortTitle("Add Equipment Slot")]
    [UnitCategory("Virtuademy\\Flow")]
    public class AddEquipmentSlotNode : Unit
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
        public ValueInput Type { get; private set; }

        [DoNotSerialize]
        public ValueInput SlotName { get; private set; }

        [DoNotSerialize]
        public ValueInput Icon { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), Enter);
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Inventory = ValueInput<InventoryPlaceholder>(nameof(Inventory), null).NullMeansSelf();
            Type = ValueInput(nameof(Type), InventoryPlaceholder.EEquipmentSlotType.Custom);
            SlotName = ValueInput(nameof(SlotName), string.Empty);
            Icon = ValueInput<Sprite>(nameof(Icon), null);

            Requirement(Inventory, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }

        private ControlOutput Enter(Flow flow)
        {
            InventoryPlaceholder inventory = flow.GetValue<InventoryPlaceholder>(Inventory);
            if (inventory != null)
            {
                inventory.AddEquipmentSlot(flow.GetValue<InventoryPlaceholder.EEquipmentSlotType>(Type),
                    flow.GetValue<string>(SlotName), flow.GetValue<Sprite>(Icon));
            }
            return OutputTrigger;
        }
    }
}
