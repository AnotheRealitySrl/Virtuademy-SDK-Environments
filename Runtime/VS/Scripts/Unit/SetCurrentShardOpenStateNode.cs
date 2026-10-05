using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Networking: Set Current Shard Open State")]
    [UnitSurtitle("Networking")]
    [UnitShortTitle("Set Current Shard Open State")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetCurrentShardOpenStateNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Open { get; private set; }

        protected override void Definition()
        {
            Open = ValueInput<bool>(nameof(Open), false);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyFramework.Current.Session.SetShardOpen(f.GetValue<bool>(Open));
                //if (f.GetValue<bool>(Open))
                //{
                //    IVirtuademyFramework.Current.Session.SetShardOpen(true);
                //}
                //else
                //{
                //    IVirtuademyFramework.Current.Session.SetShardOpen(false);
                //}
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
