
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Synced Object: Is Owned Locally")]
    [UnitSurtitle("Synced Object")]
    [UnitShortTitle("Is Owned Locally")]
    [UnitCategory("Virtuademy\\Flow")]
    public class CheckOwnershipNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabel("True")]
        public ControlOutput OutputTriggerTrue { get; private set; }
        [DoNotSerialize]
        [PortLabel("False")]
        public ControlOutput OutputTriggerFalse { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput SyncedObject { get; private set; }

        protected override void Definition()
        {
            SyncedObject = ValueInput<SyncedObject>(nameof(SyncedObject), null).NullMeansSelf();
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                if (!IVirtuademyFramework.Current.Session.IsMultiplayer
                || !f.GetValue<SyncedObject>(SyncedObject).IsNetworked
                || f.GetValue<SyncedObject>(SyncedObject).IsOwnedLocally)
                {
                    return OutputTriggerTrue;
                }
                else
                {
                    return OutputTriggerFalse;
                }
            });

            OutputTriggerTrue = ControlOutput(nameof(OutputTriggerTrue));
            OutputTriggerFalse = ControlOutput(nameof(OutputTriggerFalse));

            Succession(InputTrigger, OutputTriggerTrue);
            Succession(InputTrigger, OutputTriggerFalse);
        }
    }
}
