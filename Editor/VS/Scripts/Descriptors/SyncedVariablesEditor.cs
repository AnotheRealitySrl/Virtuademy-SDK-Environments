#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [CustomEditor(typeof(SyncedVariables))]
    public class SyncedVariablesEditor : UnityEditor.Editor
    {
        void OnEnable()
        {
            var variables = target as SyncedVariables;
            if (variables != null)
            {
                variables.hideFlags = HideFlags.HideInInspector;
            }
        }

        public override void OnInspectorGUI()
        {
            var syncedVariables = target as SyncedVariables;
            serializedObject.Update();

            GUILayout.Space(8);

            serializedObject.ApplyModifiedProperties();

            if (syncedVariables.TryGetComponent(out Variables variables))
            {
                bool hasSomeVariables = false;
                foreach (VariableDeclaration variable in variables.declarations)
                {
                    if (variable.typeHandle.Identification == null)
                    {
                        continue;
                    }
                    Type variableType = Type.GetType(variable.typeHandle.Identification);
                    if (TypeIsSyncable(variableType))
                    {
                        hasSomeVariables = true;
                        SyncedVariables.Data syncedVariableData = syncedVariables.variableSettings.Find(x => x.name == variable.name);
                        bool isSynced = syncedVariableData != null;

                        EditorGUILayout.BeginHorizontal();

                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Button(variableType.HumanName(), new GUILayoutOption[] { GUILayout.Width(100) });
                        EditorGUILayout.TextField(variable.name, new GUILayoutOption[] { GUILayout.MinWidth(60) });
                        EditorGUI.EndDisabledGroup();
                        bool newIsSynced = isSynced;

                        if (GUILayout.Button(isSynced ? "Synced" : "Not Synced", new GUILayoutOption[] { GUILayout.Width(100) }))
                        {
                            newIsSynced = !isSynced;
                        }

                        EditorGUILayout.EndHorizontal();

                        if (newIsSynced && !isSynced)
                        {
                            Undo.RecordObject(syncedVariables, $"Sync {variable.name}");
                            syncedVariables.variableSettings.Add(new SyncedVariables.Data()
                            {
                                name = variable.name,
                                declaration = variable,
                            });
                            UnityEditor.EditorUtility.SetDirty(syncedVariables);
                        }

                        if (!newIsSynced && isSynced)
                        {
                            Undo.RecordObject(syncedVariables, $"Don't Sync {variable.name}");
                            syncedVariables.variableSettings.Remove(syncedVariableData);
                            UnityEditor.EditorUtility.SetDirty(syncedVariables);
                        }
                    }
                }

                if (!hasSomeVariables)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("(No valid variables defined.)");
                }

                // search through VariableSettings for any that are no longer valid
                List<SyncedVariables.Data> toRemove = new List<SyncedVariables.Data>();
                foreach (SyncedVariables.Data variableSetting in syncedVariables.variableSettings)
                {
                    if (string.IsNullOrEmpty(variableSetting.name) || !variables.declarations.IsDefined(variableSetting.name))
                    {
                        toRemove.Add(variableSetting);
                        continue;
                    }
                    if (!TypeIsSyncable(Type.GetType(variables.declarations.GetDeclaration(variableSetting.name).typeHandle.Identification)))
                    {
                        toRemove.Add(variableSetting);
                    }
                }

                if (toRemove.Count > 0)
                {
                    GUILayout.Space(8);

                    GUILayout.Space(4);
                    foreach (SyncedVariables.Data variableSetting in toRemove)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Button("INVALID", new GUILayoutOption[] { GUILayout.Width(100) });
                        EditorGUILayout.TextField(variableSetting.name);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();
                    }
                    GUILayout.Space(8);
                    if (GUILayout.Button("Clear Invalid Variables"))
                    {
                        foreach (SyncedVariables.Data remove in toRemove)
                        {
                            syncedVariables.variableSettings.Remove(remove);
                        }
                    }
                }
            }
        }

        private bool TypeIsSyncable(Type type)
        {
            if (
                type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(char) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(string) ||
                type == typeof(Vector2) ||
                type == typeof(Vector3))
            {
                return true;
            }
            return false;
        }
    }
}
#endif