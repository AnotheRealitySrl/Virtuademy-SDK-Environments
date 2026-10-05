using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;


namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy: Expose Visual Scripting Interactable")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("Visual Scripting Interactable")]
    [UnitCategory("Virtuademy\\Expose")]
    public class ExposeVisualScriptingInteractableUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Interactable { get; private set; }

        [DoNotSerialize]
        public ValueOutput GameObjectReference { get; private set; }

        [DoNotSerialize]
        public ValueOutput InteractionState { get; private set; }

        [DoNotSerialize]
        public ValueOutput InteractionColliders { get; private set; }

        protected override void Definition()
        {
            Interactable = ValueInput<IVisualScriptingInteractable>(nameof(Interactable), null).NullMeansSelf();

            GameObjectReference = ValueOutput(nameof(GameObjectReference), (flow) => flow.GetValue<IVisualScriptingInteractable>(Interactable)?.Interactable?.GameObjectRef);

            InteractionState = ValueOutput(nameof(InteractionState), (flow) => flow.GetValue<IVisualScriptingInteractable>(Interactable)?.CurrentInteractionState);

            InteractionColliders = ValueOutput(nameof(InteractionColliders), (flow) => flow.GetValue<IVisualScriptingInteractable>(Interactable)?.Interactable?.Colliders);
        }
    }
}
