namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    /// <summary>
    /// Handler of <see cref="Placeholders.InteractablePlaceholder.OnBlockedChanged"/>: <paramref name="blocked"/> is the
    /// new value of <see cref="Placeholders.InteractablePlaceholder.IsBlocked"/>.
    /// </summary>
    public delegate void InteractableBlockedChangedHandler(bool blocked);
}
