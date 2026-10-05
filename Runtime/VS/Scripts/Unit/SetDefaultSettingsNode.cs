using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Settings: Set default settings")]
    [UnitSurtitle("SetDefaultSettings")]
    [UnitShortTitle("Set Default Settings")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetDefaultSettingsNode : Unit
    {

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Player.ApplyDefaultInputSettings();
                return OutputTrigger;
            });
        
            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
