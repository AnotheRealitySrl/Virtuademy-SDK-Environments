using UnityEngine;
using UnityEngine.Events;
using static Virtuademy.Environments.ScriptingApi.Interaction.IManipulable;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ManipulablePlaceholder : InteractionBehaviourPlaceholder
    {
        [SerializeField, Tooltip("Translate, rotate and scale.")]
        private EManipulationMode manipulationMode = (EManipulationMode)~0;

        [SerializeField, Tooltip("Enables hand and ray interaction on this object")]
        private EVRInteraction vrInteraction = (EVRInteraction)~0;

        [SerializeField, Tooltip("A dynamic attach means that the object won't snap to the center of gravity")]
        private bool dynamicAttach;

        [SerializeField, Tooltip("Resets the rotation on one or more axes when the interaction ends (VR-only)")]
        private bool adjustRotationOnRelease;

        [SerializeField, Tooltip("Resets the rotation on the X axis")]
        private bool realignAxisX = true;

        [SerializeField, Tooltip("Resets the rotation on the Y axis")]
        private bool realignAxisY = false;

        [SerializeField, Tooltip("Resets the rotation on the Z axis")]
        private bool realignAxisZ = true;

        [SerializeField, Tooltip("How much time is needed to reset the rotation")]
        private float realignDurationTimeInSeconds = 1f;

        [SerializeField, Tooltip("If selected, the object will face the camera on mouse interaction")]
        private bool mouseLookAtCamera;

        [SerializeField, Tooltip("If specified, a dynamic attach will be generated on interaction")]
        private Transform attachTransform;

        [SerializeField, Tooltip("Event called when the manipulable state changes")]
        public UnityEvent<EManipulableState> onManipulationStateChange;

        [Header("WebGL Only")]
        [SerializeField, Tooltip("Whether or not to disable rotation and scale gizmos")]
        private bool gizmosDisabled;

        [SerializeField, Tooltip("The force necessary to combine x and y rotation")]
        private float threshold = 0.4f;

        [SerializeField]
        private bool freeRotation;

        [SerializeField]
        private bool lockXRotation;

        [SerializeField]
        private bool lockYRotation;

        [SerializeField]
        private bool isFocusedInteractable;

        [HideInInspector] public bool spriteDragMode = false;
        [HideInInspector] public Sprite spriteToDrag;

        public EManipulationMode ManipulationMode { get => manipulationMode; set => manipulationMode = value; }
        public EVRInteraction VrInteraction { get => vrInteraction; set => vrInteraction = value; }
        public bool DynamicAttach { get => dynamicAttach; set => dynamicAttach = value; }
        public bool AdjustRotationOnRelease { get => adjustRotationOnRelease; set => adjustRotationOnRelease = value; }
        public bool RealignAxisX { get => realignAxisX; set => realignAxisX = value; }
        public bool RealignAxisY { get => realignAxisY; set => realignAxisY = value; }
        public bool RealignAxisZ { get => realignAxisZ; set => realignAxisZ = value; }
        public float RealignDurationTimeInSeconds { get => realignDurationTimeInSeconds; set => realignDurationTimeInSeconds = value; }
        public bool MouseLookAtCamera { get => mouseLookAtCamera; set => mouseLookAtCamera = value; }
        public bool GizmosDisabled { get => gizmosDisabled; set => gizmosDisabled = value; }
        public float Threshold { get => threshold; set => threshold = value; }
        public bool FreeRotation { get => freeRotation; set => freeRotation = value; }
        public bool LockXRotation { get => lockXRotation; set => lockXRotation = value; }
        public bool LockYRotation { get => lockYRotation; set => lockYRotation = value; }
        public bool IsFocusedInteractable { get => isFocusedInteractable; set => isFocusedInteractable = value; }
        public Transform AttachTransform { get => attachTransform; set => attachTransform = value; }
        public UnityEvent<EManipulableState> OnCurrentStateChange { get => onManipulationStateChange; }
    }
}
