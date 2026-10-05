
using System.Threading.Tasks;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Character: Pan")]
    [UnitSurtitle("Character")]
    [UnitShortTitle("Pan")]
    [UnitCategory("Virtuademy\\Flow")]
    public class PanNode : AwaitableUnit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput TargetTransform { get; private set; }

        protected override void Definition()
        {
            TargetTransform = ValueInput<Transform>(nameof(TargetTransform));

            base.Definition();
        }

        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> ready = new();
            IVirtuademyGameplay.Current.Player.PanCameraAround(flow.GetValue<Transform>(TargetTransform),
                                                               () => ready.TrySetResult(true));

            return ready.Task;
        }
    }
}
