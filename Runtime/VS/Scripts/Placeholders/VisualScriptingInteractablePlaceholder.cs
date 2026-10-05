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

        [SerializeField, Tooltip("Enables hand and ray interaction on this object")]
        private EVRVisualScriptingInteraction vrVisualScriptingInteraction = (EVRVisualScriptingInteraction)~0;

        public EVRVisualScriptingInteraction VrVisualScriptingInteraction { get => vrVisualScriptingInteraction; set => vrVisualScriptingInteraction = value; }



        #endregion

    }
}
