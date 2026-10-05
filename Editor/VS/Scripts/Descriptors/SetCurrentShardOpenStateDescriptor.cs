using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(SetCurrentShardOpenStateNode))]
    public class SetCurrentShardOpenStateDescriptor : UnitDescriptor<SetCurrentShardOpenStateNode>
    {
        public SetCurrentShardOpenStateDescriptor(SetCurrentShardOpenStateNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will change the open state of the Virtuademy event shard " +
                "where the local player currently is.\n" +
                "Every event shard starts as open, and it can be closed to prevent " +
                "more users from entering it (while still allowing the users already " +
                "inside it to leave). The shard can successively be opened again to " +
                "allow players to enter.";
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            switch (port.key)
            {
                case "Open":
                    description.summary = "Set to true to open the shard. Set to false to close the shard.";
                    break;
            }
        }
    }
}
