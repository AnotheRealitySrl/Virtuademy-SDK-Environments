using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;

using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy GameObject: Get Contextual Menu Interactable")]
    [UnitSurtitle("GameObject")]
    [UnitShortTitle("Get Contextual Menu Interactable")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetContextualMenuInteractableUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput GameObject { get; private set; }

        [DoNotSerialize]
        public ValueOutput ContextualMenuInteractable { get; private set; }

        protected override void Definition()
        {
            GameObject = ValueInput<GameObject>(nameof(GameObject), null).NullMeansSelf();

            ContextualMenuInteractable = ValueOutput(nameof(ContextualMenuInteractable), (flow) => flow.GetValue<GameObject>(GameObject).GetComponent<IContextualMenuManageable>());
        }


    }
}
