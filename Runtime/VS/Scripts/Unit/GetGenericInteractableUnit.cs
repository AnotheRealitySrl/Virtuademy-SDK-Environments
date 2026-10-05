using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;

using UnityEngine;


namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy GameObject: Get Visual Scripting Interactable")]
    [UnitSurtitle("GameObject")]
    [UnitShortTitle("Get Visual Scripting Interactable")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetVisualScriptingInteractableUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput GameObject { get; private set; }

        [DoNotSerialize]
        public ValueOutput VisualScriptingInteractable { get; private set; }

        protected override void Definition()
        {
            GameObject = ValueInput<GameObject>(nameof(GameObject), null).NullMeansSelf();

            VisualScriptingInteractable = ValueOutput(nameof(IVisualScriptingInteractable), (flow) => flow.GetValue<GameObject>(GameObject).GetComponent<IVisualScriptingInteractable>());
        }


    }
}
