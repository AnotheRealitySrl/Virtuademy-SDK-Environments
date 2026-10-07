using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    /// <summary>
    /// Common interaface for any interactable entity.
    /// </summary>
    public interface IInteractable
    {

        [Flags]
        public enum EInteractableType
        {
            VisualScriptingInteractable = 1,
            Manipulable = 2,
            ContextualMenuInteractable = 4
        }


        //bitmask used to know if an interactable is blocked for various reasons
        [System.Flags]
        public enum EBlockedState
        {
            BlockedByOthersManipulation = 1, //blocked by player manipolation (like when manipulating with ownership)
            BlockedBySelection = 2, //used in the block by selection node --> Never set by ownership
            BlockedByGenericLogic = 4, //the interactions are blocked --> Set by general scripts. When in this state interaction are stopped and the interactable script is usually set to false
            BlockedByPermissions = 8, //interactions blocked by a missing permission
            BlockedByLockObject = 16, //interactions blocked because someone has locked the object
            BlockedByEdit = 32, //interactions blocked because the object is in edit mode
            BlockedByOtherUser = 64, //another user is currently using the object --> collaborative tools only
        }

        GameObject GameObjectRef { get; }

        bool IsHovered { get; }

        /// <summary>
        /// Gameobject that rapresents the bounding box of the interactable entity.
        /// The scale of this object is used to calculate the size of the interactable entity.
        /// The center of the interactable object corrisponds to the position of the bounding box.
        /// </summary>
        GameObject BoundingBox { get; }

        public List<Collider> Colliders { get; }

        public EBlockedState CurrentBlockedState { get; set; }

        public bool IsBlocked { get; }

        UnityEvent<EBlockedState> OnCurrentBlockedChanged { get; }

        Task Setup();
        Task SetupSubmeshes();
        void EnableColliders(bool value);
    }
}