using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Manages a set of <see cref="UIToolkitToggleElement"/>s, world-space or screen-space alike.
    ///
    /// When <c>exclusive</c> is on (the default), the toggles are mutually exclusive: when one opens,
    /// every other toggle in the group that is currently open gets closed. Turn it off to let several
    /// stay open and use the group only to close them all on an outside tap.
    ///
    /// The toggles come from the serialized <c>toggles</c> list. If the list is left empty, the group
    /// collects every <see cref="UIToolkitToggleElement"/> in its children instead, so it can simply
    /// sit on their parent.
    ///
    /// Only toggles that are actually open are closed, so <c>onClose</c> never fires on a toggle that
    /// was already closed.
    ///
    /// Optionally closes every toggle on a tap outside all of them (empty space, the scene, or any
    /// other UI). A press is "inside" when one of the toggles reports it through
    /// <see cref="UIToolkitToggleElement.PressedInside"/> — the same UI Toolkit picking that delivers
    /// the clicks, so mouse, touch and XR ray behave alike. Only a real tap closes: a drag (e.g.
    /// looking around a skybox on mobile or with the mouse) leaves the toggles open.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIToolkitToggleGroup : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("pois"), Tooltip("Toggles in this group. If empty, every " +
            "UIToolkitToggleElement in the children of this object is used.")]
        private List<UIToolkitToggleElement> toggles = new();

        [SerializeField, Tooltip("When a toggle opens, close every other open toggle of the group.")]
        private bool exclusive = true;

        [SerializeField, Tooltip("Close every open toggle when the user taps outside all of them " +
            "(empty space, the scene or any other UI).")]
        private bool closeOnOutsideTap = true;

        [SerializeField, Tooltip("Mouse / touch: maximum movement, in screen pixels, between press and " +
            "release for it to count as a tap. Beyond this it is a drag (e.g. looking around) and the " +
            "toggles stay open.")]
        private float maxTapDistance = 25f;

        [SerializeField, Tooltip("XR controllers (no screen position): maximum press duration, in " +
            "seconds, for it to count as a tap.")]
        private float maxTapDuration = 0.4f;

        // Press on any pointer (mouse, pen, touchscreen) or on an XR controller trigger.
        private InputAction pressAction;

        private int pressFrame = -1;
        private double pressTime;
        private Vector2 pressPosition;
        private bool pressHasPosition;

        // Frame of the last press reported by a toggle, and of a tap still waiting to be evaluated.
        private int lastInsideFrame = -1;
        private int pendingTapFrame = -1;

        private void Awake()
        {
            if (toggles.Count == 0)
            {
                GetComponentsInChildren(true, toggles);
            }
        }

        private void OnEnable()
        {
            foreach (UIToolkitToggleElement toggle in toggles)
            {
                if (toggle != null)
                {
                    toggle.Opened += OnToggleOpened;
                    toggle.PressedInside += OnTogglePressedInside;
                }
            }

            pressAction = new InputAction("ToggleGroupOutsideTap", InputActionType.Button);
            pressAction.AddBinding("<Pointer>/press");
            pressAction.AddBinding("<XRController>/{TriggerButton}");
            pressAction.started += OnPressStarted;
            pressAction.canceled += OnPressReleased;
            pressAction.Enable();
        }

        private void OnDisable()
        {
            foreach (UIToolkitToggleElement toggle in toggles)
            {
                if (toggle != null)
                {
                    toggle.Opened -= OnToggleOpened;
                    toggle.PressedInside -= OnTogglePressedInside;
                }
            }

            if (pressAction != null)
            {
                pressAction.started -= OnPressStarted;
                pressAction.canceled -= OnPressReleased;
                pressAction.Dispose();
                pressAction = null;
            }
            pendingTapFrame = -1;
        }

        private void OnToggleOpened(UIToolkitToggleElement opened)
        {
            if (!exclusive)
            {
                return;
            }
            foreach (UIToolkitToggleElement toggle in toggles)
            {
                if (toggle != null && toggle != opened && toggle.IsOpen)
                {
                    toggle.Close();
                }
            }
        }

        private void OnTogglePressedInside(UIToolkitToggleElement _) => lastInsideFrame = Time.frameCount;

        private void OnPressStarted(InputAction.CallbackContext context)
        {
            pressFrame = Time.frameCount;
            pressTime = context.time;
            // Forget the previous press; the input callbacks run before the EventSystem dispatches
            // this new press to the toggles, so their report for it still arrives after this reset.
            lastInsideFrame = -1;
            pressHasPosition = context.control.device is Pointer;
            pressPosition = pressHasPosition ? ((Pointer)context.control.device).position.ReadValue() : default;
        }

        private void OnPressReleased(InputAction.CallbackContext context)
        {
            if (!closeOnOutsideTap || pressFrame < 0)
            {
                return;
            }

            bool isTap;
            if (pressHasPosition && context.control.device is Pointer pointer)
            {
                isTap = Vector2.Distance(pointer.position.ReadValue(), pressPosition) <= maxTapDistance;
            }
            else
            {
                isTap = context.time - pressTime <= maxTapDuration;
            }

            if (isTap)
            {
                // Decide later: the UI may not have dispatched this press to the toggles yet (a quick
                // tap can press and release within a single input update).
                pendingTapFrame = Time.frameCount;
            }
        }

        private void LateUpdate()
        {
            // Evaluate one frame after the release, once the EventSystem has surely run.
            if (pendingTapFrame < 0 || Time.frameCount <= pendingTapFrame)
            {
                return;
            }
            pendingTapFrame = -1;

            // A toggle reported the press (on its trigger or target) somewhere between press and release.
            bool pressedInside = lastInsideFrame >= 0;
            if (!pressedInside)
            {
                CloseAll();
            }
        }

        /// <summary>Closes every open toggle of the group.</summary>
        public void CloseAll()
        {
            foreach (UIToolkitToggleElement toggle in toggles)
            {
                if (toggle != null && toggle.IsOpen)
                {
                    toggle.Close();
                }
            }
        }
    }
}
