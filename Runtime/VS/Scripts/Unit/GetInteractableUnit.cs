using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;

using UnityEngine;


namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy GameObject: Get Interactable")]
    [UnitSurtitle("GameObject")]
    [UnitShortTitle("Get Interactable")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetInteractableUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput GameObject { get; private set; }

        [DoNotSerialize]
        public ValueOutput Interactable { get; private set; }

        protected override void Definition()
        {
            GameObject = ValueInput<GameObject>(nameof(GameObject), null).NullMeansSelf();

            Interactable = ValueOutput(nameof(Interactable), (flow) => flow.GetValue<GameObject>(GameObject).GetComponent<IInteractable>());
        }


    }
}
