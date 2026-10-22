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
        /// <para>
        /// Node: <c>Virtuademy Visual Scripting Interactable: On Selected Change</c>.
        /// </para>
        /// <para>
        /// It is raised only when the selection changes, so it is not an event per click: a click
        /// on the object already selected does not raise it, and neither does a click on an object
        /// whose Visual Scripting Interactable does not allow the Selected state (that click
        /// interacts without selecting). To hear every click on an object, subscribe to
        /// <see cref="Placeholders.InteractablePlaceholder.OnInteract"/> and
        /// <see cref="Placeholders.InteractablePlaceholder.OnSelectEnter"/> on its placeholder.
        /// </para>
        /// </remarks>
        event Action<IVisualScriptingInteractable> SelectedChanged;
    }
}
