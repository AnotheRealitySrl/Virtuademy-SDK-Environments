using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A counter with a progress bar, a hint box and a hint button: the UI Toolkit successor of the
    /// SDK's <c>AnimatedScreenCanvas</c>. Over the screen on desktop, WebGL and mobile, inside the safe
    /// frame; in VR a panel that follows the head (<see cref="OverlayPanelPlacement"/>).
    /// </summary>
    /// <remarks>
    /// Same functions as the uGUI HUD — show, hide, maximum, count, show hint, hide hint, ask for a
    /// hint — as public methods for scripts and graphs, plus the events a step needs.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy HUD")]
    public class VirtuademyHud : UIDocumentBinder, IScenePanel
    {
        [SerializeField, Tooltip("Label left of the counter.")]
        private VirtuademyPanel.LocalizedText counterLabel = new("Found");

        [SerializeField, Tooltip("Label of the hint button. Empty text and key: no button.")]
        private VirtuademyPanel.LocalizedText hintButtonLabel = new("Hint");

        [SerializeField, Tooltip("Hint the button shows (a second press hides it). Empty text and key: the " +
            "button only raises On Hint Requested, for a script or graph that picks the hint itself " +
            "(DisplayHint, IUIApi.SetHudHint).")]
        private VirtuademyPanel.LocalizedText hintOnRequest = new("Write the hint of this step in the HUD's Hint On Request field.");

        [SerializeField, Min(0)] private int maxCount = 5;
        [SerializeField, Min(0)] private int count;

        [SerializeField, Tooltip("Visible when the scene starts. Off: call Show().")]
        private bool visibleOnStart = true;

        [Header("Events")]
        [SerializeField, Tooltip("The hint button was pressed.")]
        private UnityEvent onHintRequested = new();

        [SerializeField, Tooltip("The count changed, with the new value.")]
        private UnityEvent<int> onCountChanged = new();

        [SerializeField, Tooltip("The count reached the maximum.")]
        private UnityEvent onCompleted = new();

        private VisualElement hud;
        private Label counterTitle;
        private Label counterValue;
        private VisualElement progressFill;
        private VisualElement hintBox;
        private Label hintText;
        private Button hintButton;
        private bool visible;
        private string hint;
        private bool listening;

        public UnityEvent OnHintRequested => onHintRequested;
        public UnityEvent<int> OnCountChanged => onCountChanged;
        public UnityEvent OnCompleted => onCompleted;

        public int Count => count;
        public int MaxCount => maxCount;

        private void Awake()
        {
            visible = visibleOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            hud = root.Q<VisualElement>("hud");
            if (hud == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyHud)}] '{name}': the document has no element named 'hud'.", this);
                return false;
            }
            counterTitle = root.Q<Label>("counter-label");
            counterValue = root.Q<Label>("counter");
            progressFill = root.Q<VisualElement>("progress-fill");
            hintBox = root.Q<VisualElement>("hint");
            hintText = root.Q<Label>("hint-text");
            hintButton = root.Q<Button>("hint-button");
            PanelEnvironment.IgnoreExceptControls(root);
            if (hintButton != null)
            {
                hintButton.clicked += RequestHint;
            }
            listening = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            WatchForRebuild(hud);
            Refresh();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (hintButton != null)
            {
                hintButton.clicked -= RequestHint;
            }
            if (listening)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
                listening = false;
            }
            hud = null;
        }

        /// <summary>Shows the HUD (it slides in).</summary>
        public void Show()
        {
            visible = true;
            Refresh();
        }

        /// <summary>Hides the HUD (it slides out).</summary>
        public void Hide()
        {
            visible = false;
            Refresh();
        }

        /// <summary>Sets the maximum of the counter.</summary>
        public void SetMaxCount(int value)
        {
            maxCount = Mathf.Max(0, value);
            Refresh();
        }

        /// <summary>Sets the counter; raises the events when it changes and when it reaches the maximum.</summary>
        public void UpdateCount(int value)
        {
            value = Mathf.Max(0, value);
            if (value == count)
            {
                return;
            }
            count = value;
            Refresh();
            onCountChanged.Invoke(count);
            if (maxCount > 0 && count >= maxCount)
            {
                onCompleted.Invoke();
            }
        }

        /// <summary>Adds one to the counter.</summary>
        public void Increment() => UpdateCount(count + 1);

        /// <summary>Shows a hint under the counter, as written.</summary>
        public void DisplayHint(string text)
        {
            hint = text;
            Refresh();
        }

        /// <summary>Hides the hint.</summary>
        public void HideHint()
        {
            hint = null;
            Refresh();
        }

        /// <summary>The same as pressing the hint button.</summary>
        /// <remarks>
        /// With a <see cref="hintOnRequest"/> text the press shows it, or hides it if it is already
        /// shown; the events are raised either way, after the hint changed.
        /// </remarks>
        public void RequestHint()
        {
            string authored = hintOnRequest.Resolve();
            if (!string.IsNullOrEmpty(authored))
            {
                if (hint == authored)
                {
                    HideHint();
                }
                else
                {
                    DisplayHint(authored);
                }
            }
            onHintRequested.Invoke();
            PanelEvents.RaiseButtonPressed(this, 0);
        }

        /// <summary>Sets the hint the button shows, as written; empty makes the button event-only.</summary>
        public void SetHintOnRequest(string text)
        {
            hintOnRequest = new VirtuademyPanel.LocalizedText(text ?? string.Empty);
        }

        /// <summary>The title is the counter label; the text is the hint (empty hides it).</summary>
        void IScenePanel.SetTexts(string title, string text)
        {
            if (title != null)
            {
                counterLabel = new VirtuademyPanel.LocalizedText(title);
            }
            if (text != null)
            {
                hint = text;
            }
            Refresh();
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (hud == null)
            {
                return;
            }
            hud.EnableInClassList("vd-hud--hidden", !visible);
            if (hintButton != null)
            {
                // The class only fades the HUD out; a faded button must not take clicks (nor hold
                // the interaction gate, which does not look at the enabled state).
                hintButton.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
                string label = hintButtonLabel.Resolve();
                hintButton.text = label;
                hintButton.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (counterTitle != null)
            {
                counterTitle.text = counterLabel.Resolve();
            }
            if (counterValue != null)
            {
                counterValue.text = maxCount > 0 ? $"{count}/{maxCount}" : count.ToString();
            }
            if (progressFill != null)
            {
                float ratio = maxCount > 0 ? Mathf.Clamp01((float)count / maxCount) : 0f;
                progressFill.style.width = Length.Percent(ratio * 100f);
            }
            if (hintBox != null)
            {
                hintBox.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (hintText != null)
            {
                hintText.text = hint ?? string.Empty;
            }
        }
    }
}
