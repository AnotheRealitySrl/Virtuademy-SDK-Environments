
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMEvent: Get CMEvent")]
    [UnitSurtitle("CMEvent")]
    [UnitShortTitle("Get CMEvent")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCMEventNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CMEvent { get; private set; }

        protected override void Definition()
        {
            CMEvent = ValueOutput(nameof(CMEvent), (f) => IVirtuademyFramework.Current.Session.Details);
        }
    }
}
