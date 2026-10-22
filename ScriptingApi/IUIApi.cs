using System;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// Short messages over the world, modal questions, and the colours of the theme the panels use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The toast and the popup are drawn by the environment's own notification host when its scene
    /// has one (the SDK's <c>NotificationHost</c> prefab, which a creator can restyle), and by the
    /// application's copy of the same prefab otherwise. Either way they take the theme's colours:
    /// the tenant's when the application publishes them.
    /// </para>
    /// <para>
    /// Texts are shown as given. Pass the result of <see cref="ILocalizationApi.TranslateOr"/> to
    /// show a localized one.
    /// </para>
    /// </remarks>
    public interface IUIApi
    {
        /// <summary>
        /// Whether anything can draw a toast or a popup: the application provides a notification host,
        /// or the scene has one. False in an authoring project played without the application.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Shows a short message that closes by itself. Toasts queue: at most three are on screen,
        /// newest at the bottom, and the oldest goes first.
        /// </summary>
        /// <param name="text">The message. Empty text is ignored.</param>
        /// <param name="severity">The colour of its accent bar and icon.</param>
        /// <param name="duration">Seconds on screen; zero or less uses the host's default (3 s).</param>
        /// <remarks>Node: <c>Virtuademy UI: Show Toast</c>.</remarks>
        void ShowToast(string text, EToastSeverity severity = EToastSeverity.Info, float duration = 0f);

        /// <summary>
        /// Shows a modal popup with a title, a text and up to three buttons, and reports which one
        /// closed it. Only one popup is open at a time: a new one closes the previous with -1.
        /// </summary>
        /// <param name="title">The heading; may be empty.</param>
        /// <param name="text">The body.</param>
        /// <param name="buttons">
        /// The button labels, left to right; the last one is the primary button. Null or empty shows
        /// a single "OK". Labels past the third are ignored.
        /// </param>
        /// <param name="onClosed">Called once when the popup closes; may be null.</param>
        /// <remarks>Node: <c>Virtuademy UI: Show Popup</c>.</remarks>
        void ShowPopup(string title, string text, string[] buttons, PopupClosedHandler onClosed = null);

        /// <summary>Closes the open popup, if any, reporting -1 to its handler.</summary>
        /// <remarks>Node: <c>Virtuademy UI: Close Popup</c>.</remarks>
        void ClosePopup();

        /// <summary>Whether a popup is open.</summary>
        bool IsPopupOpen { get; }

        /// <summary>
        /// The colour the theme gives <paramref name="role"/> right now: the tenant's when the
        /// application published one for that role, the SDK default otherwise. Use it to colour
        /// something that is not a themed panel, such as a material or a line renderer.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy UI: Get Theme Color</c>.</remarks>
        Color GetThemeColor(EThemeRole role);

        /// <summary>
        /// Raised when the application publishes or clears the tenant's colours. Themed panels follow
        /// by themselves; subscribe to re-apply colours a script set with <see cref="GetThemeColor"/>.
        /// </summary>
        event Action ThemeChanged;

        // ------------------------------------------------------------------ the SDK's scene panels
        //
        // The UI Toolkit panels of the SDK (VirtuademyPanel and its variants, VirtuademyListPanel,
        // VirtuademyChoicePanel, VirtuademyHud, VirtuademyLabel, VirtuademyCheckMark) are components
        // under Virtuademy.SDK, which the script whitelist denies, so a script drives them through
        // their GameObject. Each member returns false when the object carries no panel it applies to.
        // Graphs can call the components' public methods directly.

        /// <summary>Shows the panel on <paramref name="panel"/>.</summary>
        bool ShowPanel(GameObject panel);

        /// <summary>Hides the panel on <paramref name="panel"/>.</summary>
        bool HidePanel(GameObject panel);

        /// <summary>
        /// Sets the texts of a panel, as written: title and body of a panel, title and prompt of a
        /// choice panel, title of a list (body ignored), the text of a label (title ignored). A null
        /// argument leaves that text as it is.
        /// </summary>
        bool SetPanelText(GameObject panel, string title, string text);

        /// <summary>Sets the role of a <c>VirtuademyPanel</c>: its accent and icon colour.</summary>
        bool SetPanelVariant(GameObject panel, EPanelVariant variant);

        /// <summary>Sets the counter of a HUD; a <paramref name="max"/> below zero keeps the maximum.</summary>
        bool SetHudCount(GameObject hud, int count, int max = -1);

        /// <summary>Shows a hint on a HUD, as written; empty hides it.</summary>
        bool SetHudHint(GameObject hud, string hint);

        /// <summary>Marks an item of a list panel done or not done.</summary>
        bool SetListItemDone(GameObject list, int index, bool done);

        /// <summary>Shows the check mark (<paramref name="correct"/>) or the cross of a check mark panel.</summary>
        bool ShowCheckResult(GameObject checkMark, bool correct);

        /// <summary>
        /// Raised when a button of any SDK panel in the scene is pressed, with the panel's GameObject
        /// and the button's index (see <see cref="PanelButtonHandler"/>).
        /// </summary>
        event PanelButtonHandler PanelButtonPressed;
    }
}
