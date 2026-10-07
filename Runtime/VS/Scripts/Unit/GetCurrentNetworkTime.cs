using System.Collections.Generic;

using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Networking: Get current network time")]
    [UnitSurtitle("NetworkTime")]
    [UnitShortTitle("Get Network Time")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCurrentNetworkTime : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput NetworkedTime { get; private set; }

        private List<Flow> runningFlows = new List<Flow>();

        protected override void Definition()
        {
            NetworkedTime = ValueOutput<double>(nameof(NetworkedTime), f => IVirtuademyFramework.Current.Session.NetworkTime);
        }
    }
}
