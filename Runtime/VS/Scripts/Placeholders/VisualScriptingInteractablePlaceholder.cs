using Virtuademy.Environments.ScriptingApi.Placeholders;
using System;

using Unity.VisualScripting;

using UnityEngine;
using static Virtuademy.Environments.ScriptingApi.Interaction.IVisualScriptingInteractable;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    public class VisualScriptingInteractablePlaceholder : InteractionBehaviourPlaceholder
    {
        #region Visual Scripting interaction
        [SerializeField, Tooltip("Reference to the script machine that describes what happens during interaction events." +
            "Utilize \"VisualScriptingInteractableHoverEnter\",\"VisualScriptingInteractableHoverExit\",\"VisualScriptingInteractableSelectEnter\",\"VisualScriptingInteractableSelectExit\"" +
            " and \"VisualScriptingInteractableInteract\" nodes to custumize your interactions")]
        private ScriptMachine interactionsScriptMachine;

        [Header("Allowed states")]

        [SerializeField, Tooltip("Choose which state are enabled on this object in desktop platforms.")]
        private EAllowedVisualScriptingInteractableState desktopAllowedStates = (EAllowedVisualScriptingInteractableState)~0;

        [SerializeField, Tooltip("Choose which state are enabled on this object in VR platforms.")]
        private EAllowedVisualScriptingInteractableState vrAllowedStates = (EAllowedVisualScriptingInteractableState)~0;

        [HideInInspector]
        public Action<GameObject> OnSelectedActionVisualScripting;
        public EAllowedVisualScriptingInteractableState DesktopAllowedStates { get => desktopAllowedStates; set => desktopAllowedStates = value; }
        public EAllowedVisualScriptingInteractableState VRAllowedStates { get => vrAllowedStates; set => vrAllowedStates = value; }
        public ScriptMachine InteractionsScriptMachine { get => interactionsScriptMachine; set => interactionsScriptMachine = value; }

        // Legacy: the VR controllers used to be chosen here, but since 2025 the application gave
        // every interactable both rays and hands, so the value changed nothing. They are set on the
        // Interactable placeholder now (InteractablePlaceholder.VRControllers). Kept, hidden, only so
        // that an explicit "Ray only" or "Hands only" saved here moves there (MigrateVRControllers).
        [SerializeField, HideInInspector]
        private EVRVisualScriptingInteraction vrVisualScriptingInteraction = (EVRVisualScriptingInteraction)~0;

        [Obsolete("Set the VR controllers on the Interactable placeholder: InteractablePlaceholder.VRControllers. " +
            "Setting this moves an explicit Ray-only or Hands-only value there.")]
        public EVRVisualScriptingInteraction VrVisualScriptingInteraction
        {
            get => vrVisualScriptingInteraction;
            set
            {
                vrVisualScriptingInteraction = value;
                MigrateVRControllers();
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            MigrateVRControllers();
        }
#endif

        // Moves an explicit "Ray only" or "Hands only" from the legacy field to the Interactable
        // placeholder, unless that one was already restricted, then clears the legacy value so it
        // moves once. Both or none: nothing to move (neither restricted anything).
        private void MigrateVRControllers()
        {
            bool ray = vrVisualScriptingInteraction.HasFlag(EVRVisualScriptingInteraction.RayInteraction);
            bool hands = vrVisualScriptingInteraction.HasFlag(EVRVisualScriptingInteraction.Hands);
            if (ray == hands)
            {
                return;
            }
            InteractablePlaceholder interactable = InteractionPlaceholder;
            if (interactable == null)
            {
                return;
            }
            InteractablePlaceholder.EVRInteractionControllers all = InteractablePlaceholder.EVRInteractionControllers.Ray | InteractablePlaceholder.EVRInteractionControllers.Hands;
            if ((interactable.VRControllers & all) == all)
            {
                interactable.VRControllers = ray ? InteractablePlaceholder.EVRInteractionControllers.Ray : InteractablePlaceholder.EVRInteractionControllers.Hands;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(interactable);
#endif
            }
            vrVisualScriptingInteraction = (EVRVisualScriptingInteraction)~0;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

    }
}
