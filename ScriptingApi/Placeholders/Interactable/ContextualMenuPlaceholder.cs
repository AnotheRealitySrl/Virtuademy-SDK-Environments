
using UnityEngine;

using static Virtuademy.Environments.ScriptingApi.Interaction.IContextualMenuManageable;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ContextualMenuPlaceholder : InteractionBehaviourPlaceholder
    {
        #region Contextual menu

        [Header("Contextual menu")]
        [HelpBox("Please note that only \"Duplicate\" and \"Reset Transform\" options are currently not supported. "
            , HelpBoxMessageType.Warning)]

        [SerializeField, Tooltip("Select how many options will be available in this menu")]
        private EContextualMenuOption contextualMenuOptions =
            EContextualMenuOption.Delete;

        public EContextualMenuOption ContextualMenuOptions { get => contextualMenuOptions; set => contextualMenuOptions = value; }
        #endregion

        #region Options Settings
        #region Color picker
        [Tooltip("If true the color picker will color only the first mesh in the hierarchy")]
        public bool colorFirstMeshOnly;
        #endregion

        #region ModelExploder
        [Tooltip("If true users will be able to move the object submeshes when the object is exploded")]
        public bool explosionMoveSubmeshes;
        #endregion

        #region LockObject
        [Tooltip("If true the object submeshes will be used to interact with the object, instead of the object main collider")]
        public bool useSubmeshesOnLock = true;
        #endregion

        #endregion
    }
}
