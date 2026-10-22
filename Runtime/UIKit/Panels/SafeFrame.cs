using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Keeps a screen-space UI Toolkit panel clear of the screen zones the application keeps for its
    /// own UI (<see cref="ReservedScreenZones"/>) and of the device's safe area (notch, rounded
    /// corners, home indicator).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It insets one element, by default the first with the class <c>vd-safe-frame</c>, with inline
    /// <c>left/right/top/bottom</c>: lay the panel's content out inside that element and it never
    /// sits under the top bar, the bottom bar, the joystick or the task HUD. The insets are the
    /// larger of the application's zones and the device's safe area, recomputed when either changes.
    /// </para>
    /// <para>
    /// World-space documents (and every document in VR) are left alone: they have no screen edge.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Safe Frame")]
    public class SafeFrame : UIDocumentBinder
    {
        public enum ETaskHud
        {
            /// <summary>Keep clear of the task HUD when the scene has a TaskUIPlaceholder.</summary>
            Auto = 0,
            Always = 1,
            Never = 2,
        }

        [SerializeField, Tooltip("Name of the element to inset. Empty: the first element with the class vd-safe-frame.")]
        private string targetElementName;

        [SerializeField, Tooltip("Also keep clear of the task HUD on desktop and WebGL.")]
        private ETaskHud taskHud = ETaskHud.Auto;

        [SerializeField, Tooltip("Extra margin inside the frame, in reference pixels, on every side.")]
        private float margin = 16f;

        private VisualElement target;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private bool dirty = true;

        /// <summary>The insets in use, in panel pixels.</summary>
        public ReservedScreenZones.Insets Current { get; private set; }

        protected override bool BindTo(VisualElement root)
        {
            target = string.IsNullOrEmpty(targetElementName)
                ? root.Q<VisualElement>(className: ThemeTokens.SafeFrameClass)
                : root.Q<VisualElement>(targetElementName);
            if (target == null)
            {
                Debug.LogWarning($"[{nameof(SafeFrame)}] '{name}': no element to inset (name '{targetElementName}', " +
                    $"or the class {ThemeTokens.SafeFrameClass}).", this);
                return false;
            }
            ReservedScreenZones.Changed += MarkDirty;
            WatchForRebuild(target);
            dirty = true;
            return true;
        }

        protected override void UnbindFromTree()
        {
            ReservedScreenZones.Changed -= MarkDirty;
            target = null;
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (target == null || Document == null || Document.panelSettings == null)
            {
                return;
            }
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            {
                dirty = true;
            }
            if (!dirty)
            {
                return;
            }
            dirty = false;
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            Apply();
        }

        private void Apply()
        {
            bool screenSpace = Document.panelSettings.renderMode == PanelRenderMode.ScreenSpaceOverlay;
            if (!screenSpace || PanelEnvironment.IsVR || target.panel == null)
            {
                Current = default;
                SetInsets(default);
                return;
            }

            ReservedScreenZones.Insets reserved;
            if (PanelEnvironment.IsMobile)
            {
                reserved = ReservedScreenZones.Mobile;
            }
            else
            {
                reserved = ReservedScreenZones.Desktop;
                bool hud = taskHud == ETaskHud.Always
                    || (taskHud == ETaskHud.Auto && FindFirstObjectByType<TaskUIPlaceholder>() != null);
                if (hud)
                {
                    ReservedScreenZones.Insets add = ReservedScreenZones.DesktopTaskHud;
                    reserved = new ReservedScreenZones.Insets(reserved.top + add.top, reserved.bottom + add.bottom,
                        reserved.left + add.left, reserved.right + add.right);
                }
            }

            // The device safe area, from screen pixels (origin bottom-left) to panel pixels (top-left).
            IPanel panel = target.panel;
            Rect safe = Screen.safeArea;
            Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));
            Vector2 full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            var device = new ReservedScreenZones.Insets(topLeft.y, full.y - bottomRight.y, topLeft.x, full.x - bottomRight.x);

            ReservedScreenZones.Insets insets = ReservedScreenZones.Insets.Max(reserved, device);
            insets = new ReservedScreenZones.Insets(insets.top + margin, insets.bottom + margin,
                insets.left + margin, insets.right + margin);
            Current = insets;
            SetInsets(insets);
        }

        private void SetInsets(ReservedScreenZones.Insets insets)
        {
            target.style.position = Position.Absolute;
            target.style.top = insets.top;
            target.style.bottom = insets.bottom;
            target.style.left = insets.left;
            target.style.right = insets.right;
        }
    }
}
