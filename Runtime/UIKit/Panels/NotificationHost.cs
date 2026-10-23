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
    /// the popup is modal: its scrim takes the clicks that would reach the world, and closes it when
    /// <see cref="closePopupOnOutsideClick"/> (or the call) says so. Both fade in and out over the
    /// theme's <c>--vd-motion-fade</c>. Every text is shown as given (pass translated text).
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Notification Host")]
    public class NotificationHost : UIDocumentBinder
    {
        private const string VisibleClass = "vd-toast--visible";
        private const string PopupVisibleClass = "vd-popup-scrim--visible";

        // Used when the stylesheet gives no transition: the fade itself is the theme's
        // --vd-motion-fade, read back from the resolved style.
        private const long FallbackFadeMs = 250;

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

        [SerializeField, Tooltip("A click or tap outside the popup closes it, reporting -1. A call can " +
            "decide for itself (IUIApi.ShowPopup with closeOnOutsideClick, the node's input).")]
        private bool closePopupOnOutsideClick;

        [SerializeField, Min(0f), Tooltip("Seconds after which a popup closes by itself (reporting -1) when " +
            "the call does not say; 0: never. The timer belongs to its popup and never closes a later one.")]
        private float popupAutoCloseSeconds;

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
        private int openPopupId;
        // Static: ids are unique across hosts, so an id never names another host's popup.
        private static int lastPopupId;
        private readonly HashSet<int> pendingPopups = new();
        private readonly HashSet<int> cancelledPopups = new();
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
            pendingPopups.Clear();
            cancelledPopups.Clear();
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

        /// <summary>
        /// Whether a click outside the popup closes it, when the call does not say. Off by default.
        /// </summary>
        public bool ClosePopupOnOutsideClick
        {
            get => closePopupOnOutsideClick;
            set => closePopupOnOutsideClick = value;
        }

        /// <summary>Shows the popup. See <c>IUIApi.ShowPopup</c>; outside clicks follow <see cref="ClosePopupOnOutsideClick"/>.</summary>
        public void ShowPopup(string title, string text, string[] buttons, PopupClosedHandler onClosed = null)
            => ShowPopup(title, text, buttons, onClosed, closePopupOnOutsideClick, popupAutoCloseSeconds);

        /// <summary>
        /// Shows the popup. With <paramref name="closeOnOutsideClick"/>, a click or tap on the scrim
        /// around it closes it and reports -1.
        /// </summary>
        public void ShowPopup(string title, string text, string[] buttons, PopupClosedHandler onClosed,
                              bool closeOnOutsideClick)
            => ShowPopup(title, text, buttons, onClosed, closeOnOutsideClick, popupAutoCloseSeconds);

        /// <summary>
        /// Shows the popup and returns its id, for <see cref="ClosePopup(int)"/>. With
        /// <paramref name="autoCloseSeconds"/> above zero it closes by itself after that long,
        /// reporting -1. The timer belongs to this popup: it dies with it, so it can never close a
        /// popup opened later.
        /// </summary>
        public int ShowPopup(string title, string text, string[] buttons, PopupClosedHandler onClosed,
                             bool closeOnOutsideClick, float autoCloseSeconds)
        {
            int id = ++lastPopupId;
            if (!IsBound)
            {
                pending.Enqueue(() => Open(id, title, text, buttons, onClosed, closeOnOutsideClick, autoCloseSeconds));
                pendingPopups.Add(id);
                return id;
            }
            Open(id, title, text, buttons, onClosed, closeOnOutsideClick, autoCloseSeconds);
            return id;
        }

        private void Open(int id, string title, string text, string[] buttons, PopupClosedHandler onClosed,
                          bool closeOnOutsideClick, float autoCloseSeconds)
        {
            // A popup closed by id before the host could draw it is answered and never drawn.
            if (pendingPopups.Remove(id) && cancelledPopups.Remove(id))
            {
                onClosed?.Invoke(-1);
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
            // The panel is pickable too, so a click on its body is not a click on the scrim.
            VisualElement body = popup.Q<VisualElement>("popup");
            if (body != null)
            {
                body.pickingMode = PickingMode.Position;
            }
            if (closeOnOutsideClick)
            {
                popup.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == popup)
                    {
                        Close(id, -1);
                    }
                });
            }

            VisualElement actions = popup.Q<VisualElement>("actions");
            string[] labels = buttons == null || buttons.Length == 0 ? new[] { defaultButtonLabel } : buttons;
            int count = Mathf.Min(labels.Length, 3);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = new Button(() => Close(id, index)) { text = labels[i] };
                button.AddToClassList("vd-button");
                button.AddToClassList(i == count - 1 ? "vd-button--primary" : "vd-button--secondary");
                actions?.Add(button);
            }

            popupLayer.Add(popup);
            openPopup = popup;
            openPopupId = id;
            openPopupHandler = onClosed;
            ThemeApplier.Apply(popup);
            // One frame later, so the fade runs from the hidden state.
            popup.schedule.Execute(() => popup.AddToClassList(PopupVisibleClass)).StartingIn(16);
            if (autoCloseSeconds > 0f)
            {
                // Scheduled on this popup's element and checked against its id: once the popup is
                // gone, the timer either never runs or finds another id and does nothing.
                popup.schedule.Execute(() => Close(id, -1)).StartingIn((long)(autoCloseSeconds * 1000f));
            }
            onPopupOpened.Invoke(title);
        }

        /// <summary>Closes the open popup, whichever it is, reporting -1.</summary>
        public void ClosePopup() => Close(openPopupId, -1);

        /// <summary>
        /// Closes the popup <paramref name="popupId"/> (from <c>ShowPopup</c>), reporting -1. Does
        /// nothing when that popup is already closed or another one has replaced it.
        /// </summary>
        public void ClosePopup(int popupId)
        {
            if (pendingPopups.Contains(popupId))
            {
                cancelledPopups.Add(popupId);
                return;
            }
            Close(popupId, -1);
        }

        /// <summary>The id of the open popup; 0 when none is open.</summary>
        public int OpenPopupId => openPopup != null ? openPopupId : 0;

        private void Close(int id, int index)
        {
            if (openPopup == null || id != openPopupId)
            {
                return;
            }
            VisualElement closing = openPopup;
            openPopup = null;
            openPopupId = 0;
            FadeOutAndRemove(closing);
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
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(FadeMs(toast));
        }

        private static void FadeOutAndRemove(VisualElement popup)
        {
            // Nothing in it takes clicks while it fades.
            popup.pickingMode = PickingMode.Ignore;
            popup.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            if (popup.panel == null)
            {
                popup.RemoveFromHierarchy();
                return;
            }
            popup.RemoveFromClassList(PopupVisibleClass);
            popup.schedule.Execute(popup.RemoveFromHierarchy).StartingIn(FadeMs(popup));
        }

        /// <summary>The longest transition the element's style declares, in milliseconds.</summary>
        private static long FadeMs(VisualElement element)
        {
            float longest = -1f;
            foreach (TimeValue time in element.resolvedStyle.transitionDuration)
            {
                float seconds = time.unit == TimeUnit.Millisecond ? time.value / 1000f : time.value;
                longest = Mathf.Max(longest, seconds);
            }
            return longest < 0f ? FallbackFadeMs : (long)(longest * 1000f) + 16;
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
        private static void ResetForDomainReloadOff()
        {
            hosts.Clear();
            lastPopupId = 0;
        }
    }
}
