using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A short text on a dark pill in the world, that can turn to face the user: the UI Toolkit
    /// successor of the SDK's uGUI <c>Hover_DescriptionCanvas</c>. Show and hide it from the hover
    /// events of an Interactable to label what the pointer is on.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy Label")]
    public class VirtuademyLabel : UIDocumentBinder, IScenePanel
    {
        [SerializeField] private VirtuademyPanel.LocalizedText text = new("Label");
        [SerializeField, Tooltip("Visible when the scene starts. Off: call Show().")] private bool visibleOnStart;
        [SerializeField, Tooltip("Turn every frame to face the camera (upright).")] private bool faceCamera = true;

        private Label label;
        private bool visible;
        private bool listening;

        private void Awake()
        {
            visible = visibleOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            label = root.Q<Label>("label");
            if (label == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyLabel)}] '{name}': the document has no Label named 'label'.", this);
                return false;
            }
            PanelEnvironment.IgnoreExceptControls(root);
            listening = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            WatchForRebuild(label);
            Refresh();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (listening)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
                listening = false;
            }
            label = null;
        }

        public void Show()
        {
            visible = true;
            Refresh();
        }

        public void Hide()
        {
            visible = false;
            Refresh();
        }

        /// <summary>Sets the text as written (no localization).</summary>
        public void SetText(string value)
        {
            text = new VirtuademyPanel.LocalizedText(value);
            Refresh();
        }

        void IScenePanel.SetTexts(string title, string value)
        {
            if (value != null)
            {
                SetText(value);
            }
        }

        private void LateUpdate()
        {
            if (!faceCamera || !visible)
            {
                return;
            }
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            Vector3 away = Vector3.ProjectOnPlane(transform.position - cam.transform.position, Vector3.up);
            if (away.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (label == null)
            {
                return;
            }
            label.text = text.Resolve();
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
