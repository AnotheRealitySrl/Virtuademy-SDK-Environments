using UnityEditor;
using UnityEngine;
using Virtuademy.SDK.Environments;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    /// <summary>
    /// The SpawnableObject's inspector: its fields, the VR hand references, and (2026-10-29) the preview of how the item is
    /// held in third person on desktop, web and mobile, shown on the creator's request.
    /// </summary>
    [CustomEditor(typeof(SpawnableObjectPlaceholder))]
    public class SpawnableObjectPlaceholderEditor : UnityEditor.Editor
    {
        private ThirdPersonHoldPreview preview;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            SpawnableObjectPlaceholder placeholder = (SpawnableObjectPlaceholder)target;
            string leftButtonLabel = placeholder.LeftHandReference == null ? "Show Left Reference" : "Hide Left Reference";
            string rightButtonLabel = placeholder.RightHandReference == null ? "Show Right Reference" : "Hide Right Reference";

            placeholder.SetPositionLeftHand();

            if (GUILayout.Button(leftButtonLabel))
            {
                placeholder.ShowHandReference(placeholder.LeftHandReference, 0);
            }
            if (GUILayout.Button(rightButtonLabel))
            {
                placeholder.ShowHandReference(placeholder.RightHandReference, 1);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Third-person hold (desktop, web, mobile)", EditorStyles.boldLabel);
            bool shown = preview != null && preview.IsShown;
            if (GUILayout.Button(shown ? "Hide preview" : "Preview third-person hold"))
            {
                if (shown)
                {
                    HidePreview();
                }
                else
                {
                    preview = new ThirdPersonHoldPreview(placeholder);
                    preview.Show();
                }
            }
            else if (changed && shown)
            {
                preview.Refresh();
            }
        }

        private void OnDisable()
        {
            // Deselected, or the inspector closed: the mannequin goes
            HidePreview();
        }

        private void HidePreview()
        {
            preview?.Hide();
            preview = null;
        }
    }
}
