using System;

using Virtuademy.Environments.ScriptingApi.Interaction;

namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    public interface IContextualMenuManageable : IInteractableBehaviour
    {
        public enum EContextualMenuInteractableState
        {
            Idle,
            Showing
        }

        public enum EContextualMenuType
        {
            Default,
            VideoPlayer,
            PresentationPlayer,
            TextBox,
        }

        [Flags]
        public enum EContextualMenuOption
        {
            None = 0,
            LockTransform = 1,
            ResetTransform = 2,
            Duplicate = 4,
            Delete = 8,
            ColorPicker = 16,
            Explodable = 32,
            NonProportionalScale = 64,
            Edit = 128,
        }

        EContextualMenuType ContextualMenuType { get; }
        EContextualMenuOption ContextualMenuOptions { get; }

    }
}
