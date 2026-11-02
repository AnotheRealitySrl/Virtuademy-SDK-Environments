using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the right item placed on the target (or the right tool used on it) completes the task, Right; a
    /// wrong item is a wrong attempt. Put it beside an Inventory Item Trigger Detector placeholder, which says which item
    /// and how it is activated (on contact or on use).
    /// </summary>
    /// <remarks>
    /// Items do not say who holds them yet, so in a shared procedure the master reports each event (once for the room);
    /// Local Player Only is not possible until they do: the element warns once and behaves as Any Player.
    /// </remarks>
    [DisallowMultipleComponent]
    public class ItemOnTargetElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Item on target")]
        [SerializeField, Tooltip("The detector. Empty: the one on this object.")]
        private InventoryItemTriggerDetectorPlaceholder detector;

        [SerializeField, Tooltip("Why a wrong item is wrong, for the feedback and the summary.")]
        private string wrongReason = "";

        public InventoryItemTriggerDetectorPlaceholder Detector { get => detector; set => detector = value; }
        public string WrongReason { get => wrongReason; set => wrongReason = value ?? ""; }
    }
}
