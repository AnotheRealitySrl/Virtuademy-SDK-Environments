#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using static Virtuademy.Environments.ScriptingApi.Interaction.IManipulable;


namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(InteractablePlaceholder), typeof(ManipulablePlaceholder))]
    public class PickablePlaceholder : SceneComponentPlaceholderNetwork
    {
        [Tooltip("The name of the item")]
        public string itemName; //Name of the item

        [Tooltip("Number of times the item can be used before being lost, if -1 then it is infinite")]
        public int numberOfUses = -1;

        [Tooltip("The icon used to display the item in the menu")]
        public Sprite icon; //The icon to display in the inventory

        [Tooltip("The colliders that have interaction logic (example.: Trigger detector colliders)")]
        public Collider[] interactingColliders;

        [SerializeField] public bool transformable;
        [SerializeField] public PickablePlaceholder transformIntoPickable;

        public bool destroyOnUse = false;

        public UnityEvent onItemUsed;
        public UnityEvent onItemExpired;
        public UnityEvent onItemAddedToInventory;


        public virtual string AddressableKey => itemName;

        private void Awake()
        {
            ManipulablePlaceholder manipulablePlaceholder = GetComponent<ManipulablePlaceholder>();
            if (manipulablePlaceholder == null)
            {
                manipulablePlaceholder = gameObject.AddComponent<ManipulablePlaceholder>();
            }

            manipulablePlaceholder.VrInteraction = EVRInteraction.Hands;
            manipulablePlaceholder.ManipulationMode = EManipulationMode.Translate | EManipulationMode.Rotate;
            manipulablePlaceholder.ManipulationMode &= ~EManipulationMode.Scale;
            manipulablePlaceholder.DynamicAttach = false;
            manipulablePlaceholder.AdjustRotationOnRelease = false;
            manipulablePlaceholder.MouseLookAtCamera = false;
            manipulablePlaceholder.GizmosDisabled = true;
            manipulablePlaceholder.IsFocusedInteractable = false;
            manipulablePlaceholder.AttachTransform = null;
            manipulablePlaceholder.spriteDragMode = true;
            manipulablePlaceholder.spriteToDrag = icon;
        }

#if UNITY_EDITOR

        private void OnValidate()
        {
            ManipulablePlaceholder manipulablePlaceholder = GetComponent<ManipulablePlaceholder>();
            if (manipulablePlaceholder == null)
            {
                manipulablePlaceholder = gameObject.AddComponent<ManipulablePlaceholder>();
            }

            manipulablePlaceholder.VrInteraction = EVRInteraction.Hands;
            manipulablePlaceholder.ManipulationMode = EManipulationMode.Translate | EManipulationMode.Rotate;
            manipulablePlaceholder.ManipulationMode &= ~EManipulationMode.Scale;
            manipulablePlaceholder.DynamicAttach = false;
            manipulablePlaceholder.AdjustRotationOnRelease = false;
            manipulablePlaceholder.MouseLookAtCamera = false;
            manipulablePlaceholder.GizmosDisabled = true;
            manipulablePlaceholder.IsFocusedInteractable = false;
            manipulablePlaceholder.AttachTransform = null;
            manipulablePlaceholder.spriteDragMode = true;
            manipulablePlaceholder.spriteToDrag = icon;
        }


        [CustomEditor(typeof(PickablePlaceholder), true)]
        public class PickablePlaceholderEditor : Editor
        {
            SerializedProperty transformableProp;
            SerializedProperty transformIntoProp;

            void OnEnable()
            {
                // You must link the properties here first
                transformableProp = serializedObject.FindProperty("transformable");
                transformIntoProp = serializedObject.FindProperty("transformIntoPickable");
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                //DrawDefaultInspector();
                DrawPropertiesExcluding(serializedObject, "m_Script", "transformable", "transformIntoPickable", "IsNetworked", "<IsNetworked>k__BackingField");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Transformation Settings", EditorStyles.boldLabel);

                // 2. Draw your custom conditional logic
                EditorGUILayout.PropertyField(transformableProp);

                if (transformableProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(transformIntoProp);
                    EditorGUI.indentLevel--;
                }


                SerializedProperty networked = serializedObject.FindProperty("<IsNetworked>k__BackingField");
                networked.boolValue = true;

                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}
