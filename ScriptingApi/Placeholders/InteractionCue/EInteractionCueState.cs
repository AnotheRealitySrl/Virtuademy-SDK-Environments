namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// The state an <see cref="InteractionCuePlaceholder"/> shows. Its <see cref="InteractionCuePlaceholder.State"/> is
    /// the first that applies in this order: <see cref="Locked"/>, then <see cref="Right"/> or <see cref="Wrong"/>
    /// (for the feedback time), <see cref="Completed"/>, <see cref="Selected"/>, <see cref="Hover"/>, and last the base
    /// state (<see cref="Available"/> or <see cref="NextTarget"/>).
    /// </summary>
    public enum EInteractionCueState
    {
        /// <summary>Nothing to signal: no rim.</summary>
        Available = 0,
        /// <summary>The object is the next thing to act on: a rim that pulses (or is fixed), and the arrow if on.</summary>
        NextTarget = 1,
        /// <summary>The local user hovers the object.</summary>
        Hover = 2,
        /// <summary>The local user selected the object.</summary>
        Selected = 3,
        /// <summary>A right action or answer: green, for the feedback time.</summary>
        Right = 4,
        /// <summary>A wrong action or answer: red, for the feedback time.</summary>
        Wrong = 5,
        /// <summary>Done: no rim, no arrow, no hover or selection rim; the object stays clickable unless the cue blocks
        /// it (Block Interaction When Completed).</summary>
        Completed = 6,
        /// <summary>The object cannot be used now: no rim, no hover.</summary>
        Locked = 7,
    }
}
