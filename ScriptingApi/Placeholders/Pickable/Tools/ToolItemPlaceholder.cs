using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
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


