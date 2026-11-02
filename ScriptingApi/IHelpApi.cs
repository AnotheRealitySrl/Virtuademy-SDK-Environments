using System;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// The help panel, for a world that wants to open or close it as part of its own flow.
    /// </summary>
    public interface IHelpApi
    {
        /// <summary>
        /// Whether the application provides a help panel at all. A world can run in a host that has
        /// none, so this is worth checking before the rest.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>Opens the help panel.</summary>
        /// <remarks>Node: <c>Virtuademy Tutorial: Enable</c>.</remarks>
        void Open();

        /// <summary>Closes it.</summary>
        void Close();

        /// <summary>Raised once the panel has finished closing.</summary>
        /// <remarks>Node: <c>Virtuademy Tutorial: On Tutorial Closed</c>.</remarks>
        event Action Closed;

        /// <summary>
        /// Whether the help panel is open, or opening or closing. The application opens it by itself
        /// when a user enters an environment for the first time, and while it is open the user
        /// cannot move: a world can wait for it, or keep its own panels out of the way (the SDK's
        /// VR overlays hide while it is open).
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Tutorial: Is Tutorial Open</c>.</remarks>
        bool IsOpen { get; }

        /// <summary>Raised once the panel has finished opening.</summary>
        /// <remarks>Node: <c>Virtuademy Tutorial: On Tutorial Opened</c>.</remarks>
        event Action Opened;
    }
}
