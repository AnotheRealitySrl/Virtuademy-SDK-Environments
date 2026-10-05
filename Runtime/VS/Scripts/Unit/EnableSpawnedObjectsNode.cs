
using System.Collections.Generic;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Scene: Enable Spawned Objects")]
    [UnitSurtitle("Scene")]
    [UnitShortTitle("Enable Spawned Objects")]
    [UnitCategory("Virtuademy\\Flow")]
    public class EnableSpawnedObjectsNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Enable { get; private set; }

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);

        }

        protected override void Definition()
        {
            Enable = ValueInput<bool>(nameof(Enable));

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Scene.ShowSpawnedObjects(f.GetValue<bool>(Enable), f.stack.AsReference().gameObject);

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
