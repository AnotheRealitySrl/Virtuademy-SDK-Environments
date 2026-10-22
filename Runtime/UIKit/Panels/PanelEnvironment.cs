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
        /// <summary>Running in a headset. Without the application, whether an XR device is active.</summary>
        public static bool IsVR
        {
            get
            {
                try
                {
                    if (IVirtuademyGameplay.IsAvailable)
                    {
                        return IVirtuademyGameplay.Current.Platform.IsVR;
                    }
                }
                catch (Exception)
                {
                    // The platform system is not up yet: fall through to the device check.
                }
                return XRSettings.isDeviceActive;
            }
        }

        /// <summary>Running on a phone or tablet. Without the application, Unity's own answer.</summary>
        public static bool IsMobile
        {
            get
            {
                try
                {
                    if (IVirtuademyGameplay.IsAvailable)
                    {
                        return IVirtuademyGameplay.Current.Platform.IsMobile;
                    }
                }
                catch (Exception)
                {
                }
                return Application.isMobilePlatform;
            }
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
            try
            {
                if (IVirtuademyGameplay.IsAvailable && IVirtuademyGameplay.Current.Localization.IsAvailable)
                {
                    return IVirtuademyGameplay.Current.Localization.TranslateOr(key, fallback ?? key);
                }
            }
            catch (Exception)
            {
                // No localization system: show what the author wrote.
            }
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to language changes when a localization system is
        /// there, and returns whether it did. Pair it with <see cref="UnsubscribeLanguage"/>.
        /// </summary>
        public static bool SubscribeLanguage(Action<string> handler)
        {
            try
            {
                if (IVirtuademyGameplay.IsAvailable)
                {
                    IVirtuademyGameplay.Current.Localization.LanguageChanged += handler;
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static void UnsubscribeLanguage(Action<string> handler)
        {
            try
            {
                if (IVirtuademyGameplay.IsAvailable)
                {
                    IVirtuademyGameplay.Current.Localization.LanguageChanged -= handler;
                }
            }
            catch (Exception)
            {
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
