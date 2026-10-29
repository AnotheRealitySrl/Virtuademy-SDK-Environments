using System;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// What an SDK panel needs to know about where it runs: the platform and the translated texts.
    /// Both answer sensibly in an authoring project played without the application.
    /// </summary>
    public static class PanelEnvironment
    {
        // The application's scripting surface when it is installed (2026-10-29, review B3: availability checks, not
        // catch-all blocks; an exception from the application's implementation is a bug and surfaces)
        private static IVirtuademyGameplay Gameplay => IVirtuademyGameplay.IsAvailable ? IVirtuademyGameplay.Current : null;

        /// <summary>Running in a headset. Without the application, whether an XR device is active.</summary>
        public static bool IsVR => Gameplay?.Platform != null ? Gameplay.Platform.IsVR : XRSettings.isDeviceActive;

        /// <summary>Running on a phone or tablet. Without the application, Unity's own answer.</summary>
        public static bool IsMobile => Gameplay?.Platform != null ? Gameplay.Platform.IsMobile : Application.isMobilePlatform;

        /// <summary>
        /// Whether the application's help panel (its tutorial) is open, opening or closing. False
        /// outside the application or when it has no help panel.
        /// </summary>
        public static bool IsHelpOpen
        {
            get
            {
                IHelpApi help = Gameplay?.Help;
                return help != null && help.IsAvailable && help.IsOpen;
            }
        }

        /// <summary>
        /// Subscribes to the help panel opening and closing when the application has one, and returns whether it did.
        /// Pair it with <see cref="UnsubscribeHelp"/>.
        /// </summary>
        public static bool SubscribeHelp(Action opened, Action closed)
        {
            IHelpApi help = Gameplay?.Help;
            if (help == null || !help.IsAvailable)
            {
                return false;
            }
            help.Opened += opened;
            help.Closed += closed;
            return true;
        }

        public static void UnsubscribeHelp(Action opened, Action closed)
        {
            IHelpApi help = Gameplay?.Help;
            if (help == null)
            {
                return;
            }
            help.Opened -= opened;
            help.Closed -= closed;
        }

        /// <summary>
        /// The text to show for a localization key with a fallback: the translation when the
        /// environment's Localization File has one, else <paramref name="fallback"/>. An empty key
        /// means "no localization": the fallback is shown as written.
        /// </summary>
        public static string Text(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback ?? string.Empty;
            }
            ILocalizationApi localization = Gameplay?.Localization;
            if (localization != null && localization.IsAvailable)
            {
                return localization.TranslateOr(key, fallback ?? key);
            }
            // No localization system: show what the author wrote.
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to language changes when a localization system is
        /// there, and returns whether it did. Pair it with <see cref="UnsubscribeLanguage"/>.
        /// </summary>
        public static bool SubscribeLanguage(Action<string> handler)
        {
            ILocalizationApi localization = Gameplay?.Localization;
            if (localization == null)
            {
                return false;
            }
            localization.LanguageChanged += handler;
            return true;
        }

        public static void UnsubscribeLanguage(Action<string> handler)
        {
            ILocalizationApi localization = Gameplay?.Localization;
            if (localization != null)
            {
                localization.LanguageChanged -= handler;
            }
        }

        /// <summary>Sets <paramref name="element"/> and every descendant to <see cref="PickingMode.Ignore"/>.</summary>
        /// <remarks>
        /// World-space picking rule: only controls are pickable. Call it on a subtree created from
        /// code, then re-enable picking on the controls (a <see cref="Button"/> created in code is
        /// left pickable by <see cref="IgnoreExceptControls"/>).
        /// </remarks>
        public static void IgnoreExceptControls(VisualElement element)
        {
            if (element == null)
            {
                return;
            }
            if (!(element is Button))
            {
                element.pickingMode = PickingMode.Ignore;
            }
            foreach (VisualElement child in element.Children())
            {
                IgnoreExceptControls(child);
            }
        }
    }
}
