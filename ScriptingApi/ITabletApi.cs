using System;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// The application's tablet: the panel the user opens for files, settings and the session, on
    /// desktop and in VR (mobile has none). Read-only: a world can tell when the tablet is in front
    /// of the user, to keep its own panels out of the way or pause what it is doing; opening and
    /// closing it is the user's.
    /// </summary>
    public interface ITabletApi
    {
        /// <summary>Whether this application has a tablet. False on mobile and outside the application.</summary>
        bool IsAvailable { get; }

        /// <summary>Whether the tablet is open now.</summary>
        /// <remarks>Node: <c>Virtuademy Tablet: Is Tablet Open</c>.</remarks>
        bool IsOpen { get; }

        /// <summary>Raised when the tablet opens.</summary>
        /// <remarks>Node: <c>Virtuademy Tablet: On Tablet Opened</c>.</remarks>
        event Action Opened;

        /// <summary>Raised when the tablet closes.</summary>
        /// <remarks>Node: <c>Virtuademy Tablet: On Tablet Closed</c>.</remarks>
        event Action Closed;
    }
}
