using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How the player uses a tool held in the hand.</summary>
    public enum EToolUseMode
    {
        /// <summary>The tool has no use: holding it is all it does (the behaviour before 2026.7).</summary>
        None = 0,
        /// <summary>Each press of the use key is one use.</summary>
        Click = 1,
        /// <summary>A use starts on press and ends on release (On Use Ended).</summary>
        Hold = 2,
    }

    /// <summary>Multiplayer: whose wheel a tool goes to when a player picks it up.</summary>
    public enum EToolPickupSharing
    {
        /// <summary>What the tool inventory says (its Picked Tools Shared).</summary>
        AsInventory = 0,
        /// <summary>Only the wheel of the player who picked it up.</summary>
        PickerOnly = 1,
        /// <summary>Every player's wheel, also players who join later.</summary>
        Everyone = 2,
    }

    /// <remarks>
    /// With a use mode, the tool in the player's hand is used with the platform's single use key
    /// (desktop left click or F, mobile tap or press, VR trigger of the holding hand). Each use spends
    /// one of the Pickable's Number Of Uses and raises the Pickable's On Item Used, and On Item Expired
    /// when the last one is spent; a Hold use also raises On Use Ended on release. The events fire for
    /// the local player only.
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

        [Tooltip("How the player uses the tool in the hand with the platform's use key. None: no use (as before 2026.7).")]
        public EToolUseMode useMode = EToolUseMode.None;

        [Tooltip("Hold only: the use key was released. The use itself raises the Pickable's On Item Used.")]
        public UnityEvent onUseEnded = new();

        [Tooltip("Desktop and mobile, first person: also show the tool's 3D model in front of the camera, as if in hand, "
                 + "while it is held; the Pickable's icon still follows the pointer when it has one. Off: icon only. In third "
                 + "person the model is in the avatar's right hand whatever this says.")]
        public bool showModelInHand = false;

        [Tooltip("Multiplayer: whose wheel the tool goes to when a player picks it up. As Inventory uses the tool inventory's setting.")]
        public EToolPickupSharing pickupSharing = EToolPickupSharing.AsInventory;
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


