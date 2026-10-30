using System;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>What an <see cref="InteractionCuePlaceholder"/> shows for a right or wrong action.</summary>
    [Flags]
    public enum ECueFeedback
    {
        /// <summary>A green or red rim.</summary>
        Rim = 1,
        /// <summary>A check or cross mark above the object.</summary>
        Mark = 2,
        /// <summary>A flash of particles (right only).</summary>
        Flash = 4,
        /// <summary>A sound.</summary>
        Sound = 8,
    }
}
