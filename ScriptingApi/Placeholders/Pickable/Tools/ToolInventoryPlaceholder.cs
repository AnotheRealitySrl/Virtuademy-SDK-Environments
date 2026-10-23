using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using static Virtuademy.Environments.ScriptingApi.Placeholders.ToolInventoryPlaceholder;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ToolInventoryPlaceholder : SpawnableHandlerPlaceholder
    {
        public enum EToolLayout
        {
            Radial = 0,
            Grid = 1,
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
