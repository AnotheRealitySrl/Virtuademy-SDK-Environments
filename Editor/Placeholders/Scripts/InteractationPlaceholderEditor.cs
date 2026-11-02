using UnityEditor;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    [CustomEditor(typeof(InteractablePlaceholder))]
    public class InteractationPlaceholderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            InteractablePlaceholder interactablePlaceholder = target as InteractablePlaceholder;
            // IsNetworked is a property -> <IsNetworked>k__BackingField
            EditorGUILayout.PropertyField(serializedObject.FindProperty("<IsNetworked>k__BackingField"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("automaticSetup"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactWithColliders"));
            if (interactablePlaceholder.InteractWithColliders)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionColliders"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("vrControllers"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lockHoverDuringInteraction"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
