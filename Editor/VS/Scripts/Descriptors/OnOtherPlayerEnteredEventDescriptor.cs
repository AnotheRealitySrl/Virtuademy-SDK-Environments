using Virtuademy.SDK.Environments.VisualScripting;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(OnOtherPlayerEnteredEventNode))]
    public class OnOtherPlayerEnteredEventDescriptor : UnitDescriptor<OnOtherPlayerEnteredEventNode>
    {
        public OnOtherPlayerEnteredEventDescriptor(OnOtherPlayerEnteredEventNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This event will be triggered when a player enters the Virtuademy event where the local " +
                "player currently is.\n" +
                "It won't be triggered by the local player upon entering a Virtuademy event.";
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
