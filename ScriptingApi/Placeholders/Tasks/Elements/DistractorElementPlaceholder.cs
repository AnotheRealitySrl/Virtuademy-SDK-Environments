using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: an object that should not be used. Interacting with it is a wrong attempt with a reason: an error
    /// for the procedure (Counts As Error) and <see cref="PenaltyPoints"/> off the total. Never completes anything. The
    /// task is optional (the procedure is then the one set here, or the one of the task system it is under).
    /// </summary>
    [DisallowMultipleComponent]
    public class DistractorElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Distractor")]
        [SerializeField, Min(0f), Tooltip("Points taken off the procedure's total (of its max score).")]
        private float penaltyPoints = 5f;

        [SerializeField, Tooltip("Whether using it counts as an error (towards Max Errors).")]
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
