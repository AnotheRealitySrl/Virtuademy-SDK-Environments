using Virtuademy.Environments.ScriptingApi.Placeholders;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class InventoryItemTriggerDetectorPlaceholder : SceneComponentPlaceholderBase
    {
        /// <summary>How the target is activated by a tool (2026.7).</summary>
        public enum EActivation
        {
            /// <summary>Bringing the right tool to it is enough (as before 2026.7).</summary>
            Contact = 0,
            /// <summary>The tool must touch it while it is used (VR: the trigger; a Hold tool keeps acting while held).</summary>
            Use = 1,
        }

        [Tooltip("How a tool activates this target. Contact: bringing the right tool to it is enough (on desktop and "
                 + "mobile: clicking it with the tool). Use: the tool must touch it while it is used (in VR the trigger; "
                 + "a Hold tool, like a spray, keeps acting while the trigger is held). On desktop and mobile the click "
                 + "with the tool is the use, in both cases")]
        public EActivation activation = EActivation.Contact;

        [Tooltip("The reference to the item that triggers the action when entering the collider")]
        public PickablePlaceholder inventoryItem;

        [Tooltip("Whether or not only a specific collider of the inventory item can trigger. Leave to false if all the colliders can trigger the detector")]
        public bool handleSpecificColliderPosition = false;
        [Tooltip("The position in the list of interacting colliders of the item placeholder's collider")]
        [HideInInspector] public int colliderTriggerIndex = -1;

        [Tooltip("The collider in the scene that is triggered when the inventory item enters its space. Need to have the interactable placeholder component")]
        public Collider[] detectorCollider;

        public Transform feedbackCheckPosition;

        public UnityEvent OnItemTriggerEnter;
        public UnityEvent OnWrongItemTriggerEnter;
        public UnityEvent OnItemTriggerExit;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InventoryItemTriggerDetectorPlaceholder))]
    public class InventoryItemTriggerDetectorPlaceholderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            // Draw the "consumable" field
            SerializedProperty specificColl = serializedObject.FindProperty("handleSpecificColliderPosition");

            // Conditionally show "numberOfUses"
            if (specificColl.boolValue)
            {
                SerializedProperty triggerIndex = serializedObject.FindProperty("colliderTriggerIndex");
                EditorGUILayout.PropertyField(triggerIndex);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
