using System;

using Virtuademy.Environments.ScriptingApi.Interaction;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// What the player currently has hold of.
    /// </summary>
    /// <remarks>
    /// The interaction contracts moved into this assembly to make this group possible. They used to
    /// sit under <c>Virtuademy.SDK</c>, which the script whitelist denies by prefix, so a member
    /// naming one was a member no interpreted script could call — the reason this signal was the
    /// last thing left on the flat facade.
    /// </remarks>
    public interface IInteractionApi
    {
        /// <summary>
        /// The interactable the player just selected, or null when they deselected without
        /// selecting anything else.
        /// </summary>
        /// <remarks>
        /// Node: <c>Virtuademy Visual Scripting Interactable: On Selected Change</c>.
        /// </remarks>
        event Action<IVisualScriptingInteractable> SelectedChanged;
    }
}
