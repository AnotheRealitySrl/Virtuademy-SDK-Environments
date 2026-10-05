using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy interactable: SetFocusedInteractable")]
    [UnitSurtitle("Interactable")]
    [UnitShortTitle("SetFocusedInteractable")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetFocusedInteractableNode : Unit
    {
        [NullMeansSelf]
        public ValueInput Interactable { get; private set; }

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
            Interactable = ValueInput<ManipulablePlaceholder>(nameof(Interactable), null).NullMeansSelf();

            Succession(inputTrigger, outputTrigger);
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            //IVirtuademyGameplay.Current.Player.SetCameraSpeed(flow.GetValue<ManipulablePlaceholder>(Interactable));
            return outputTrigger;
        }
    }
}
