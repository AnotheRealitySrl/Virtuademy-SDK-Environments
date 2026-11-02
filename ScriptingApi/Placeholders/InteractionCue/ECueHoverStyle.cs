namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How an <see cref="InteractionCuePlaceholder"/> shows that the local user hovers it.</summary>
    public enum ECueHoverStyle
    {
        /// <summary>A rim.</summary>
        Rim = 0,
        /// <summary>A rim and the hover label.</summary>
        RimAndLabel = 1,
        /// <summary>Nothing: the cue ignores hover.</summary>
        None = 2,
    }
}
