using System;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// The player's tools, and the feedback a world spawns to answer them.
    /// </summary>
    public interface IToolsApi
    {
        /// <summary>Sets the opacity of the tool inventory, 0 to 1.</summary>
        /// <remarks>Node: <c>Virtuademy Tools: Set Alpha</c>.</remarks>
        void SetInventoryAlpha(float alpha);

        /// <summary>
        /// Spawns the right-or-wrong feedback at a point — the tick or the cross a quiz shows.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy general: Spawn Feedback</c>. WebGL only, as the node says.</remarks>
        void ShowAnswerFeedback(Transform where, bool correct);

        /// <summary>
        /// Closes the contextual menu if one is open. A world calls it before showing something of
        /// its own in the same place.
        /// </summary>
        /// <remarks>Node: <c>Hide Contextual Menu</c>.</remarks>
        void HideContextualMenu();

        /// <summary>
        /// Puts a pickable into the local player's inventory, and says whether it fitted. False
        /// when the inventory is full, or when <paramref name="pickable"/> carries no pickable.
        /// </summary>
        /// <remarks>
        /// Takes the object rather than the placeholder component on it. The component's type is
        /// under <c>Virtuademy.SDK</c>, a namespace the script whitelist denies by prefix, so a
        /// member naming it would be one no interpreted script could call. Node:
        /// <c>Virtuademy inventory: AddPickableToInventoryNode</c>.
        /// </remarks>
        bool AddPickableToInventory(GameObject pickable);

        /// <summary>
        /// The item name of the tool the local player holds, or an empty string when the hand is
        /// empty or the scene has no tool inventory.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Tools: Get Held Tool</c>.</remarks>
        string HeldToolName { get; }

        /// <summary>
        /// Puts the tool with this item name in the local player's hand, as if the player had
        /// picked it from the wheel. True when the tool is held afterwards (also when it already
        /// was); false when the scene has no tool inventory or none of its tools has that name.
        /// </summary>
        /// <remarks>
        /// The application never switches tools on its own: call this only in answer to something
        /// the player did. Node: <c>Virtuademy Tools: Equip Tool</c>.
        /// </remarks>
        bool EquipTool(string toolName);

        /// <summary>Empties the local player's hand. Does nothing when it is already empty.</summary>
        /// <remarks>Node: <c>Virtuademy Tools: Unequip Tool</c>.</remarks>
        void UnequipTool();

        /// <summary>The local player took a tool in hand; carries its item name.</summary>
        /// <remarks>Node: <c>Virtuademy Tools: On Tool Equipped</c>.</remarks>
        event Action<string> ToolEquipped;

        /// <summary>The local player put a tool away, or switched from it; carries its item name.</summary>
        /// <remarks>Node: <c>Virtuademy Tools: On Tool Unequipped</c>.</remarks>
        event Action<string> ToolUnequipped;

        /// <summary>
        /// The local player used the held tool: a use started (a tool with a use mode), or the tool
        /// was applied to an item trigger detector. Carries its item name.
        /// </summary>
        /// <remarks>
        /// Per-use detail (held time, the object aimed at, uses left) is on the tool's
        /// <c>ToolItemPlaceholder</c> events. Node: <c>Virtuademy Tools: On Tool Used</c>.
        /// </remarks>
        event Action<string> ToolUsed;
    }
}
