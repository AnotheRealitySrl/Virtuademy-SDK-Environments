#if UNITY_EDITOR
using Virtuademy.Environments.ScriptingApi.Placeholders.Editor;

using UnityEditor;

using UnityEngine;


using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [CustomEditor(typeof(SyncedObject))]
    public class SyncedObjectEditor : NetworkPlaceholderEditor
    {
        private UnityEditor.Editor _variablesEditor;
        private SerializedProperty _syncTransformProp;
        private GameObject _targetGameObject;

        private void InitializePropertiesIfNecessary()
        {
            if (_syncTransformProp != null)
            {
                return;
            }

            _syncTransformProp = serializedObject.FindProperty("syncTransform");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            DrawFields();

            serializedObject.ApplyModifiedProperties();

        }

        private void OnEnable()
        {
            if (target != null)
            {
                _targetGameObject = (target as SyncedObject).gameObject;
            }
            else
            {
                _targetGameObject = null;
            }
        }

        private void OnDisable()
        {
            // when target is null here, it's been destroyed
            if (target == null)
            {
                if (_targetGameObject != null)
                {
                    // Check target object really doesn't have a SpatialSyncedObject
                    if (_targetGameObject.TryGetComponent<SyncedObject>(out SyncedObject obj))
                    {
                        return;
                    }
                    // Delete any hidden SpatialSyncedVariables components when synced object component is deleted in the editor
                    if (_targetGameObject.TryGetComponent<SyncedVariables>(out SyncedVariables variables))
                    {
                        DestroyImmediate(variables);
                    }
                }
            }
        }

        public virtual void DrawFields()
        {
            InitializePropertiesIfNecessary();
            SyncedObject syncedObject = target as SyncedObject;

            GUILayout.Space(8);
            EditorGUILayout.PropertyField(_syncTransformProp);

            GUI.enabled = true;

            GUILayout.Space(8);

            //Embed the synced variables inspector
            if (syncedObject.TryGetComponent(out SyncedVariables syncedVariables))
            {
                GUIStyle boldStyle = new(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold
                };

                GUILayout.Label("List of Variables", boldStyle);

                if (_variablesEditor == null || _variablesEditor.target != syncedVariables)
                {
                    _variablesEditor = CreateEditor(syncedVariables);
                }

                _variablesEditor.OnInspectorGUI();
            }
            else
            {
                //No synced Variables
                if (GUILayout.Button("Add Synced Variables", new GUILayoutOption[] { GUILayout.Height(32) }))
                {
                    syncedObject.gameObject.AddComponent<SyncedVariables>();
                }
            }
        }
    }
}
#endif