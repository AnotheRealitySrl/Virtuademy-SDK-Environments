using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Distractor element: an interaction is a wrong attempt, on its task when it has one (the task's error
    /// penalty and an error), else on the procedure (Penalty Points and, if set, an error).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class DistractorElementDriver : ElementDriver
    {
        private InteractablePlaceholder interactable;

        private DistractorElementPlaceholder Distractor => (DistractorElementPlaceholder)Element;

        protected override void OnBind()
        {
            interactable = GetComponent<InteractablePlaceholder>();
            if (interactable == null)
            {
                Debug.LogWarning("[DistractorElement] " + name + " needs an Interactable placeholder.", this);
                return;
            }
            interactable.OnInteract += OnAction;
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.OnInteract -= OnAction;
            }
        }

        private void OnAction(InteractionEventArgs args) => Use();

        /// <summary>The distractor used: charged by the procedure, shown on the cue, raised.</summary>
        public void Use()
        {
            TaskResult result = TaskResult.Wrong(gameObject, Distractor.Reason);
            Task task = TaskOf();
            if (task != null && !MayReport(task))
            {
                return;
            }
            if (task != null)
            {
                // A wrong attempt on its task, as a wrong tool is (user decision, 2026-11-02): the task's error penalty
                // and a procedure error; Penalty Points are for a distractor without a task
                TaskReports.Attempt(task, result);
            }
            else
            {
                TaskReports.Distractor(null, ProcedureForNoTask(), result, Distractor.PenaltyPoints, Distractor.CountsAsError);
                ShowOnOwnCue(false);
            }
            Element.RaiseReported(result);
        }

        private ProcedureRun ProcedureForNoTask()
        {
            if (Distractor.Procedure != null && Distractor.Procedure.TryGetComponent(out ProcedureRun set))
            {
                return set;
            }
            TaskSystemVirtuademy system = GetComponentInParent<TaskSystemVirtuademy>(true);
            return system != null ? system.Procedure : null;
        }
    }
}
