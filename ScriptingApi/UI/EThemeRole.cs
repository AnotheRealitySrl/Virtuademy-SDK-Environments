namespace Virtuademy.Environments.ScriptingApi.UI
{
    /// <summary>
    /// The colour roles of the Virtuademy theme. Every SDK panel colours its parts by role, never by
    /// value, so the host application can restyle a whole environment by publishing a palette.
    /// </summary>
    /// <remarks>
    /// The USS variable of each role is <c>--vd-color-</c> followed by the role in kebab case
    /// (<c>--vd-color-text-secondary</c>), and the role classes are <c>vd-bg-*</c>,
    /// <c>vd-text-*</c> and <c>vd-border-*</c> with the same suffix. Append new roles at the end:
    /// the numeric values are serialized in scenes.
    /// </remarks>
    public enum EThemeRole
    {
        /// <summary>Panel background.</summary>
        Surface = 0,

        /// <summary>Background of an element that sits on a panel: a card, a list row, a field.</summary>
        SurfaceRaised = 1,

        /// <summary>Separators and outlines on a surface.</summary>
        Border = 2,

        /// <summary>Body text on a surface.</summary>
        Text = 3,

        /// <summary>Captions, hints and other text that steps back from the body.</summary>
        TextSecondary = 4,

        /// <summary>Primary buttons, links, selection, progress: what the user acts on.</summary>
        Action = 5,

        /// <summary>Text and icons drawn on an <see cref="Action"/> background.</summary>
        OnAction = 6,

        /// <summary>Right answer, completed step.</summary>
        Success = 7,

        /// <summary>Something needs attention but nothing went wrong.</summary>
        Warning = 8,

        /// <summary>Wrong answer, failed step.</summary>
        Error = 9,

        /// <summary>Neutral information and hints.</summary>
        Info = 10,

        /// <summary>The dimming layer behind a modal popup.</summary>
        Scrim = 11,
    }
}
