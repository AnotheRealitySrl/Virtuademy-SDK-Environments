using System;
using System.Threading.Tasks;

using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    public interface IVisualScriptingInteractable : IInteractableBehaviour
    {
        //TO DO: remove this events from the generic interface
        //implement VisualScriptingInteractableHoverDetector as a placeholder
        UnityEvent OnHoverGrabEnter { get; }
        UnityEvent OnHoverGrabExit { get; }
        UnityEvent OnHoverRayEnter { get; }
        UnityEvent OnHoverRayExit { get; }
        UnityEvent OnHoverMouseEnter { get; }
        UnityEvent OnHoverMouseExit { get; }
        EVisualScriptingInteractableState CurrentInteractionState { get; }
        bool SkipSelectState { get; }

        public Task EnterInteractionState();
        public Task ExitInteractionState();
        public Task Interact();

        [Flags]
        public enum EVisualScriptingInteractableState
        {
            Idle = 0,
            SelectEntering = 1,
            Selected = 2,
            Interacting = 3,
            SelectExiting = 4,
        }

        [Flags]
        public enum EAllowedVisualScriptingInteractableState
        {
            Selected = 1,
            Interacting = 2,
            Hovered = 4,
        }

        [Flags]
        public enum EVRVisualScriptingInteraction
        {
            RayInteraction = 1,
            Hands = 2
        }
    }
}
