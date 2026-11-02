using System;

using UnityEngine;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// What every SDK UI Toolkit scene panel answers to, so the scripting API can drive one through its
    /// GameObject (<c>IUIApi.ShowPanel</c>, <c>HidePanel</c>, <c>SetPanelText</c>).
    /// </summary>
    public interface IScenePanel
    {
        void Show();

        void Hide();

        /// <summary>Sets the panel's texts as written; a null argument leaves that text as it is.</summary>
        void SetTexts(string title, string text);
    }

    /// <summary>
    /// One event for the buttons of every SDK panel, for the scripting API: a script cannot name the
    /// panel types to subscribe to each one.
    /// </summary>
    public static class PanelEvents
    {
        /// <summary>A button was pressed: the panel's GameObject and the button's index.</summary>
        public static event Action<GameObject, int> ButtonPressed;

        public static void RaiseButtonPressed(Component panel, int index)
        {
            if (panel != null)
            {
                ButtonPressed?.Invoke(panel.gameObject, index);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReloadOff() => ButtonPressed = null;
    }
}
