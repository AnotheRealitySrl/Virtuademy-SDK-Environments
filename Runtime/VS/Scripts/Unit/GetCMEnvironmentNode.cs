
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMEnvironment: Get CMEnvironment")]
    [UnitSurtitle("CMEnvironment")]
    [UnitShortTitle("Get CMEnvironment")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCMEnvironmentNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CMEnvironment { get; private set; }

        protected override void Definition()
        {
            CMEnvironment = ValueOutput(nameof(CMEnvironment), (f) => IVirtuademyFramework.Current.Session.Environment);
        }
    }
}
