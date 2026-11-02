using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the user collects the object into the inventory and the task is completed, Right. Put it beside a
    /// Pickable placeholder (a Wearable too: it goes to its Wear Slot as the inventory does); the player who picks it up
    /// reports. The scene needs an Inventory placeholder. The <c>ProcCollect</c> prefab has the pickable.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Collect")]
        [SerializeField, Tooltip("The pickable to collect. Empty: the one on this object.")]
        private PickablePlaceholder pickable;

        public PickablePlaceholder Pickable { get => pickable; set => pickable = value; }
    }
}
