
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get CMUser")]
    [UnitSurtitle("CMUser")]
    [UnitShortTitle("Get CMUser")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCMUserNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CMUser { get; private set; }

        protected override void Definition()
        {
            CMUser = ValueOutput(nameof(CMUser), (f) => IVirtuademyFramework.Current.Session.LocalUser);
        }
    }
}
