using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <remarks>
    /// Slot types (2026.7): a tool (<see cref="ToolItemPlaceholder"/>) goes in the hand; a wearable
    /// goes in a custom slot that only keeps the worn state. With an empty Wear Slot a wearable
    /// behaves as before 2026.7 (worn when it enters the inventory, toggled by its cell). With a
    /// Wear Slot name, at most one wearable of that name is worn at a time: one that enters the
    /// inventory while the slot is taken waits unworn, and wearing it takes the other one off.
    /// </remarks>
    [RequireComponent(typeof(PickablePlaceholder))]
    public class WearablePlaceholder : SceneComponentPlaceholderNetwork
    {
        [Tooltip("If set to false then the user cannot unequip this wearable")]
        public bool canRemove = true;

        [Tooltip("Optional: the name of the custom slot this wearable goes in (for example Head, Gloves). "
                 + "Wearables with the same name share the slot: one worn at a time. Empty: no slot (as before 2026.7).")]
        public string wearSlot = "";

        [Tooltip("The local player started wearing it.")]
        public UnityEvent onWorn = new();

        [Tooltip("The local player took it off (also when another wearable took its slot).")]
        public UnityEvent onRemoved = new();
    }
}
