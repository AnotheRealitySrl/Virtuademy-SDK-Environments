using System;

using UnityEngine;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// The parts of the screen the Virtuademy application keeps for its own UI over an environment,
    /// as insets from each edge in reference pixels of a 1920×1080 screen. <see cref="SafeFrame"/> keeps
    /// environment panels inside what is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The defaults were measured from the application's code and prefabs of 2026.6 (not from
    /// screenshots), rounded up to the next 8 px:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Desktop and WebGL</b> (Nova main menu, <c>MainMenuDesktop.prefab</c>): the header,
    /// 41 px high about 44 px from the top, so 96 at the top; the bottom bar, 100 px high, takes
    /// clicks across the lowest fifth of the screen (a known issue of the application), so 216 at the
    /// bottom; 24 at the sides. With a task HUD (<c>UI_TaskHandler.prefab</c>: a summary panel 30%
    /// wide, 2% from the left edge, its top 20% down), 640 more on the left.</item>
    /// <item><b>Mobile</b> (UI Toolkit HUD, <c>HudMobile.uss</c> and <c>MobileControls.uss</c>): the
    /// top bar, 80 px high 24 px from the top right, so 112 at the top; the joystick (320 px, 48 px
    /// from the bottom-left corner) and the jump button (120 px, bottom right) take the lower
    /// corners, so 400 on each side and 48 at the bottom: a centred column runs down to the bottom
    /// edge between the two. The application shows no task HUD on mobile. The device's own safe
    /// area (notch, home indicator) is added by <see cref="SafeFrame"/>. That centred column is
    /// <see cref="Mobile"/>; a panel that belongs at an edge (the HUD) uses <see cref="MobileEdges"/>
    /// instead: 24 at the sides, 112 at the top, and 384 at the bottom, above the joystick (its top
    /// is 368 px up) and the jump button.</item>
    /// <item><b>VR</b> has no screen: the task HUD and the tablet are world objects, and an overlay
    /// panel floats in front of the user (<see cref="OverlayPanelPlacement"/>).</item>
    /// </list>
    /// <para>
    /// Nova scales part of the desktop menu with the camera's pixels rather than the 1920×1080
    /// reference, so on screens far from 1080p the desktop values are approximate. The application
    /// can publish exact values with <see cref="Publish"/>.
    /// </para>
    /// </remarks>
    public static class ReservedScreenZones
    {
        /// <summary>Insets from each edge, in reference pixels (1920×1080).</summary>
        [Serializable]
        public struct Insets
        {
            public float top;
            public float bottom;
            public float left;
            public float right;

            public Insets(float top, float bottom, float left, float right)
            {
                this.top = top;
                this.bottom = bottom;
                this.left = left;
                this.right = right;
            }

            public static Insets Max(Insets a, Insets b) => new(Mathf.Max(a.top, b.top),
                Mathf.Max(a.bottom, b.bottom), Mathf.Max(a.left, b.left), Mathf.Max(a.right, b.right));

            public override string ToString() => $"top {top}, bottom {bottom}, left {left}, right {right}";
        }

        public static readonly Insets DefaultDesktop = new(96f, 216f, 24f, 24f);
        public static readonly Insets DefaultDesktopTaskHud = new(0f, 0f, 640f, 0f);
        public static readonly Insets DefaultMobile = new(112f, 48f, 400f, 400f);
        public static readonly Insets DefaultMobileEdges = new(112f, 384f, 24f, 24f);

        private static Insets? desktop;
        private static Insets? desktopTaskHud;
        private static Insets? mobile;
        private static Insets? mobileEdges;

        /// <summary>Raised when the application publishes or clears its own values.</summary>
        public static event Action Changed;

        /// <summary>What the application keeps on desktop and WebGL.</summary>
        public static Insets Desktop => desktop ?? DefaultDesktop;

        /// <summary>What a task HUD adds on desktop and WebGL.</summary>
        public static Insets DesktopTaskHud => desktopTaskHud ?? DefaultDesktopTaskHud;

        /// <summary>What the application keeps on mobile, before the device's safe area.</summary>
        public static Insets Mobile => mobile ?? DefaultMobile;

        /// <summary>
        /// On mobile, what an edge panel keeps clear of: the bars and the corner controls, reaching
        /// the side edges between them.
        /// </summary>
        public static Insets MobileEdges => mobileEdges ?? DefaultMobileEdges;

        /// <summary>
        /// The application's own measure of its zones, replacing the defaults. Null keeps a default.
        /// </summary>
        public static void Publish(Insets? desktopInsets, Insets? desktopTaskHudInsets, Insets? mobileInsets,
                                   Insets? mobileEdgesInsets = null)
        {
            desktop = desktopInsets;
            desktopTaskHud = desktopTaskHudInsets;
            mobile = mobileInsets;
            mobileEdges = mobileEdgesInsets;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReloadOff()
        {
            desktop = null;
            desktopTaskHud = null;
            mobile = null;
            mobileEdges = null;
            Changed = null;
        }
    }
}
