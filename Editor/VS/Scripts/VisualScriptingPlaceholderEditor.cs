using UnityEditor;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [CustomEditor(typeof(VisualScriptingInteractablePlaceholder))]
    public class VisualScriptingPlaceholderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VisualScriptingInteractablePlaceholder visualScriptingPlaceholder = target as VisualScriptingInteractablePlaceholder;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("vrVisualScriptingInteraction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionsScriptMachine"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("desktopAllowedStates"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vrAllowedStates"));

            serializedObject.ApplyModifiedProperties();

        }
    }
}
