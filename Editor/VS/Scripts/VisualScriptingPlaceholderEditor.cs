using UnityEditor;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [CustomEditor(typeof(VisualScriptingInteractablePlaceholder))]
    public class VisualScriptingPlaceholderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VisualScriptingInteractablePlaceholder visualScriptingPlaceholder = target as VisualScriptingInteractablePlaceholder;

            // The VR controllers an object reacts to are set on its Interactable placeholder.
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionsScriptMachine"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("desktopAllowedStates"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vrAllowedStates"));

            serializedObject.ApplyModifiedProperties();

        }
    }
}
