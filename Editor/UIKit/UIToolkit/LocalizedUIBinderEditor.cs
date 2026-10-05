// Non-I2 inspector for LocalizedUIBinder. Compiled only when I2Loc is NOT present; the value of each
// localized key is edited as a plain string. When I2Loc is present this file is excluded and
// LocalizedUIBinderEditorI2 (in the I2Loc editor assembly) draws the term dropdown instead.
#if !I2LOC
using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities.Editor
{
    [CustomEditor(typeof(LocalizedUIBinder))]
    public class LocalizedUIBinderEditor : UnityEditor.Editor
    {
        private SerializedProperty documentProp;
        private SerializedProperty bindingsProp;
        private int cachedAssetId = -1;
        private int unnamedCount;

        private void OnEnable()
        {
            documentProp = serializedObject.FindProperty("document");
            bindingsProp = serializedObject.FindProperty("bindings");
            Rescan();
            cachedAssetId = CurrentAssetId();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            int assetId = CurrentAssetId();
            if (assetId != cachedAssetId)
            {
                Rescan();
                cachedAssetId = assetId;
            }

            EditorGUILayout.PropertyField(documentProp);
            EditorGUILayout.Space();

            VisualTreeAsset vta = LocalizedUIBinderEditorCore.GetVisualTreeAsset(documentProp, target);
            if (vta == null)
            {
                EditorGUILayout.HelpBox("Assegna un UIDocument con un Source Asset per elencare gli " +
                    "elementi localized.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("I2Loc non presente: inserisci direttamente la chiave/stringa. " +
                    "Con I2 installato qui comparirà il menu a tendina dei termini.", MessageType.None);
            }

            if (unnamedCount > 0)
            {
                EditorGUILayout.HelpBox($"{unnamedCount} elemento/i localized nella UXML non hanno un " +
                    "'name' e sono stati ignorati. Assegna un name per poterli gestire qui.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Elementi localized", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Ricarica dalla UXML", GUILayout.Width(150)))
                {
                    Rescan();
                    cachedAssetId = assetId;
                }
            }
            EditorGUILayout.Space();

            LocalizedUIBinderEditorCore.DrawBindings(bindingsProp, (label, valueProp) =>
                EditorGUILayout.PropertyField(valueProp, label));

            serializedObject.ApplyModifiedProperties();
        }

        private int CurrentAssetId()
        {
            VisualTreeAsset vta = LocalizedUIBinderEditorCore.GetVisualTreeAsset(documentProp, target);
            return vta != null ? vta.GetInstanceID() : 0;
        }

        private void Rescan()
        {
            serializedObject.Update();
            unnamedCount = 0;
            VisualTreeAsset vta = LocalizedUIBinderEditorCore.GetVisualTreeAsset(documentProp, target);
            if (vta == null)
            {
                return;
            }
            var scanned = LocalizedUIBinderEditorCore.Scan(vta, out unnamedCount);
            if (LocalizedUIBinderEditorCore.NeedsSync(bindingsProp, scanned))
            {
                LocalizedUIBinderEditorCore.Sync(bindingsProp, scanned);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif
