using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Distractor element: an interaction is a wrong attempt that costs points and, if set, an error.</summary>
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
            ProcedureRun procedure = task != null ? ProcedureOf(task) : ProcedureForNoTask();
            TaskReports.Distractor(task, procedure, result, Distractor.PenaltyPoints, Distractor.CountsAsError);
            if (task == null)
            {
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
