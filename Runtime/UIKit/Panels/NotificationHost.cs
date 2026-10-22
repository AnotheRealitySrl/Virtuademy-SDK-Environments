using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.Environments.ScriptingApi.UI;
using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Draws the toasts and the popup that <c>IUIApi.ShowToast</c> / <c>ShowPopup</c> (and their
    /// Visual Scripting nodes) ask for, in the Virtuademy theme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An environment that wants its own look drops the SDK's <c>NotificationHost</c> prefab in its
    /// scene and restyles it; the application keeps a copy of the same prefab for every other
    /// environment (<see cref="isApplicationFallback"/>). <see cref="Current"/> prefers the scene's.
    /// </para>
    /// <para>
    /// Toasts stack at the bottom of the safe frame, newest last, at most <see cref="maxToasts"/>;
    /// the popup is modal: its scrim takes the clicks that would reach the world. Every text is
    /// shown as given (pass translated text).
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Notification Host")]
    public class NotificationHost : UIDocumentBinder
    {
        private const string VisibleClass = "vd-toast--visible";
        private const float LeaveSeconds = 0.3f;

        private static readonly List<NotificationHost> hosts = new();

        [SerializeField, Tooltip("Template of one toast: an element named 'toast' with a Label named 'text'.")]
        private VisualTreeAsset toastTemplate;

        [SerializeField, Tooltip("Template of the popup: 'scrim' > 'popup' with Labels 'title' and 'body' " +
            "and an element 'actions' for the buttons.")]
        private VisualTreeAsset popupTemplate;

        [SerializeField, Tooltip("Seconds a toast stays when the caller gives no duration.")]
        private float defaultToastSeconds = 3f;

        [SerializeField, Range(1, 6), Tooltip("Toasts on screen at once; the oldest leaves first.")]
        private int maxToasts = 3;

        [SerializeField, Tooltip("Label of the single button of a popup shown without buttons.")]
        private string defaultButtonLabel = "OK";

        [SerializeField, HideInInspector, Tooltip("Set on the application's copy: a host in the scene wins over it.")]
        private bool isApplicationFallback;

        [Header("Events")]
        [SerializeField, Tooltip("A toast appeared, with its text.")]
        private UnityEvent<string> onToastShown = new();

        [SerializeField, Tooltip("A popup opened, with its title.")]
        private UnityEvent<string> onPopupOpened = new();

        [SerializeField, Tooltip("The popup closed, with the index of the button (-1: closed without one).")]
        private UnityEvent<int> onPopupClosed = new();

        private VisualElement toastStack;
        private VisualElement popupLayer;
        private readonly List<VisualElement> toasts = new();
        private VisualElement openPopup;
        private PopupClosedHandler openPopupHandler;
        private readonly Queue<System.Action> pending = new();

        /// <summary>
        /// The host to draw in: the most recently enabled one in the environment's scene, else the
        /// application's. Null when there is none.
        /// </summary>
        public static NotificationHost Current
        {
            get
            {
                NotificationHost fallback = null;
                for (int i = hosts.Count - 1; i >= 0; i--)
                {
                    if (!hosts[i].isApplicationFallback)
                    {
                        return hosts[i];
                    }
                    fallback ??= hosts[i];
                }
                return fallback;
            }
        }

        public UnityEvent<string> OnToastShown => onToastShown;
        public UnityEvent<string> OnPopupOpened => onPopupOpened;
        public UnityEvent<int> OnPopupClosed => onPopupClosed;

        /// <summary>Whether a popup is open on this host.</summary>
        public bool IsPopupOpen => openPopup != null;

        /// <summary>Marks the application's copy. Called by the application right after it instantiates it.</summary>
        public void MarkAsApplicationFallback() => isApplicationFallback = true;

        protected override void OnEnable()
        {
            hosts.Remove(this);
            hosts.Add(this);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            hosts.Remove(this);
            pending.Clear();
            base.OnDisable();
        }

        protected override bool BindTo(VisualElement root)
        {
            toastStack = root.Q<VisualElement>("toast-stack");
            popupLayer = root.Q<VisualElement>("popup-layer");
            if (toastStack == null || popupLayer == null)
            {
                Debug.LogWarning($"[{nameof(NotificationHost)}] '{name}': the document has no 'toast-stack' " +
                    "or 'popup-layer' element.", this);
                return false;
            }
            PanelEnvironment.IgnoreExceptControls(root);
            WatchForRebuild(toastStack);
            while (pending.Count > 0)
            {
                pending.Dequeue().Invoke();
            }
            return true;
        }

        protected override void UnbindFromTree()
        {
            // The tree is going (disabled, destroyed or rebuilt): a popup on it can no longer be
            // answered, so its handler hears -1 rather than nothing.
            ClosePopup();
            toasts.Clear();
            toastStack = null;
            popupLayer = null;
        }

        /// <summary>Shows a toast. See <c>IUIApi.ShowToast</c>.</summary>
        public void ShowToast(string text, EToastSeverity severity = EToastSeverity.Info, float duration = 0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!IsBound)
            {
                pending.Enqueue(() => ShowToast(text, severity, duration));
                return;
            }

            VisualElement toast = Instantiate(toastTemplate, "toast");
            if (toast == null)
            {
                return;
            }
            toast.AddToClassList("vd-toast--" + severity.ToString().ToLowerInvariant());
            Label label = toast.Q<Label>("text");
            if (label != null)
            {
                label.text = text;
            }
            PanelEnvironment.IgnoreExceptControls(toast);

            while (toasts.Count >= maxToasts)
            {
                Remove(toasts[0], immediately: true);
            }
            toastStack.Add(toast);
            toasts.Add(toast);
            ThemeApplier.Apply(toast);

            // One frame later, so the transition runs from the hidden state.
            toast.schedule.Execute(() => toast.AddToClassList(VisibleClass)).StartingIn(16);
            float seconds = duration > 0f ? duration : defaultToastSeconds;
            toast.schedule.Execute(() => Remove(toast, immediately: false)).StartingIn((long)(seconds * 1000f));
            onToastShown.Invoke(text);
        }

        /// <summary>Shows the popup. See <c>IUIApi.ShowPopup</c>.</summary>
        public void ShowPopup(string title, string text, string[] buttons, PopupClosedHandler onClosed = null)
        {
            if (!IsBound)
            {
                pending.Enqueue(() => ShowPopup(title, text, buttons, onClosed));
                return;
            }

            ClosePopup();

            VisualElement popup = Instantiate(popupTemplate, "scrim");
            if (popup == null)
            {
                onClosed?.Invoke(-1);
                return;
            }
            SetLabel(popup, "title", title);
            SetLabel(popup, "body", text);
            PanelEnvironment.IgnoreExceptControls(popup);
            // The scrim is what makes the popup modal: pickable, it takes the clicks meant for the world.
            popup.pickingMode = PickingMode.Position;

            VisualElement actions = popup.Q<VisualElement>("actions");
            string[] labels = buttons == null || buttons.Length == 0 ? new[] { defaultButtonLabel } : buttons;
            int count = Mathf.Min(labels.Length, 3);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = new Button(() => ClosePopup(index)) { text = labels[i] };
                button.AddToClassList("vd-button");
                button.AddToClassList(i == count - 1 ? "vd-button--primary" : "vd-button--secondary");
                actions?.Add(button);
            }

            popupLayer.Add(popup);
            openPopup = popup;
            openPopupHandler = onClosed;
            ThemeApplier.Apply(popup);
            onPopupOpened.Invoke(title);
        }

        /// <summary>Closes the open popup, reporting -1.</summary>
        public void ClosePopup() => ClosePopup(-1);

        private void ClosePopup(int index)
        {
            if (openPopup == null)
            {
                return;
            }
            openPopup.RemoveFromHierarchy();
            openPopup = null;
            PopupClosedHandler handler = openPopupHandler;
            openPopupHandler = null;
            handler?.Invoke(index);
            onPopupClosed.Invoke(index);
        }

        private VisualElement Instantiate(VisualTreeAsset template, string elementName)
        {
            if (template == null)
            {
                Debug.LogWarning($"[{nameof(NotificationHost)}] '{name}' has no template for '{elementName}'.", this);
                return null;
            }
            TemplateContainer container = template.Instantiate();
            VisualElement element = container.Q<VisualElement>(elementName);
            if (element == null)
            {
                Debug.LogWarning($"[{nameof(NotificationHost)}] Template '{template.name}' has no '{elementName}'.", this);
                return null;
            }
            element.RemoveFromHierarchy();
            return element;
        }

        private void Remove(VisualElement toast, bool immediately)
        {
            if (!toasts.Remove(toast))
            {
                return;
            }
            if (immediately)
            {
                toast.RemoveFromHierarchy();
                return;
            }
            toast.RemoveFromClassList(VisibleClass);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn((long)(LeaveSeconds * 1000f));
        }

        private static void SetLabel(VisualElement root, string elementName, string text)
        {
            Label label = root.Q<Label>(elementName);
            if (label == null)
            {
                return;
            }
            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReloadOff() => hosts.Clear();
    }
}
