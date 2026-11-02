using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: an object that should not be used. Interacting with it is a wrong attempt with a reason, and never
    /// completes anything. With a task it is a wrong attempt on that task, as a wrong tool is: the task loses the
    /// procedure's error penalty and the procedure counts an error. Without a task (the procedure is then the one set
    /// here, or the one of the task system it is under) it takes <see cref="PenaltyPoints"/> off the total and, with
    /// Counts As Error, is an error.
    /// </summary>
    [DisallowMultipleComponent]
    public class DistractorElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Distractor")]
        [SerializeField, Min(0f), Tooltip("Without a task: points taken off the procedure's total (of its max score). With " +
            "a task the task loses the procedure's error penalty instead.")]
        private float penaltyPoints = 5f;

        [SerializeField, Tooltip("Without a task: whether using it counts as an error (towards Max Errors). With a task " +
            "it always does.")]
        private bool countsAsError = true;

        [SerializeField, Tooltip("Why it is wrong, for the feedback and the summary.")]
        private string reason = "";

        [SerializeField, Tooltip("The procedure to charge when the element has no task. Empty: the procedure of the task " +
            "system this object is under, if any.")]
        private ProcedurePlaceholder procedure;

        public float PenaltyPoints { get => penaltyPoints; set => penaltyPoints = Mathf.Max(0f, value); }
        public bool CountsAsError { get => countsAsError; set => countsAsError = value; }
        public string Reason { get => reason; set => reason = value ?? ""; }
        public ProcedurePlaceholder Procedure { get => procedure; set => procedure = value; }
    }
}
