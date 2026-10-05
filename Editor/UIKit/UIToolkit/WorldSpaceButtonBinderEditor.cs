using System.Collections.Generic;
using System.Linq;

using Virtuademy.SDK.Environments.Utilities;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="WorldSpaceButtonBinder"/>. Instead of asking the author to type
    /// the UXML button names by hand, it reads the <see cref="UIDocument"/>'s Source Asset, finds every
    /// UI Toolkit <see cref="Button"/> that has a <c>name</c> and keeps the binding list in sync with it:
    /// one entry per button appears automatically, and the author only has to wire up the <c>onClick</c>.
    ///
    /// Existing <c>onClick</c> events are preserved across a resync (matched by name and moved, never
    /// recreated). Bindings whose name no longer exists in the UXML are kept only while they still have
    /// callbacks wired (so no work is silently lost) and are flagged as orphans; the empty placeholder
    /// entries are cleaned up on their own.
    /// </summary>
    [CustomEditor(typeof(WorldSpaceButtonBinder))]
    public class WorldSpaceButtonBinderEditor : UnityEditor.Editor
    {
        private SerializedProperty documentProp;
        private SerializedProperty buttonsProp;

        private readonly List<string> uxmlButtonNames = new();
        private int cachedAssetId = -1;

        private void OnEnable()
        {
            documentProp = serializedObject.FindProperty("document");
            buttonsProp = serializedObject.FindProperty("buttons");
            RefreshButtonNames();
            SyncBindings();
            cachedAssetId = GetVisualTreeAssetId();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Re-read and resync automatically when a UIDocument / Source Asset is (re)assigned.
            int assetId = GetVisualTreeAssetId();
            if (assetId != cachedAssetId)
            {
                RefreshButtonNames();
                SyncBindings();
                cachedAssetId = assetId;
            }

            EditorGUILayout.PropertyField(documentProp);
            EditorGUILayout.Space();

            VisualTreeAsset vta = GetVisualTreeAsset();
            if (vta == null)
            {
                EditorGUILayout.HelpBox(
                    "Nessuna UXML trovata. Assegna un UIDocument con un Source Asset per popolare i bottoni " +
                    "automaticamente.", MessageType.Warning);
            }
            else if (uxmlButtonNames.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "La UXML non contiene Button con un 'name'. Dai un name a ogni Button nella UXML per " +
                    "poterlo esporre qui.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Bottoni trovati nella UXML: {uxmlButtonNames.Count}",
                    EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Ricarica dalla UXML", GUILayout.Width(150)))
                {
                    RefreshButtonNames();
                    SyncBindings();
                    cachedAssetId = GetVisualTreeAssetId();
                }
            }

            EditorGUILayout.Space();
            DrawBindings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBindings()
        {
            if (buttonsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Nessun bottone da mostrare.", MessageType.Info);
                return;
            }

            for (int i = 0; i < buttonsProp.arraySize; i++)
            {
                SerializedProperty element = buttonsProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = element.FindPropertyRelative("buttonName");
                SerializedProperty clickProp = element.FindPropertyRelative("onClick");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawNameDropdown(nameProp);
                    EditorGUILayout.PropertyField(clickProp);
                }
            }
        }

        private void DrawNameDropdown(SerializedProperty nameProp)
        {
            string current = nameProp.stringValue;
            int index = uxmlButtonNames.IndexOf(current);
            bool orphan = index < 0;

            List<string> options = new(uxmlButtonNames);
            if (orphan)
            {
                options.Add(string.IsNullOrEmpty(current) ? "<nessuno>" : $"{current}  (non nella UXML)");
                index = options.Count - 1;
            }

            using (EditorGUI.ChangeCheckScope check = new())
            {
                int newIndex = EditorGUILayout.Popup(new GUIContent("Button"), index,
                    options.Select(o => new GUIContent(o)).ToArray());
                if (check.changed && newIndex < uxmlButtonNames.Count)
                {
                    nameProp.stringValue = uxmlButtonNames[newIndex];
                }
            }

            if (orphan)
            {
                EditorGUILayout.HelpBox($"'{current}' non esiste nella UXML corrente.", MessageType.Warning);
            }
        }

        /// <summary>
        /// Reconciles the serialized binding list with the buttons found in the UXML: every UXML button
        /// (in UXML order) is placed at the front, reusing the existing entry when the name already
        /// exists so its <c>onClick</c> is preserved. Empty leftover entries are removed; leftover
        /// entries that still have callbacks are kept as orphans.
        /// </summary>
        private void SyncBindings()
        {
            // Never wipe anything if the UXML could not be read (e.g. document not assigned yet).
            if (uxmlButtonNames.Count == 0)
            {
                return;
            }

            serializedObject.Update();

            for (int target = 0; target < uxmlButtonNames.Count; target++)
            {
                string wanted = uxmlButtonNames[target];
                int found = IndexOfBinding(wanted, target);
                if (found >= 0)
                {
                    if (found != target)
                    {
                        buttonsProp.MoveArrayElement(found, target);
                    }
                }
                else
                {
                    buttonsProp.InsertArrayElementAtIndex(target);
                    SerializedProperty element = buttonsProp.GetArrayElementAtIndex(target);
                    element.FindPropertyRelative("buttonName").stringValue = wanted;
                    ClearPersistentCalls(element);
                }
            }

            // Drop trailing placeholder/orphan entries that carry no wired callbacks.
            for (int i = buttonsProp.arraySize - 1; i >= uxmlButtonNames.Count; i--)
            {
                SerializedProperty element = buttonsProp.GetArrayElementAtIndex(i);
                SerializedProperty calls = element.FindPropertyRelative("onClick.m_PersistentCalls.m_Calls");
                if (calls == null || calls.arraySize == 0)
                {
                    buttonsProp.DeleteArrayElementAtIndex(i);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private int IndexOfBinding(string buttonName, int startInclusive)
        {
            for (int i = startInclusive; i < buttonsProp.arraySize; i++)
            {
                if (buttonsProp.GetArrayElementAtIndex(i).FindPropertyRelative("buttonName").stringValue == buttonName)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ClearPersistentCalls(SerializedProperty element)
        {
            SerializedProperty calls = element.FindPropertyRelative("onClick.m_PersistentCalls.m_Calls");
            if (calls != null)
            {
                calls.arraySize = 0;
            }
        }

        private void RefreshButtonNames()
        {
            uxmlButtonNames.Clear();
            VisualTreeAsset vta = GetVisualTreeAsset();
            if (vta == null)
            {
                return;
            }

            // Clone the tree off-screen so we can query it without a live panel.
            VisualElement root = vta.Instantiate();
            root.Query<Button>().ForEach(b =>
            {
                if (!string.IsNullOrEmpty(b.name) && !uxmlButtonNames.Contains(b.name))
                {
                    uxmlButtonNames.Add(b.name);
                }
            });
        }

        private VisualTreeAsset GetVisualTreeAsset()
        {
            UIDocument doc = documentProp.objectReferenceValue as UIDocument;
            if (doc == null)
            {
                doc = ((WorldSpaceButtonBinder)target).GetComponentInChildren<UIDocument>(true);
            }
            return doc != null ? doc.visualTreeAsset : null;
        }

        private int GetVisualTreeAssetId()
        {
            VisualTreeAsset vta = GetVisualTreeAsset();
            return vta != null ? vta.GetInstanceID() : 0;
        }
    }
}
