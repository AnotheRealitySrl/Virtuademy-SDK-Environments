using System;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How the player uses a held tool.</summary>
    public enum EToolUseMode
    {
        /// <summary>The tool has no use action: holding it is all it does (the behaviour before 2026.7).</summary>
        None = 0,
        /// <summary>Each press is one use: started and ended fire together.</summary>
        Click = 1,
        /// <summary>A use lasts while the action is held: started, held every frame, ended on release.</summary>
        Hold = 2,
    }

    /// <summary>How the held tool is shown on desktop and mobile. VR always shows the tool itself.</summary>
    public enum EToolHeldDisplay
    {
        /// <summary>What the tool inventory sets for every tool (its Held Tool Display).</summary>
        AsInventory = 0,
        /// <summary>The tool's icon follows the pointer.</summary>
        CursorIcon = 1,
        /// <summary>The tool's 3D model is shown in front of the camera, as if in hand.</summary>
        Model = 2,
        /// <summary>Both: the icon follows the pointer and the 3D model is in front of the camera.</summary>
        Both = 3,
    }

    /// <summary>A <see cref="UnityEvent"/> carrying the seconds since the previous frame of a held use.</summary>
    [Serializable]
    public class ToolUseHeldEvent : UnityEvent<float> { }

    /// <summary>A <see cref="UnityEvent"/> carrying the object a tool was used on.</summary>
    [Serializable]
    public class ToolTargetEvent : UnityEvent<GameObject> { }

    /// <summary>
    /// A tool of the tool inventory. The object it sits on is the tool the player sees; its
    /// <see cref="PickablePlaceholder"/> gives the name, icon and number of uses.
    /// </summary>
    /// <remarks>
    /// The events fire only for the local player, on this scene object, whatever copy of the tool
    /// the application shows in the player's hand. The application raises them: the
    /// <c>Notify*</c> methods are not meant to be called by an environment.
    /// </remarks>
    [RequireComponent(typeof(PickablePlaceholder))]
    public class ToolItemPlaceholder : SceneComponentPlaceholderNetwork
    {
        private PickablePlaceholder _pickablePlaceholder;
        public PickablePlaceholder PickablePlaceholder
        {
            get
            {
                if (_pickablePlaceholder == null)
                    _pickablePlaceholder = GetComponent<PickablePlaceholder>();
                return _pickablePlaceholder;
            }
        }

        [Header("Use")]
        [Tooltip("How the player uses the tool while holding it. None keeps the tool passive.")]
        public EToolUseMode useMode = EToolUseMode.None;

        [Tooltip("The input that uses the tool. Leave it without bindings for the platform default: "
                 + "the trigger of the holding hand in VR, the left mouse button or F on desktop, a tap on mobile.")]
        public InputAction useAction;

        [Tooltip("How far, in metres, the aim ray looks for the object the tool is used on.")]
        [Min(0f)]
        public float useRange = 3f;

        [Tooltip("The layers the aim ray can hit.")]
        public LayerMask useLayers = ~0;

        [Header("Hold")]
        [Tooltip("Optional. A child of this object marking where the right hand holds the tool: in VR the tool appears "
                 + "with this point at the hand, ready to be grabbed; on desktop and mobile it is the grip point of the 3D view. "
                 + "Empty keeps the hand pivots of the spawnable object.")]
        public Transform holdAnchor;

        [Tooltip("Optional. The same for the left hand (left-handed players in VR). Empty uses Hold Anchor, "
                 + "then the left hand pivot of the spawnable object.")]
        public Transform holdAnchorLeft;

        [Tooltip("How the held tool is shown on desktop and mobile. As Inventory uses the tool inventory's setting.")]
        public EToolHeldDisplay heldDisplay = EToolHeldDisplay.AsInventory;

        [Header("Events")]
        [Tooltip("The player took the tool in hand.")]
        public UnityEvent onEquipped = new();

        [Tooltip("The player put the tool away, or switched to another one.")]
        public UnityEvent onUnequipped = new();

        [Tooltip("A use started. With Click it is followed at once by On Use Ended.")]
        public UnityEvent onUseStarted = new();

        [Tooltip("Every frame of a Hold use, with the seconds since the previous frame.")]
        public ToolUseHeldEvent onUseHeld = new();

        [Tooltip("A use ended.")]
        public UnityEvent onUseEnded = new();

        [Tooltip("A use started while the aim ray hit an object within Use Range.")]
        public ToolTargetEvent onUsedOnTarget = new();

        [Tooltip("The last of the tool's uses was spent (Number Of Uses on the Pickable).")]
        public UnityEvent onUsesFinished = new();

        /// <summary>Whether the local player is holding this tool.</summary>
        public bool IsEquipped { get; private set; }

        /// <summary>Whether a use is in progress (always false between uses of a Click tool).</summary>
        public bool IsInUse { get; private set; }

        /// <summary>
        /// What the aim ray hit when the current or last use started or was last held, or null.
        /// </summary>
        public GameObject AimTarget { get; private set; }

        /// <summary>Uses left; -1 means unlimited.</summary>
        public int RemainingUses { get; private set; } = -1;

        /// <summary>Called by the application.</summary>
        public void NotifyEquipped(int remainingUses)
        {
            IsEquipped = true;
            RemainingUses = remainingUses;
            onEquipped?.Invoke();
        }

        /// <summary>Called by the application.</summary>
        public void NotifyUnequipped()
        {
            IsEquipped = false;
            IsInUse = false;
            onUnequipped?.Invoke();
        }

        /// <summary>Called by the application.</summary>
        public void NotifyUseStarted(GameObject target, int remainingUses)
        {
            IsInUse = true;
            AimTarget = target;
            RemainingUses = remainingUses;
            onUseStarted?.Invoke();
            if (target != null)
                onUsedOnTarget?.Invoke(target);
        }

        /// <summary>Called by the application.</summary>
        public void NotifyUseHeld(float deltaTime, GameObject target)
        {
            AimTarget = target;
            onUseHeld?.Invoke(deltaTime);
        }

        /// <summary>Called by the application.</summary>
        public void NotifyUseEnded()
        {
            IsInUse = false;
            onUseEnded?.Invoke();
        }

        /// <summary>Called by the application.</summary>
        public void NotifyUsesFinished()
        {
            RemainingUses = 0;
            onUsesFinished?.Invoke();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ToolItemPlaceholder))]
    public class ToolItemPlaceholderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            //DrawDefaultInspector();
            DrawPropertiesExcluding(serializedObject, "IsNetworked", "<IsNetworked>k__BackingField");
            SerializedProperty networked = serializedObject.FindProperty("<IsNetworked>k__BackingField");

            networked.boolValue = true;

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
