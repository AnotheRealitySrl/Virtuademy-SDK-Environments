using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Open/closed toggle driven by a UI Toolkit element: clicking the <c>trigger</c> element (in the
    /// <c>document</c>, e.g. a point-of-interest icon) flips the state and raises <c>onOpen</c> /
    /// <c>onClose</c>. What "open" means is entirely up to those events — typically
    /// <c>panel.SetActive(true)</c> / <c>panel.SetActive(false)</c> on a child GameObject that holds the
    /// panel's own UIDocument. Works with any UIDocument, world-space or screen-space.
    ///
    /// The trigger name may be left empty: the toggle is then driven only through <see cref="Open"/>,
    /// <see cref="Close"/>, <see cref="Toggle"/> and <see cref="SetOpen"/> (from a UnityEvent — e.g. a
    /// <see cref="UIToolkitButtonBinder"/> entry — a Visual Scripting graph or code).
    ///
    /// The optional <c>panel</c> reference is only used to tell presses on the open panel from presses
    /// elsewhere (<see cref="PressedInside"/>): every UIDocument under it counts as "inside". A USS class
    /// (<c>openClassName</c>) can be put on the trigger while open, to style the active trigger. Hover
    /// feedback stays purely in USS (e.g. <c>.poi-icon:hover { scale: 1.1 1.1; }</c>). Click is
    /// delivered by Unity's native UI Toolkit picking (the same path <see cref="UIToolkitButtonBinder"/>
    /// documents), so it works on VR (XR ray), desktop (mouse) and mobile (tap).
    ///
    /// The toggle starts closed and raises no event at start, unless <c>startOpen</c> is set: then it
    /// opens (raising <c>onOpen</c>) on Start. Author the panel inactive so the closed state matches.
    ///
    /// Click-outside-to-close and mutual exclusion are intentionally NOT handled here:
    /// <see cref="UIToolkitToggleGroup"/> does them, using <see cref="Opened"/> and
    /// <see cref="PressedInside"/>.
    ///
    /// Like the sibling utilities, it binds lazily (the visual tree is not built on the first
    /// <c>OnEnable</c>) and re-binds itself if the tree is rebuilt at runtime (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), preserving the open/closed state.
    /// </summary>
    public class UIToolkitToggleElement : UIDocumentBinder
    {
        [SerializeField, FormerlySerializedAs("iconName"), Tooltip("Name of the clickable element in " +
            "the UXML that toggles (a Button, or any pickable element). Leave empty to drive the toggle " +
            "only from code / UnityEvents.")]
        private string triggerName = "trigger";

        [SerializeField, Tooltip("The panel this toggle opens (shown/hidden by onOpen / onClose). Presses " +
            "on any UIDocument under it count as inside, so a UIToolkitToggleGroup does not close it on " +
            "a click on the panel. Optional.")]
        private GameObject panel;

        [SerializeField, Tooltip("If true, the toggle opens on Start (raising onOpen).")]
        private bool startOpen = false;

        [SerializeField, Tooltip("USS class added to the trigger while open (e.g. 'is-open'). Leave " +
            "empty for none.")]
        private string openClassName = "";

        [SerializeField, Tooltip("Invoked when the toggle opens, e.g. panel.SetActive(true).")]
        private UnityEvent onOpen = new();

        [SerializeField, Tooltip("Invoked when the toggle closes, e.g. panel.SetActive(false).")]
        private UnityEvent onClose = new();

        private readonly List<UIDocument> panelDocuments = new();
        private readonly List<VisualElement> watchedPanelRoots = new();
        private Button triggerButton;
        private VisualElement triggerElement;
        private bool isOpen;

        /// <summary>Whether the toggle is currently open.</summary>
        public bool IsOpen => isOpen;

        /// <summary>The panel this toggle opens, if set.</summary>
        public GameObject Panel => panel;

        /// <summary>
        /// Raised (with this toggle) whenever it opens. Code-side counterpart of <c>onOpen</c>, used by
        /// <see cref="UIToolkitToggleGroup"/> to close the other toggles.
        /// </summary>
        public event Action<UIToolkitToggleElement> Opened;

        /// <summary>Raised (with this toggle) whenever it closes. Counterpart of <c>onClose</c>.</summary>
        public event Action<UIToolkitToggleElement> Closed;

        /// <summary>
        /// Raised (with this toggle) when a pointer goes down on the trigger or on the open panel —
        /// mouse, touch or XR ray alike, since it comes from the same UI Toolkit picking as the clicks.
        /// Used by <see cref="UIToolkitToggleGroup"/> to tell presses on a toggle from presses outside
        /// every toggle.
        /// </summary>
        public event Action<UIToolkitToggleElement> PressedInside;

        private void Start()
        {
            if (startOpen)
            {
                SetOpen(true);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnwatchPanel();
        }

        private void LateUpdate()
        {
            // The panel's UIDocuments build (and rebuild) their trees whenever the panel is activated,
            // so look for new roots while it is open. Few documents, and only for the open toggle.
            if (isOpen && panel != null && panel.activeInHierarchy)
            {
                WatchPanel();
            }
        }

        /// <summary>Opens the toggle.</summary>
        public void Open() => SetOpen(true);

        /// <summary>Closes the toggle.</summary>
        public void Close() => SetOpen(false);

        /// <summary>Toggles between open and closed.</summary>
        public void Toggle() => SetOpen(!isOpen);

        /// <summary>Opens (true) or closes (false) the toggle. UnityEvent&lt;bool&gt;-friendly.</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
            ApplyTriggerClass();
            if (open)
            {
                onOpen?.Invoke();
                WatchPanel();
                Opened?.Invoke(this);
            }
            else
            {
                UnwatchPanel();
                onClose?.Invoke();
                Closed?.Invoke(this);
            }
        }

        private void ApplyTriggerClass()
        {
            if (!string.IsNullOrEmpty(openClassName))
            {
                triggerElement?.EnableInClassList(openClassName, isOpen);
            }
        }

        // The open/closed state in isOpen survives a rebuild of the tree and is re-applied here.
        protected override bool BindTo(VisualElement root)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                // No trigger: driven only from code / UnityEvents.
                return true;
            }
            triggerElement = root.Q<VisualElement>(triggerName);
            if (triggerElement == null)
            {
                // Tree exists but the element is not in yet (or the name is wrong); retry.
                return false;
            }

            ApplyTriggerClass();

            triggerButton = triggerElement as Button;
            if (triggerButton != null)
            {
                triggerButton.clicked += Toggle;
            }
            else
            {
                triggerElement.RegisterCallback<PointerDownEvent>(OnTriggerPointerDown);
            }

            // Trickle-down: seen before any child (a Button) can stop the event's propagation.
            triggerElement.RegisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);

            // Re-bind automatically if the tree gets rebuilt (the elements detach from the panel).
            WatchForRebuild(triggerElement);
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (triggerButton != null)
            {
                triggerButton.clicked -= Toggle;
                triggerButton = null;
            }
            else if (triggerElement != null)
            {
                triggerElement.UnregisterCallback<PointerDownEvent>(OnTriggerPointerDown);
            }
            triggerElement?.UnregisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);
            triggerElement = null;
        }

        private void WatchPanel()
        {
            if (panel == null)
            {
                return;
            }
            panel.GetComponentsInChildren(true, panelDocuments);
            foreach (UIDocument panelDocument in panelDocuments)
            {
                VisualElement root = panelDocument.rootVisualElement;
                if (root != null && !watchedPanelRoots.Contains(root))
                {
                    // Trickle-down on the root sees every press on the panel's pickable elements.
                    root.RegisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);
                    watchedPanelRoots.Add(root);
                }
            }
        }

        private void UnwatchPanel()
        {
            foreach (VisualElement root in watchedPanelRoots)
            {
                root.UnregisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);
            }
            watchedPanelRoots.Clear();
        }

        private void OnTriggerPointerDown(PointerDownEvent _) => Toggle();

        private void OnPointerDownInside(PointerDownEvent _) => PressedInside?.Invoke(this);
    }
}
