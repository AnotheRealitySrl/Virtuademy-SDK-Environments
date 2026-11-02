using UnityEngine;
using UnityEngine.Animations;
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
        [SerializeField, Tooltip("Turn to face the camera (upright), by an AimConstraint on it.")] private bool faceCamera = true;

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
            Face();
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

        protected override void OnEnable()
        {
            base.OnEnable();
            Face();
        }

        // Facing the camera, upright (its back to the camera, as a world-space document is read): an AimConstraint
        // turning it about the world's up axis only, set on the current main camera when it is enabled or shown. The
        // engine turns it; no code of ours runs per frame (2026-10-29, review B2).
        private void Face()
        {
            AimConstraint aim = GetComponent<AimConstraint>();
            Camera cam = Camera.main;
            if (!faceCamera || cam == null)
            {
                if (aim != null)
                {
                    aim.constraintActive = false;
                }
                return;
            }
            if (aim == null)
            {
                aim = gameObject.AddComponent<AimConstraint>();
            }
            while (aim.sourceCount > 0)
            {
                aim.RemoveSource(0);
            }
            aim.AddSource(new ConstraintSource { sourceTransform = cam.transform, weight = 1f });
            aim.aimVector = Vector3.back;
            aim.upVector = Vector3.up;
            aim.worldUpType = AimConstraint.WorldUpType.SceneUp;
            aim.rotationAxis = Axis.Y;
            aim.rotationAtRest = transform.localEulerAngles;
            aim.locked = true;
            aim.constraintActive = true;
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
