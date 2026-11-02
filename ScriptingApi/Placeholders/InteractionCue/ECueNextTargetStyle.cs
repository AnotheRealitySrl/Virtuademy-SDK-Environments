namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How an <see cref="InteractionCuePlaceholder"/> shows <see cref="EInteractionCueState.NextTarget"/>.</summary>
    public enum ECueNextTargetStyle
    {
        /// <summary>A rim that pulses once per second.</summary>
        Pulse = 0,
        /// <summary>A fixed rim.</summary>
        Fixed = 1,
        /// <summary>No rim (the arrow still shows if on).</summary>
        None = 2,
    }
}
