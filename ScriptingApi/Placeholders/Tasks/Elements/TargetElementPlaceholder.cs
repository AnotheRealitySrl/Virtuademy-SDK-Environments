using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the user interacts with the object (or selects it) and the task is completed, Right. Needs an
    /// Interactable and a Visual Scripting Interactable on the object (the <c>ProcTarget</c> prefab has them).
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Target")]
        [SerializeField, Tooltip("Complete on select instead of on interact.")]
        private bool onSelect = false;

        public bool OnSelect { get => onSelect; set => onSelect = value; }
    }
}
