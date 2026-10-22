namespace Virtuademy.Environments.ScriptingApi.UI
{
    /// <summary>
    /// Called once when a popup closes, with the index of the button that closed it in the array
    /// passed to <see cref="IUIApi.ShowPopup"/>, or -1 when it was closed without one (another popup
    /// replaced it, the scene unloaded, <see cref="IUIApi.ClosePopup"/> was called).
    /// </summary>
    /// <remarks>
    /// A named delegate rather than <c>Action&lt;int&gt;</c>: a generic instantiated over a value type
    /// from interpreted code needs AOT metadata the player may not carry, and this needs none.
    /// </remarks>
    public delegate void PopupClosedHandler(int buttonIndex);
}
