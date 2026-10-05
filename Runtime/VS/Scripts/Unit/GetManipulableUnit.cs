using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;

using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy GameObject: Get Manipulable")]
    [UnitSurtitle("GameObject")]
    [UnitShortTitle("Get Manipulable")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetManipulableUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput GameObject { get; private set; }

        [DoNotSerialize]
        public ValueOutput Manipulable { get; private set; }

        protected override void Definition()
        {
            GameObject = ValueInput<GameObject>(nameof(GameObject), null).NullMeansSelf();

            Manipulable = ValueOutput(nameof(Manipulable), (flow) => flow.GetValue<GameObject>(GameObject).GetComponent<IManipulable>());
        }


    }
}
