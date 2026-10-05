using System;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Open/closed toggle for an element of a UI Toolkit document: clicking the <c>trigger</c> element
    /// shows the <c>target</c> element, clicking it again hides it. The trigger itself stays visible the
    /// whole time; only the target is shown/hidden. Works with any UIDocument, world-space or
    /// screen-space — e.g. a point-of-interest icon that opens its info panel, a "?" that opens a hint,
    /// a header that expands a section.
    ///
    /// The trigger name may be left empty: the toggle is then driven only through <see cref="Open"/>,
    /// <see cref="Close"/>, <see cref="Toggle"/> and <see cref="SetOpen"/> (from a UnityEvent — e.g. a
    /// <see cref="WorldSpaceButtonBinder"/> entry — a Visual Scripting graph or code).
    ///
    /// The state is applied in two optional ways: the target's <c>display</c> (<c>useDisplay</c>), and
    /// a USS class (<c>openClassName</c>) put on both the target and the trigger while open, so USS can
    /// style the active trigger or animate the target (turn <c>useDisplay</c> off and let the class
    /// drive opacity / scale transitions). Hover feedback stays purely in USS
    /// (e.g. <c>.poi-icon:hover { scale: 1.1 1.1; }</c>). Click is delivered by Unity's native UI
    /// Toolkit picking (the same path <see cref="WorldSpaceButtonBinder"/> documents), so it works on
    /// VR (XR ray), desktop (mouse) and mobile (tap).
    ///
    /// Click-outside-to-close and mutual exclusion are intentionally NOT handled here:
    /// <see cref="UIToolkitToggleGroup"/> does them, using <see cref="Opened"/> and
    /// <see cref="PressedInside"/>.
    ///
    /// Like the sibling utilities, it binds lazily (the visual tree is not built on the first
    /// <c>OnEnable</c>) and re-binds itself if the tree is rebuilt at runtime (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), preserving the open/closed state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIToolkitToggleElement : UIDocumentBinder
    {
        [SerializeField, FormerlySerializedAs("iconName"), Tooltip("Name of the clickable element in " +
            "the UXML that toggles the target (a Button, or any pickable element). Leave empty to drive " +
            "the toggle only from code / UnityEvents.")]
        private string triggerName = "trigger";

        [SerializeField, FormerlySerializedAs("panelName"), Tooltip("Name of the element in the UXML " +
            "to show/hide.")]
        private string targetName = "target";

        [SerializeField, Tooltip("If true, the target starts open.")]
        private bool startOpen = false;

        [SerializeField, Tooltip("If true, the target's 'display' is set to flex when open and none " +
            "when closed. Turn it off to express the state only through the open class (e.g. to " +
            "animate it in USS).")]
        private bool useDisplay = true;

        [SerializeField, Tooltip("USS class added to the target and the trigger while open (e.g. " +
            "'is-open'). Leave empty for none.")]
        private string openClassName = "";

        [SerializeField, Tooltip("Invoked when the target opens.")]
        private UnityEvent onOpen = new();

        [SerializeField, Tooltip("Invoked when the target closes.")]
        private UnityEvent onClose = new();

        private Button triggerButton;
        private VisualElement triggerElement;
        private VisualElement target;
        private bool isOpen;

        /// <summary>Whether the target is currently open.</summary>
        public bool IsOpen => isOpen;

        /// <summary>
        /// Raised (with this toggle) whenever the target opens. Code-side counterpart of
        /// <c>onOpen</c>, used by <see cref="UIToolkitToggleGroup"/> to close the other toggles.
        /// </summary>
        public event Action<UIToolkitToggleElement> Opened;

        /// <summary>Raised (with this toggle) whenever the target closes. Counterpart of <c>onClose</c>.</summary>
        public event Action<UIToolkitToggleElement> Closed;

        /// <summary>
        /// Raised (with this toggle) when a pointer goes down on the trigger or on the target — mouse,
        /// touch or XR ray alike, since it comes from the same UI Toolkit picking as the clicks. Used by
        /// <see cref="UIToolkitToggleGroup"/> to tell presses on a toggle from presses outside every
        /// toggle.
        /// </summary>
        public event Action<UIToolkitToggleElement> PressedInside;

        protected override void OnEnable()
        {
            isOpen = startOpen;
            base.OnEnable();
        }

        /// <summary>Opens the target.</summary>
        public void Open() => SetOpen(true);

        /// <summary>Closes the target.</summary>
        public void Close() => SetOpen(false);

        /// <summary>Toggles the target between open and closed.</summary>
        public void Toggle() => SetOpen(!isOpen);

        /// <summary>Opens (true) or closes (false) the target. UnityEvent&lt;bool&gt;-friendly.</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
            ApplyState();
            if (open)
            {
                onOpen?.Invoke();
                Opened?.Invoke(this);
            }
            else
            {
                onClose?.Invoke();
                Closed?.Invoke(this);
            }
        }

        private void ApplyState()
        {
            if (target != null && useDisplay)
            {
                target.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!string.IsNullOrEmpty(openClassName))
            {
                target?.EnableInClassList(openClassName, isOpen);
                triggerElement?.EnableInClassList(openClassName, isOpen);
            }
        }

        // The open/closed state in isOpen survives a rebuild of the tree and is re-applied here.
        protected override bool BindTo(VisualElement root)
        {
            bool hasTrigger = !string.IsNullOrEmpty(triggerName);
            triggerElement = hasTrigger ? root.Q<VisualElement>(triggerName) : null;
            target = root.Q<VisualElement>(targetName);
            if (target == null || (hasTrigger && triggerElement == null))
            {
                // Tree exists but the elements are not in yet (or a name is wrong); retry.
                triggerElement = null;
                target = null;
                return false;
            }

            // Apply the current open/closed state to the freshly found elements, then wire the trigger.
            ApplyState();

            if (triggerElement != null)
            {
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
            }
            target.RegisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);

            // Re-bind automatically if the tree gets rebuilt (the elements detach from the panel).
            WatchForRebuild(triggerElement ?? target);
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
            target?.UnregisterCallback<PointerDownEvent>(OnPointerDownInside, TrickleDown.TrickleDown);
            triggerElement = null;
            target = null;
        }

        private void OnTriggerPointerDown(PointerDownEvent _) => Toggle();

        private void OnPointerDownInside(PointerDownEvent _) => PressedInside?.Invoke(this);
    }
}
