namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    /// <summary>
    /// How the application delivers interaction events to an interactable's placeholder. Scripts
    /// subscribe to the events of <see cref="Placeholders.InteractablePlaceholder"/>.
    /// </summary>
    /// <remarks>
    /// A C# event can only be raised by the class that declares it, so the placeholder implements
    /// this for the application's interaction core (<c>Virtuademy.Worlds.Interaction</c>, named in
    /// <c>AssemblyInfo.cs</c>). Internal, so that a script cannot raise them.
    /// </remarks>
    internal interface IInteractableEventSink
    {
        void RaiseHoverEnter(InteractionEventArgs args);
        void RaiseHoverExit(InteractionEventArgs args);
        void RaiseSelectEnter(InteractionEventArgs args);
        void RaiseSelectExit(InteractionEventArgs args);
        void RaiseInteract(InteractionEventArgs args);
    }
}
