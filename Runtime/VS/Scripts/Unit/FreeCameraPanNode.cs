
using System.Threading.Tasks;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Camera: FreeCameraPan")]
    [UnitSurtitle("Camera")]
    [UnitShortTitle("FreeCameraPan")]
    [UnitCategory("Virtuademy\\Flow")]
    public class FreeCameraPanNode : AwaitableUnit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput TargetTransform { get; private set; }

        [NullMeansSelf]
        public ValueInput MaxZoom { get; private set; }

        [NullMeansSelf]
        public ValueInput MinZoom { get; private set; }

        [NullMeansSelf]
        public ValueInput MaxYRotation { get; private set; }

        [NullMeansSelf]
        public ValueInput MinYRotation { get; private set; }

        [NullMeansSelf]
        public ValueInput MaxXRotation { get; private set; }

        [NullMeansSelf]
        public ValueInput MinXRotation { get; private set; }

        protected override void Definition()
        {
            TargetTransform = ValueInput<Transform>(nameof(TargetTransform));
            MaxZoom = ValueInput<float>(nameof(MaxZoom), 0.0001f);
            MinZoom = ValueInput<float>(nameof(MinZoom), 1f);

            MaxYRotation = ValueInput<float>(nameof(MaxYRotation), 85f);
            MinYRotation = ValueInput<float>(nameof(MinYRotation), -85f);

            MaxXRotation = ValueInput<float>(nameof(MaxXRotation), 180f);
            MinXRotation = ValueInput<float>(nameof(MinXRotation), -180f);

            base.Definition();
        }

        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> ready = new();
            IVirtuademyGameplay.Current.Player.PanCameraAround(flow.GetValue<Transform>(TargetTransform),
                                                               flow.GetValue<float>(MaxZoom),
                                                               flow.GetValue<float>(MinZoom),
                                                               flow.GetValue<float>(MaxYRotation),
                                                               flow.GetValue<float>(MinYRotation),
                                                               flow.GetValue<float>(MaxXRotation),
                                                               flow.GetValue<float>(MinXRotation),
                                                               true,
                                                               () => ready.TrySetResult(true));

            return ready.Task;
        }
    }
}
