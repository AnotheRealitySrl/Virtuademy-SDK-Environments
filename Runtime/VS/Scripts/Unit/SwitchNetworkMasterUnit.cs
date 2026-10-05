using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Network: IsMaster")]
    [UnitSurtitle("Virtuademy Network")]
    [UnitShortTitle("Is Master")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SwitchNetworkMasterUnit : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        public ControlOutput True { get; private set; }
        [DoNotSerialize]
        public ControlOutput False { get; private set; }


        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                if (IVirtuademyFramework.Current.Session.IsMasterClient)
                {
                    return True;
                }
                else
                {
                    return False;
                }
            });

            True = ControlOutput(nameof(True));
            False = ControlOutput(nameof(False));

            Succession(InputTrigger, True);
            Succession(InputTrigger, False);
        }
    }
}
