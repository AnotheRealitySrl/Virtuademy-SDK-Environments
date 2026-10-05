using System.Threading.Tasks;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Platform: Check Scene Availability")]
    [UnitSurtitle("Platform")]
    [UnitShortTitle("Check Scene Availability")]
    [UnitCategory("Virtuademy\\Flow")]
    public class CheckSceneAvailabilityNode : AwaitableUnit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput SceneAddressableName { get; private set; }
        public ValueOutput IsAvailable { get; private set; }
        private bool _isAvailable;

        protected override void Definition()
        {
            SceneAddressableName = ValueInput<string>(nameof(SceneAddressableName));
            IsAvailable = ValueOutput<bool>(nameof(IsAvailable), f => _isAvailable);

            base.Definition();
        }

        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> done = new();

            IVirtuademyFramework.Current.Session.FindExperience(flow.GetValue<string>(SceneAddressableName),
                                                               experience =>
            {
                _isAvailable = experience != null;
                done.TrySetResult(true);
            });

            return done.Task;
        }
    }
}
