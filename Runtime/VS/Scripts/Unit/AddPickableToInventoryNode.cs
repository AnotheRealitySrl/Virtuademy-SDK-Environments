using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy inventory: AddPickableToInventoryNode")]
    [UnitSurtitle("Inventory")]
    [UnitShortTitle("AddPickableToInventoryNode")]
    [UnitCategory("Virtuademy\\Flow")]
    public class AddPickableToInventoryNode : Unit
    {
        [NullMeansSelf]
        public ValueInput Pickable { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput inputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput outputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput Added { get; private set; }
        private bool _addedValue; // ? variabile di istanza per passare il valore

        protected override void Definition()
        {
            inputTrigger = ControlInput(nameof(inputTrigger), Output);
            outputTrigger = ControlOutput("outputTrigger");

            Pickable = ValueInput<PickablePlaceholder>(nameof(Pickable), null).NullMeansSelf();
            Added = ValueOutput<bool>(nameof(Added), (f) => _addedValue);

            Succession(inputTrigger, outputTrigger);
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            // The port stays typed - this is compiled package code, and a graph author picks a
            // placeholder, not a bare object. Only the surface call sheds the type.
            PickablePlaceholder pickable = flow.GetValue<PickablePlaceholder>(Pickable);
            _addedValue = pickable != null
                          && IVirtuademyGameplay.Current.Tools.AddPickableToInventory(pickable.gameObject);
            return outputTrigger;
        }
    }
}
