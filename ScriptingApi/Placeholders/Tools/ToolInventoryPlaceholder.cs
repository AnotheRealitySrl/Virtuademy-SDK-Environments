using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static Virtuademy.Environments.ScriptingApi.Placeholders.ToolInventoryPlaceholder;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>A <see cref="UnityEvent"/> carrying a tool's item name.</summary>
    [Serializable]
    public class ToolNameEvent : UnityEvent<string> { }

    public class ToolInventoryPlaceholder : SpawnableHandlerPlaceholder
    {
        public enum EToolLayout
        {
            Radial = 0,
            Grid = 1,
        }

        /// <summary>How the held tool is shown on desktop and mobile. VR always shows the tool in the hand.</summary>
        public enum EHeldToolDisplay
        {
            /// <summary>The tool's icon follows the pointer (the behaviour before 2026.7).</summary>
            CursorIcon = 0,
            /// <summary>The tool's 3D model is shown in front of the camera, and the pointer stays visible.</summary>
            Model = 1,
            /// <summary>Both: the icon follows the pointer and the 3D model is shown in front of the camera.</summary>
            Both = 2,
        }

        [HideInInspector] public EToolLayout inventoryLayout = EToolLayout.Radial; //Hide for now --> Display in the future

        [Tooltip("The list of the items that are already inside the menu")]
        public List<ToolItemPlaceholder> inventoryItems; //list of the scriptable objects describing the inventory items

        [Tooltip("The distance offset of the radialMenu from the camera")]
        public float zOffset = 0.8f;

        [Tooltip("The distance offset of the radialMenu on the y axis")]
        public float yOffset = 0.15f;

        [Tooltip("The radius that the items are going to use when opening the radialMenu")]
        public float radius = 0.3f;

        [Tooltip("The speed with which the radialMenu will be opened")]
        public float openSpeed = 0.25f;

        [Tooltip("the input pressed in order to open and close the radialMenu")]
        public InputAction action;

        [Header("Input")]
        [Tooltip("Adds the platform's default inputs to the ones above: in VR the secondary button (B / Y) opens the wheel; "
                 + "on desktop Tab opens it, Q and E switch to the previous and next tool. "
                 + "The tool button at the bottom right opens it on desktop and mobile in any case.")]
        public bool useDefaultBindings = true;

        [Tooltip("Optional: switches to the next tool without opening the wheel.")]
        public InputAction nextToolAction;

        [Tooltip("Optional: switches to the previous tool without opening the wheel.")]
        public InputAction previousToolAction;

        [Header("Held tool on desktop and mobile")]
        [Tooltip("The default for every tool (a tool can override it with its Held Display). Cursor Icon: the tool's icon "
                 + "follows the pointer. Model: the tool's 3D model is shown in front of the camera, as if in hand. Both: the two together.")]
        public EHeldToolDisplay heldToolDisplay = EHeldToolDisplay.CursorIcon;

        [Tooltip("With Model: where the tool's grip point (its Hold Anchor, or the centre of its model) sits, "
                 + "in metres from the camera (x right, y up, z forward).")]
        public Vector3 heldToolOffset = new Vector3(0.22f, -0.2f, 0.55f);

        [Header("Events")]
        [Tooltip("The local player took a tool in hand, with its item name.")]
        public ToolNameEvent onToolEquipped = new();

        [Tooltip("The local player put a tool away, with its item name.")]
        public ToolNameEvent onToolUnequipped = new();

        [Tooltip("The local player used a tool, with its item name: a use started, or the tool was applied to an item trigger detector.")]
        public ToolNameEvent onToolUsed = new();

        [Tooltip("Whether or not you want to show the hand item in the inventory, used to remove items instead of clicking on the same item again")]
        [HideInInspector] public bool displayEmptyHand;
        [HideInInspector] public Sprite emptyHandSprite;
        [HideInInspector] public string emptyHandName;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ToolInventoryPlaceholder))]
    public class ToolPlaceholderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ToolInventoryPlaceholder inventoryPlaceholder = (ToolInventoryPlaceholder)target;
            if (inventoryPlaceholder.inventoryLayout == EToolLayout.Radial)
            {
                SerializedProperty displayEmptyHand = serializedObject.FindProperty("displayEmptyHand");
                EditorGUILayout.PropertyField(displayEmptyHand);

                if (inventoryPlaceholder.displayEmptyHand == true)
                {
                    SerializedProperty emptyHandSprite = serializedObject.FindProperty("emptyHandSprite");
                    EditorGUILayout.PropertyField(emptyHandSprite);
                    SerializedProperty emptyHandName = serializedObject.FindProperty("emptyHandName");
                    EditorGUILayout.PropertyField(emptyHandName);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
