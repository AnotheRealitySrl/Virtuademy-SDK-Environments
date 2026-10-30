namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Handler of <see cref="InteractionCuePlaceholder.CueChanged"/>: the state the cue showed and the one it shows now.
    /// </summary>
    public delegate void InteractionCueChangedHandler(InteractionCuePlaceholder cue, EInteractionCueState previous,
        EInteractionCueState current);
}
