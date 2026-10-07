using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class InteractablePlaceholder : SceneComponentPlaceholderNetwork
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


        public bool LockHoverDuringInteraction
        {
            get => lockHoverDuringInteraction; set { lockHoverDuringInteraction = value; }
        }

        public List<Collider> InteractionColliders { get => interactionColliders; set => interactionColliders = value; }

        public bool InteractWithColliders { get => interactWithColliders; set => interactWithColliders = value; }
        #endregion
    }
}
