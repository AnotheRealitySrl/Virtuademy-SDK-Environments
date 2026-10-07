using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Networking: Get Current Shard Open State")]
    [UnitSurtitle("Networking")]
    [UnitShortTitle("Get Current Shard Open State")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCurrentShardOpenStateNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabel("Is Open")]
        public ValueOutput IsOpen { get; private set; }

        [DoNotSerialize]
        [PortLabel("Has Shard")]
        public ValueOutput HasShard { get; private set; }

        protected override void Definition()
        {
            // IsOpen was bool?, where null meant "the player is in no shard at all". A nullable
            // cannot cross the script surface, so the two facts are two ports: IsOpen answers the
            // question the node is named for, and HasShard says whether that answer means
            // anything. A graph that only ever checked for true keeps working off IsOpen alone.
            IsOpen = ValueOutput<bool>(nameof(IsOpen),
                                       (f) => IVirtuademyFramework.Current.Session.IsShardOpen);

            HasShard = ValueOutput<bool>(nameof(HasShard),
                                         (f) => IVirtuademyFramework.Current.Session.HasShard);
        }
    }
}
