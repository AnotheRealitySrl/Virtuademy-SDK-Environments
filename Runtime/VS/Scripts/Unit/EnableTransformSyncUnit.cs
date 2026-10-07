using Virtuademy.SDK.Core.Networking;

using Unity.VisualScripting;

using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Transform: EnableSync")]
    [UnitSurtitle("Transform")]
    [UnitShortTitle("EnableSync")]
    [UnitCategory("Virtuademy\\Flow")]
    [TypeIcon(typeof(UnityEngine.Transform))]

    public class EnableTransformSyncUnit : Unit
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
        public ValueInput Target { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Enable { get; private set; }

        protected override void Definition()
        {
            Enable = ValueInput<bool>(nameof(Enable));

            Target = ValueInput<Transform>(nameof(Target));

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                if (f.GetValue<Transform>(Target).GetComponentInParent<INetworkObject>() is INetworkObject trasformView)
                {
                    ((MonoBehaviour)trasformView).enabled = f.GetValue<bool>(Enable);
                }
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }


    }

}
