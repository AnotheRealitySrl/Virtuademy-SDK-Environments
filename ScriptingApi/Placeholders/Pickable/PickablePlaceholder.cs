#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using static Virtuademy.Environments.ScriptingApi.Interaction.IManipulable;


namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How a pickable is dragged on desktop, web and mobile before it is picked up.</summary>
    public enum EPickableDragMode
    {
        /// <summary>The item becomes its icon while dragged; released, it goes back where it was, unless it is used on a
        /// target or dropped on the inventory (the behaviour before 2026.7).</summary>
        Icon = 0,

        /// <summary>The 3D object itself is moved, as a manipulable.</summary>
        Model = 1,
    }

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

        [Tooltip("Desktop, web and mobile: dragged as its icon (it goes back where it was when released, unless it is used "
                 + "on a target or dropped on the inventory), or as its 3D object.")]
        public EPickableDragMode dragMode = EPickableDragMode.Icon;

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
            manipulablePlaceholder.spriteDragMode = dragMode == EPickableDragMode.Icon;
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
            manipulablePlaceholder.spriteDragMode = dragMode == EPickableDragMode.Icon;
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
