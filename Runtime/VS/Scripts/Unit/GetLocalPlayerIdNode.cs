using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Networking: Get Local Player ID")]
    [UnitSurtitle("Networking")]
    [UnitShortTitle("Get Local Player ID")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetLocalPlayerIdNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabel("PlayerId")]
        public ValueOutput SessionId { get; private set; }

        protected override void Definition()
        {
            SessionId = ValueOutput(nameof(SessionId), (f) => IVirtuademyFramework.Current.Session.SessionId);
        }
    }
}
