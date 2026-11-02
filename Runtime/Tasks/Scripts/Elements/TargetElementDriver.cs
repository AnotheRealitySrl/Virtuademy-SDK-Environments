using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Target element: an interaction (or a select) completes the task, Right.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class TargetElementDriver : ElementDriver
    {
        private InteractablePlaceholder interactable;

        protected override void OnBind()
        {
            interactable = GetComponent<InteractablePlaceholder>();
            if (interactable == null)
            {
                Debug.LogWarning("[TargetElement] " + name + " needs an Interactable placeholder: nothing will complete it.", this);
                return;
            }
            UseOneClick(((TargetElementPlaceholder)Element).OnSelect);
            if (((TargetElementPlaceholder)Element).OnSelect)
            {
                interactable.OnSelectEnter += OnAction;
            }
            else
            {
                interactable.OnInteract += OnAction;
            }
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.OnSelectEnter -= OnAction;
                interactable.OnInteract -= OnAction;
            }
        }

        private void OnAction(InteractionEventArgs args) => Report(TaskResult.Right(gameObject), true);
    }
}
