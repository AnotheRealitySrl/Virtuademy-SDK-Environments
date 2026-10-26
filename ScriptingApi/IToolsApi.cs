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
}
}
