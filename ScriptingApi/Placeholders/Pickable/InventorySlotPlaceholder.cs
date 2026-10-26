using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class InventorySlotPlaceholder : SceneComponentPlaceholderNetwork
    {
        [Tooltip("Number of wear slots in the wear inventory. You cannot equip or hold more that this value items")]
        public int wearSlotsCount = 1;

        [Tooltip("Number of slots in the inventory. You cannot equip or hold more that this value items")]
        public int inventorySlotsCount = 2;

        [Tooltip("Whether or not I want the inventory to show how many items the user has of a certain type")]
        public bool showItemCount = true;

        [Tooltip("Items each player has in the inventory from the start, routed as if picked up: a Wearable to the "
                 + "wear slots, a tool to the tool wheel, anything else to the inventory. Use prefabs (or objects that are "
                 + "not in the scene): an object listed here is not removed from the scene.")]
        public List<PickablePlaceholder> startingItems = new();
    }
}
