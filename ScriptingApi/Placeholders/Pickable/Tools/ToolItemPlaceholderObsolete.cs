using Virtuademy.Environments.ScriptingApi.Placeholders;
using UnityEditor;
using UnityEngine;


namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(PickablePlaceholder))]
    public class ToolItemPlaceholderObsolete : PickablePlaceholder, IAddressablePlaceholder
    {

        //[Tooltip("Whether or not the item is a consumable or an infinite item")]
        //public bool consumable; //Whether or not the item is a consumable or an infinite item
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ToolItemPlaceholderObsolete))]
    public class ToolItemPlaceholderObsoleteEditor : Editor
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
