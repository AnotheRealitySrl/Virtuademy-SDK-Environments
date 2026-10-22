using System;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.Environments.ScriptingApi.UI;
using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A themed panel with an optional icon, a title, a text and up to two buttons: the UI Toolkit
    /// successor of the SDK's uGUI description, warning, wrong-answer, welcome and confirm canvases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Variant"/> (<see cref="EPanelVariant"/>) picks the role colour (accent bar and icon): <c>Default</c> for a
    /// description or a welcome, <c>Hint</c>, <c>Warning</c>, <c>Error</c> for a wrong answer and
    /// <c>Success</c> for a right one. Every text has a localization key and a fallback written in
    /// the environment's main language; with a key the panel shows its translation and follows
    /// language changes.
    /// </para>
    /// <para>
    /// The panel lives in the scene (rule 1 of the SDK): place the prefab, write its texts, wire its
    /// events. Scripts and graphs drive it through the public methods; nothing in it is the logic of
    /// the step it belongs to.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy Panel")]
    public class VirtuademyPanel : UIDocumentBinder, IScenePanel
    {
        [Serializable]
        public struct LocalizedText
        {
            [Tooltip("Key in the environment's Localization File. Empty: the text is shown as written.")]
            public string key;

            [TextArea(1, 6), Tooltip("Shown when there is no key, or the Localization File has no translation for it.")]
            public string text;

            public LocalizedText(string text, string key = "")
            {
                this.key = key;
                this.text = text;
            }

            public string Resolve() => PanelEnvironment.Text(key, text);
        }

        private static readonly string[] variantClasses =
        {
            null, "vd-panel--hint", "vd-panel--warning", "vd-panel--error", "vd-panel--success",
        };

        private static readonly string[] variantGlyphs = { "i", "?", "!", "!", "" };

        [SerializeField] private EPanelVariant variant = EPanelVariant.Default;
        [SerializeField, Tooltip("Show the round role icon left of the title.")] private bool showIcon;
        [SerializeField] private LocalizedText title = new("Title");
        [SerializeField] private LocalizedText body = new("Text");

        [SerializeField, Tooltip("Primary button (right, action colour). Empty text and key: no button.")]
        private LocalizedText primaryButton = new("Continue");

        [SerializeField, Tooltip("Secondary button (left, outlined). Empty text and key: no button.")]
        private LocalizedText secondaryButton = new(string.Empty);

        [SerializeField, Tooltip("Visible when the scene starts. Off: call Show().")]
        private bool visibleOnStart = true;

        [SerializeField, Tooltip("Hide the panel when either button is pressed.")]
        private bool hideOnButton = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onShown = new();
        [SerializeField] private UnityEvent onHidden = new();
        [SerializeField, Tooltip("The primary button was pressed (Continue, Confirm, Start).")]
        private UnityEvent onPrimary = new();
        [SerializeField, Tooltip("The secondary button was pressed (Back, Cancel).")]
        private UnityEvent onSecondary = new();

        private VisualElement panel;
        private VisualElement icon;
        private Label iconGlyph;
        private Label titleLabel;
        private Label bodyLabel;
        private Button primary;
        private Button secondary;
        private bool visible;
        private bool listening;

        public UnityEvent OnShown => onShown;
        public UnityEvent OnHidden => onHidden;
        public UnityEvent OnPrimary => onPrimary;
        public UnityEvent OnSecondary => onSecondary;

        /// <summary>Whether the panel is shown.</summary>
        public bool IsVisible => visible;

        public EPanelVariant Variant
        {
            get => variant;
            set
            {
                variant = value;
                Refresh();
            }
        }

        private void Awake()
        {
            visible = visibleOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            panel = root.Q<VisualElement>("panel");
            if (panel == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyPanel)}] '{name}': the document has no element named 'panel'.", this);
                return false;
            }
            icon = root.Q<VisualElement>("icon");
            iconGlyph = root.Q<Label>("icon-glyph");
            titleLabel = root.Q<Label>("title");
            bodyLabel = root.Q<Label>("body");
            primary = root.Q<Button>("primary");
            secondary = root.Q<Button>("secondary");

            PanelEnvironment.IgnoreExceptControls(root);
            if (primary != null)
            {
                primary.clicked += PressPrimary;
            }
            if (secondary != null)
            {
                secondary.clicked += PressSecondary;
            }
            listening = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            WatchForRebuild(panel);
            Refresh();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (primary != null)
            {
                primary.clicked -= PressPrimary;
            }
            if (secondary != null)
            {
                secondary.clicked -= PressSecondary;
            }
            if (listening)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
                listening = false;
            }
            panel = null;
        }

        /// <summary>Shows the panel.</summary>
        public void Show()
        {
            if (visible)
            {
                return;
            }
            visible = true;
            Refresh();
            onShown.Invoke();
        }

        /// <summary>Hides the panel. Its buttons stop taking clicks while hidden.</summary>
        public void Hide()
        {
            if (!visible)
            {
                return;
            }
            visible = false;
            Refresh();
            onHidden.Invoke();
        }

        /// <summary>Sets the title as written (no localization).</summary>
        public void SetTitle(string text) => SetText(ref title, text, string.Empty);

        /// <summary>Sets the title from a localization key, with the text shown when it has no translation.</summary>
        public void SetTitle(string key, string fallback) => SetText(ref title, fallback, key);

        /// <summary>Sets the body as written (no localization).</summary>
        public void SetText(string text) => SetText(ref body, text, string.Empty);

        /// <summary>Sets the body from a localization key, with the text shown when it has no translation.</summary>
        public void SetText(string key, string fallback) => SetText(ref body, fallback, key);

        /// <summary>Sets the label of the primary button; empty hides it.</summary>
        public void SetPrimaryLabel(string text) => SetText(ref primaryButton, text, string.Empty);

        /// <summary>Sets the label of the secondary button; empty hides it.</summary>
        public void SetSecondaryLabel(string text) => SetText(ref secondaryButton, text, string.Empty);

        void IScenePanel.SetTexts(string titleText, string bodyText)
        {
            if (titleText != null)
            {
                title = new LocalizedText(titleText);
            }
            if (bodyText != null)
            {
                body = new LocalizedText(bodyText);
            }
            Refresh();
        }

        private void SetText(ref LocalizedText field, string text, string key)
        {
            field = new LocalizedText(text, key);
            Refresh();
        }

        private void PressPrimary()
        {
            onPrimary.Invoke();
            PanelEvents.RaiseButtonPressed(this, 0);
            if (hideOnButton)
            {
                Hide();
            }
        }

        private void PressSecondary()
        {
            onSecondary.Invoke();
            PanelEvents.RaiseButtonPressed(this, 1);
            if (hideOnButton)
            {
                Hide();
            }
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (panel == null)
            {
                return;
            }

            // display:none, not visibility: a hidden panel must not take picks either.
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 1; i < variantClasses.Length; i++)
            {
                panel.EnableInClassList(variantClasses[i], (int)variant == i);
            }
            if (icon != null)
            {
                icon.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (iconGlyph != null)
            {
                iconGlyph.text = variantGlyphs[(int)variant];
            }
            Apply(titleLabel, title.Resolve());
            Apply(bodyLabel, body.Resolve());
            Apply(primary, primaryButton.Resolve());
            Apply(secondary, secondaryButton.Resolve());
            if (TryGetComponent(out ThemedDocument themed))
            {
                themed.Reapply();
            }
        }

        private static void Apply(TextElement element, string text)
        {
            if (element == null)
            {
                return;
            }
            element.text = text;
            element.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Refresh();
            }
        }
    }
}
