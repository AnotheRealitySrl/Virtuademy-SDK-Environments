using System;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Declares the input settings a world starts with. The application looks for one in the
    /// loaded world and applies its <see cref="settings"/> as the character controller's defaults.
    /// </summary>
    public class InputSettingsMono : MonoBehaviour
    {
        public InputSettings settings;
    }

    /// <summary>
    /// The creator-facing copy of the application's input settings (<c>Virtuademy.SDK.Core.InputSettings</c>
    /// in Virtuademy-SystemCore, which creator projects do not install). The application copies these
    /// values field by field, so the two field sets must stay identical.
    /// </summary>
    [Serializable]
    public class InputSettings
    {
        [Header("Movement")]
        public bool EnableWASDInteraction;
        public bool EnableArrowInteraction;
        public bool EnableNavmeshMovement;
        public bool EnableJoystickInteraction;

        [Header("Camera")]
        public bool EnableMouseDraggingInteraction;
        public bool EnableInputCameraInteraction;
        public bool ThirdPerson = true;
        public bool ConstrainedRotation = false;

        [Header("Zoom")]
        public bool EnableMouseWheelZoom;
        public bool EnablePinchZoom;

        [Header("Jump")]
        public bool EnableJumpWithKey;
        public bool EnableJumpWithButton;

        [Header("Sit")]
        public bool EnableSittingInteraction;

        [Header("Walk")]
        public bool EnableWalkInteraction;
    }
}
