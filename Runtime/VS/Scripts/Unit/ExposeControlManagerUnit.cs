using Virtuademy.Environments.ScriptingApi.Placeholders;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: Expose Control Manager")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("Control Manager Instance")]
    [UnitCategory("Virtuademy\\Expose")]
    public class ExposeControlManagerUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput ControlManager { get; private set; }
      
        [DoNotSerialize]
        public ValueOutput InformativeItemList { get; private set; }

        protected override void Definition()
        {
            ControlManager = ValueInput<ControlManager>(nameof(ControlManager), null).NullMeansSelf();

            InformativeItemList = ValueOutput(nameof(InformativeItemList), (flow) => flow.GetValue<ControlManager>(ControlManager).informativeItems);
        }
    }
}
