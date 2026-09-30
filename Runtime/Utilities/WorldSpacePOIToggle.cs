using System.Collections;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Turns a world-space UI Toolkit panel into a point-of-interest (POI) popup: a clickable icon
    /// opens an info panel, clicking the icon again closes it. The icon itself stays visible the whole
    /// time; only the <c>panel</c> element is shown/hidden.
    ///
    /// The icon's hover feedback (a small enlarge) is authored purely in USS
    /// (<c>.poi-icon:hover { scale: 1.1 1.1; }</c>) — this component never touches the visuals, it only
    /// flips the panel's <c>display</c>. Click is delivered by Unity's native world-space UI Toolkit
    /// picking (the same path <see cref="WorldSpaceButtonBinder"/> documents), so it works on VR (XR
    /// ray), desktop (mouse) and mobile (tap).
    ///
    /// Click-outside-to-close is intentionally NOT handled here.
    ///
    /// Like the sibling utilities, it binds lazily (the visual tree is not built on the first
    /// <c>OnEnable</c>) and re-binds itself if the tree is rebuilt at runtime (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), preserving the open/closed state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpacePOIToggle : MonoBehaviour, IVisualTreeRebindable
    {
        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

        [SerializeField, Tooltip("Name of the clickable icon element in the UXML (a Button, or any " +
            "pickable element).")]
        private string iconName = "icon";

        [SerializeField, Tooltip("Name of the panel element in the UXML to show/hide.")]
        private string panelName = "panel";

        [SerializeField, Tooltip("If true, the panel starts open.")]
        private bool startOpen = false;

        [SerializeField, Tooltip("Invoked when the panel opens.")]
        private UnityEvent onOpen = new();

        [SerializeField, Tooltip("Invoked when the panel closes.")]
        private UnityEvent onClose = new();

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxBindFrames = 120;

        private Coroutine bindRoutine;
        private Button iconButton;
        private VisualElement iconElement;
        private VisualElement panel;
        private VisualElement watched;
        private bool bound;
        private bool isOpen;

        /// <summary>Whether the panel is currently open.</summary>
        public bool IsOpen => isOpen;

        private void OnEnable()
        {
            isOpen = startOpen;
            if (!TryBind())
            {
                // rootVisualElement is not always built during OnEnable on the first frame; keep trying.
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        private void OnDisable()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            Unbind();
        }

        /// <summary>
        /// Drops the old tree's icon handler and binds the current one, keeping the open/closed state.
        /// The detach watch already covers a rebuild; this makes it explicit for
        /// <see cref="WorldSpaceUIDocumentRebuilder"/>.
        /// </summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            Unbind();
            if (!TryBind())
            {
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        /// <summary>Opens the panel.</summary>
        public void Open() => SetOpen(true);

        /// <summary>Closes the panel.</summary>
        public void Close() => SetOpen(false);

        /// <summary>Toggles the panel between open and closed.</summary>
        public void Toggle() => SetOpen(!isOpen);

        private void SetOpen(bool open)
        {
            isOpen = open;
            ApplyState();
            if (open)
            {
                onOpen?.Invoke();
            }
            else
            {
                onClose?.Invoke();
            }
        }

        private void ApplyState()
        {
            if (panel != null)
            {
                panel.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private IEnumerator BindWhenReady()
        {
            for (int frame = 0; frame < MaxBindFrames && !bound; frame++)
            {
                yield return null;
                if (TryBind())
                {
                    break;
                }
            }
            bindRoutine = null;
        }

        private bool TryBind()
        {
            if (bound)
            {
                return true;
            }
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }
            if (document == null)
            {
                Debug.LogWarning($"[{nameof(WorldSpacePOIToggle)}] No UIDocument found on '{name}'.", this);
                return false;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                // The document has not built its tree yet; the caller will retry.
                return false;
            }

            iconElement = root.Q<VisualElement>(iconName);
            panel = root.Q<VisualElement>(panelName);
            if (iconElement == null || panel == null)
            {
                // Tree exists but the elements are not in yet (or a name is wrong); retry.
                return false;
            }

            // Apply the current open/closed state to the freshly found panel, then wire the icon.
            ApplyState();

            iconButton = iconElement as Button;
            if (iconButton != null)
            {
                iconButton.clicked += Toggle;
            }
            else
            {
                iconElement.RegisterCallback<PointerDownEvent>(OnIconPointerDown);
            }

            // Re-bind automatically if the tree gets rebuilt (the elements detach from the panel).
            Watch(iconElement);

            bound = true;
            return true;
        }

        private void OnIconPointerDown(PointerDownEvent _) => Toggle();

        private void Unbind()
        {
            if (iconButton != null)
            {
                iconButton.clicked -= Toggle;
                iconButton = null;
            }
            else if (iconElement != null)
            {
                iconElement.UnregisterCallback<PointerDownEvent>(OnIconPointerDown);
            }
            iconElement = null;
            panel = null;
            Unwatch();
            bound = false;
        }

        private void Watch(VisualElement element)
        {
            if (watched == element)
            {
                return;
            }
            Unwatch();
            watched = element;
            watched.RegisterCallback<DetachFromPanelEvent>(OnIconDetached);
        }

        private void Unwatch()
        {
            if (watched != null)
            {
                watched.UnregisterCallback<DetachFromPanelEvent>(OnIconDetached);
                watched = null;
            }
        }

        private void OnIconDetached(DetachFromPanelEvent _)
        {
            // The open/closed state in isOpen is preserved and re-applied by TryBind.
            Unbind();

            // The tree also detaches when this object is disabled or destroyed — not only on a rebuild.
            // During SetActive(false) the detach runs synchronously while isActiveAndEnabled can still
            // read true, yet StartCoroutine already sees the GameObject as inactive and errors. So gate
            // on gameObject.activeInHierarchy — the exact native flag StartCoroutine tests — plus the
            // component's own enabled flag.
            if (!enabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            // Extra guard for the destroy path: only a genuine rebuild keeps the root on a live panel.
            IPanel panel = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.panel
                : null;
            if (panel == null)
            {
                return;
            }

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
            }
            bindRoutine = StartCoroutine(BindWhenReady());
        }
    }
}
