using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the user picks the object and confirms. A correct choice completes the task (Right); a wrong one is
    /// a wrong attempt, or completes it wrong with One Shot. The confirmation is a Virtuademy Panel (the
    /// <c>ProcChoice</c> prefab has a <c>VirtuademyPanel_Confirm</c>); without one the pick is confirmed at once.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChoiceElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Choice")]
        [SerializeField, Tooltip("Whether this is a right choice.")]
        private bool isCorrect = true;

        [SerializeField, Tooltip("A wrong choice completes the task (scoring 0) instead of being an attempt.")]
        private bool oneShot = false;

        [SerializeField, Tooltip("The confirmation panel (a Virtuademy Panel: primary button confirms, secondary cancels). " +
            "Empty: no confirmation.")]
        private GameObject confirmPanel;

        [SerializeField, Tooltip("Why a wrong choice is wrong, for the feedback and the summary.")]
        private string wrongReason = "";

        public bool IsCorrect { get => isCorrect; set => isCorrect = value; }
        public bool OneShot { get => oneShot; set => oneShot = value; }
        public GameObject ConfirmPanel { get => confirmPanel; set => confirmPanel = value; }
        public string WrongReason { get => wrongReason; set => wrongReason = value ?? ""; }
    }
}
