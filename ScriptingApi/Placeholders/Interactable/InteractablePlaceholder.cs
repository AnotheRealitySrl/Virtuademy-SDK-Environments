using System;
using System.Collections.Generic;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Interaction;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Makes an object interactable: the application detects, on every platform, when the user
    /// hovers it, selects it and interacts with it, and raises the events below.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every event carries an <see cref="InteractionEventArgs"/>: the controller (mouse, ray, hand,
    /// touch) and, in VR, the hand. They are raised on this client only, not for other users.
    /// Hover, interact and select come from the local user's input; a selection can also end
    /// because a script, a node or the application unselected the object, or because the object
    /// was destroyed, and then the controller is <see cref="EInteractionControllerType.None"/>.
    /// </para>
    /// <para>
    /// <see cref="OnHoverEnter"/> and <see cref="OnHoverExit"/> are raised for every interactable.
    /// <see cref="OnSelectEnter"/>, <see cref="OnSelectExit"/> and <see cref="OnInteract"/> need a
    /// Visual Scripting Interactable placeholder on the same object (its script machine may be
    /// empty), and follow the same rules as its nodes: with the Selected state allowed, the first
    /// click selects the object and a click on the selected object interacts; without it, every
    /// click interacts.
    /// </para>
    /// <para>
    /// Desktop and mobile follow one rule: the click counts when the press and the release are both
    /// on the object. A press that starts on the object and ends elsewhere does nothing, and so does
    /// a press that starts elsewhere. On mobile the object is hovered while the finger that pressed
    /// it is over it.
    /// </para>
    /// </remarks>
    public class InteractablePlaceholder : SceneComponentPlaceholderNetwork, IInteractableEventSink
    {
        #region Settings

        [Header("Interaction settings")]

        [SerializeField, Tooltip("If set to true the user will be able to interact with the interactable using the colliders." +
            "If no colliders are listed a box collider will be created based on the object child meshes. " +
            "If false the interactable will work without colliders, but it won't be able to interact using any type of controller interactions (Mouse, Hand, Ray ...)." +
            "This is usually set to false if the interactions are only handled via scipt machines or script.")]
        private bool interactWithColliders = true;
        [SerializeField, Tooltip("The colliders that will be recognized by the interactable behaviours")]
        private List<Collider> interactionColliders = new();
        [SerializeField, Tooltip("If set to true the hover callback won't be called if the generic interactable is in a " +
            "state that is different from idle. As an example if the interactable is selected, it will keep the hover state " +
            "until the selection is over and the user is not hovering the object.")]
        private bool lockHoverDuringInteraction = false;

        /// <summary>The VR controllers that can interact with an object.</summary>
        [Flags]
        public enum EVRInteractionControllers
        {
            Ray = 1,
            Hands = 2,
        }

        [SerializeField, Tooltip("VR only: the controllers that can hover, select and interact with this object. " +
            "A controller left out ignores the object entirely: no ray highlight, no hover, no events, no interaction " +
            "(a click on it with that controller counts as a click on empty space). " +
            "Grabbing a manipulable is set on the Manipulable placeholder.")]
        private EVRInteractionControllers vrControllers = (EVRInteractionControllers)~0;

        /// <summary>
        /// VR only: the controllers that can hover, select and interact with this object. Read once,
        /// when the object is set up.
        /// </summary>
        public EVRInteractionControllers VRControllers { get => vrControllers; set => vrControllers = value; }


        public bool LockHoverDuringInteraction
        {
            get => lockHoverDuringInteraction; set { lockHoverDuringInteraction = value; }
        }

        public List<Collider> InteractionColliders { get => interactionColliders; set => interactionColliders = value; }

        public bool InteractWithColliders { get => interactWithColliders; set => interactWithColliders = value; }
        #endregion

        #region Events

        /// <summary>
        /// A controller started hovering the object: the mouse pointer, a VR ray or hand, or on
        /// mobile the finger that pressed the object. Raised once per controller, so in VR each hand
        /// raises its own.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Visual Scripting Interactable: On Hover Enter</c>.</remarks>
        public event Action<InteractionEventArgs> OnHoverEnter;

        /// <summary>The controller that raised <see cref="OnHoverEnter"/> stopped hovering the object.</summary>
        /// <remarks>Node: <c>Virtuademy Visual Scripting Interactable: On Hover Exit</c>.</remarks>
        public event Action<InteractionEventArgs> OnHoverExit;

        /// <summary>The object was selected.</summary>
        /// <remarks>Node: <c>Virtuademy Visual Scripting Interactable: On Select Enter</c>.</remarks>
        public event Action<InteractionEventArgs> OnSelectEnter;

        /// <summary>
        /// The object stopped being selected: the user selected something else or clicked on empty
        /// space, or a script or node unselected it.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Visual Scripting Interactable: On Select Exit</c>.</remarks>
        public event Action<InteractionEventArgs> OnSelectExit;

        /// <summary>The user interacted with the object.</summary>
        /// <remarks>Node: <c>Virtuademy Visual Scripting Interactable: On Interact</c>.</remarks>
        public event Action<InteractionEventArgs> OnInteract;

        void IInteractableEventSink.RaiseHoverEnter(InteractionEventArgs args) => Raise(OnHoverEnter, args);
        void IInteractableEventSink.RaiseHoverExit(InteractionEventArgs args) => Raise(OnHoverExit, args);
        void IInteractableEventSink.RaiseSelectEnter(InteractionEventArgs args) => Raise(OnSelectEnter, args);
        void IInteractableEventSink.RaiseSelectExit(InteractionEventArgs args) => Raise(OnSelectExit, args);
        void IInteractableEventSink.RaiseInteract(InteractionEventArgs args) => Raise(OnInteract, args);

        // A failing handler is its script's problem: it must neither break the interaction flow
        // that raised the event nor keep the other handlers from running.
        private void Raise(Action<InteractionEventArgs> handlers, InteractionEventArgs args)
        {
            if (handlers == null)
            {
                return;
            }
            args ??= InteractionEventArgs.None;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<InteractionEventArgs>)handler)(args);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        #endregion
    }
}
