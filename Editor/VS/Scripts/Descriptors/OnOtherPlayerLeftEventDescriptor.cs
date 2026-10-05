using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(OnOtherPlayerLeftEventNode))]
    public class OnOtherPlayerLeftEventDescriptor : UnitDescriptor<OnOtherPlayerLeftEventNode>
    {
        public OnOtherPlayerLeftEventDescriptor(OnOtherPlayerLeftEventNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This event will be triggered when a player leaves the Virtuademy event where the local " +
                "player currently is.\n" +
                "It won't be triggered by the local player upon leaving a Virtuademy event.";
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            switch (port.key)
            {
                case "UserId":
                    description.summary = "Unique identifier for the player's Virtuademy profile.";
                    break;
                case "PlayerId":
                    description.summary = "Identifier assigned to the player for the current shard.";
                    break;
            }
        }
    }
}
