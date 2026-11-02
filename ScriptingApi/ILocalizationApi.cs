using System;

using System.Collections.Generic;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// The active language, and the authored strings behind it.
    /// </summary>
    public interface ILocalizationApi
    {
        /// <summary>The language currently in use, by name.</summary>
        /// <remarks>Node: <c>Virtuademy Localization: Get Localization Data</c>.</remarks>
        string CurrentLanguage { get; }

        /// <summary>The language currently in use, as its code.</summary>
        string CurrentLanguageCode { get; }

        /// <summary>
        /// The language in force before the last change. Until a change happens it is the language
        /// that was current when the world became ready (the application records it then), not
        /// empty. The node that reports a language change publishes it beside the new one, so a world
        /// can say what it is switching from.
        /// </summary>
        string PreviousLanguage { get; }

        /// <summary>The same, as the code rather than the display name.</summary>
        string PreviousLanguageCode { get; }

        /// <summary>
        /// Every language the host's localization database knows, as display names: all of them,
        /// not only the set the tenant offers in its settings panel.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Localization: Get Localization Data</c>.</remarks>
        List<string> AvailableLanguages { get; }

        /// <summary>
        /// The translation authored for <paramref name="key"/> in the current language. Returns the
        /// key itself when nothing is authored for it, which is what makes a missing string visible
        /// in the world instead of silently empty.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Localization: Get translation</c>.</remarks>
        string Translate(string key);

        /// <summary>
        /// The translation authored for <paramref name="key"/> in the current language, or
        /// <paramref name="fallback"/> when the environment's Localization File has none for it (no
        /// row, an empty cell in every language, or no localization system at all). Use it for every
        /// text the user reads, with the fallback in the environment's main language, so an
        /// outdated Localization File shows a sentence instead of a raw key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A row with an empty cell for the current language falls back to another language first,
        /// as <see cref="Translate"/> does (the order of the application's language list), and only
        /// then to <paramref name="fallback"/>.
        /// </para>
        /// <para>Node: <c>Virtuademy Localization: Translate Or</c>.</para>
        /// </remarks>
        string TranslateOr(string key, string fallback);

        /// <summary>
        /// Switches the language for the whole application, not only for this world. It takes a
        /// language name of the application's localization database (<c>Italian</c>, as
        /// <see cref="AvailableLanguages"/> lists them), not a code: a value the database does not
        /// know is ignored, with an error in the log. The tenant's own set of languages is not
        /// checked here.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Localization: Set Language</c>.</remarks>
        void SetLanguage(string language);

        /// <summary>
        /// Raised after the language changes. Subscribe to re-read anything a script has already
        /// translated and cached, reading <see cref="CurrentLanguage"/> for the new language.
        /// </summary>
        /// <remarks>
        /// The argument is the language in force <b>before</b> the change (the application passes its
        /// previous language), not the new one.
        /// <para>Node: <c>Virtuademy Localization: On Language Changed</c>.</para>
        /// </remarks>
        event Action<string> LanguageChanged;
    
        /// <summary>
        /// Whether the host application has a localization system at all. False in a world opened
        /// straight from the editor, where <see cref="Translate"/> hands the key back unchanged.
        /// </summary>
        /// <remarks>
        /// Node: <c>Virtuademy Localization: On Language Changed</c> subscribes to
        /// <see cref="LanguageChanged"/> whatever this says, since the system may come up after
        /// the graph does.
        /// </remarks>
        bool IsAvailable { get; }
}
}
