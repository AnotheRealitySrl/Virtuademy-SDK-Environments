using System;

using Virtuademy.ScriptingApi;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// What the Virtuademy player provides: the avatar, the world around it, the screen it is drawn
    /// on, the panels over it, and the objects a multiplayer session keeps in step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The other half of the surface.</b> <see cref="IVirtuademyFramework"/> carries what only
    /// the platform knows — the session, the saved values, the analytics. Everything here the
    /// player owns: fading the view, the help panel, the active language, which device this is,
    /// and the world itself.
    /// </para>
    /// <para>
    /// <b>The device group is here on purpose</b>, though it looks like a platform fact. It exists
    /// because the whitelist denies <c>UnityEngine.Application</c> and <c>SystemInfo</c> to an
    /// interpreted script, not because the platform is the only one who could answer — anything
    /// else already knows its own device. It is an interpreter constraint, and those belong on the
    /// side that exists to serve the interpreter.
    /// </para>
    /// <para>
    /// <b>Two entry points, not one.</b> A creator writes <c>IVirtuademyGameplay.Current.Player</c>
    /// and <c>IVirtuademyFramework.Current.Session</c>; the split says whether an expression
    /// depends on the platform or on the player, which is the thing a shared surface would hide.
    /// </para>
    /// <para>
    /// <b>Written to the interpreter's budget</b>, like its sibling: no <c>Task</c>, no generic
    /// member, no <c>Nullable&lt;T&gt;</c>, and nothing from a namespace the whitelist refuses.
    /// </para>
    /// </remarks>
    public interface IVirtuademyGameplay
    {
        private static IVirtuademyGameplay current;

        /// <summary>
        /// Whether the world runtime is behind this surface. False in an authoring project, where
        /// the contracts compile but nothing answers them.
        /// </summary>
        static bool IsAvailable => current != null;

        /// <summary>The world this code is running in.</summary>
        /// <exception cref="InvalidOperationException">
        /// Nothing has installed an implementation. Check <see cref="IsAvailable"/> first if the
        /// code can run outside a world.
        /// </exception>
        static IVirtuademyGameplay Current => current ?? throw new InvalidOperationException(
            "No Virtuademy world runtime is installed, so this code cannot reach the world around "
            + "it. A Virtuademy application installs one before the first scene loads; check "
            + "IVirtuademyGameplay.IsAvailable if it can run outside one.");

        /// <summary>Raised when an implementation is installed, for code that initialised first.</summary>
        static event Action Installed;

        /// <summary>
        /// Registers the application's implementation. Internal by design: a script references this
        /// assembly in full, and a public installer would let one script replace the surface every
        /// other script is calling.
        /// </summary>
        internal static void Install(IVirtuademyGameplay gameplay)
        {
            current = gameplay ?? throw new ArgumentNullException(nameof(gameplay));

            Installed?.Invoke();
        }

        /// <summary>The local player: where they are, what they can do, what is visible.</summary>
        IPlayerApi Player { get; }

        /// <summary>The world around the script: placeholders, spawned objects, transitions.</summary>
        ISceneApi Scene { get; }

        /// <summary>The player's tools and the feedback they produce.</summary>
        IToolsApi Tools { get; }

        /// <summary>What the player currently has hold of.</summary>
        IInteractionApi Interaction { get; }

        /// <summary>The active language and the strings authored against it.</summary>
        ILocalizationApi Localization { get; }

        /// <summary>Fading the view in and out.</summary>
        IScreenApi Screen { get; }

        /// <summary>The help panel, when the host provides one.</summary>
        IHelpApi Help { get; }

        /// <summary>Which kind of device this is running on.</summary>
        IPlatformApi Platform { get; }
    }
}
