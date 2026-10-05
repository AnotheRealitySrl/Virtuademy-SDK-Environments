using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Makes a set of <see cref="WorldSpacePOIToggle"/> mutually exclusive: when one POI opens, every
    /// other POI in the group that is currently open gets closed.
    ///
    /// The POIs come from the serialized <c>pois</c> list. If the list is left empty, the group
    /// collects every <see cref="WorldSpacePOIToggle"/> in its children instead, so it can simply sit
    /// on the parent of the POIs.
    ///
    /// Only POIs that are actually open are closed, so <c>onClose</c> never fires on a POI that was
    /// already closed.
    ///
    /// Optionally closes every POI on a tap outside all of them (empty space, the scene, or any other
    /// UI). A press is "inside" when one of the POIs reports it through
    /// <see cref="WorldSpacePOIToggle.PressedInside"/> — the same UI Toolkit picking that delivers the
    /// clicks, so mouse, touch and XR ray behave alike. Only a real tap closes: a drag (e.g. looking
    /// around the skybox on mobile or with the mouse) leaves the POIs open.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldSpacePOIGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("POIs in this group. If empty, every WorldSpacePOIToggle in the " +
            "children of this object is used.")]
        private List<WorldSpacePOIToggle> pois = new();

        [SerializeField, Tooltip("Close every open POI when the user taps outside all of them " +
            "(empty space, the scene or any other UI).")]
        private bool closeOnOutsideTap = true;

        [SerializeField, Tooltip("Mouse / touch: maximum movement, in screen pixels, between press and " +
            "release for it to count as a tap. Beyond this it is a drag (e.g. looking around) and the " +
            "POIs stay open.")]
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

        // Frame of the last press reported by a POI, and of a tap still waiting to be evaluated.
        private int lastInsideFrame = -1;
        private int pendingTapFrame = -1;

        private void Awake()
        {
            if (pois.Count == 0)
            {
                GetComponentsInChildren(true, pois);
            }
        }

        private void OnEnable()
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null)
                {
                    poi.Opened += OnPOIOpened;
                    poi.PressedInside += OnPOIPressedInside;
                }
            }

            pressAction = new InputAction("POIOutsideTap", InputActionType.Button);
            pressAction.AddBinding("<Pointer>/press");
            pressAction.AddBinding("<XRController>/{TriggerButton}");
            pressAction.started += OnPressStarted;
            pressAction.canceled += OnPressReleased;
            pressAction.Enable();
        }

        private void OnDisable()
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null)
                {
                    poi.Opened -= OnPOIOpened;
                    poi.PressedInside -= OnPOIPressedInside;
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

        private void OnPOIOpened(WorldSpacePOIToggle opened)
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null && poi != opened && poi.IsOpen)
                {
                    poi.Close();
                }
            }
        }

        private void OnPOIPressedInside(WorldSpacePOIToggle _) => lastInsideFrame = Time.frameCount;

        private void OnPressStarted(InputAction.CallbackContext context)
        {
            pressFrame = Time.frameCount;
            pressTime = context.time;
            // Forget the previous press; the input callbacks run before the EventSystem dispatches
            // this new press to the POIs, so their report for it still arrives after this reset.
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
                // Decide later: the UI may not have dispatched this press to the POIs yet (a quick tap
                // can press and release within a single input update).
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

            // A POI reported the press (on its icon or panel) somewhere between press and release.
            bool pressedInside = lastInsideFrame >= 0;
            if (!pressedInside)
            {
                CloseAll();
            }
        }

        /// <summary>Closes every open POI of the group.</summary>
        public void CloseAll()
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null && poi.IsOpen)
                {
                    poi.Close();
                }
            }
        }
    }
}
