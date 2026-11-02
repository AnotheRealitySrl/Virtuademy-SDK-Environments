namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// The application's network side of a networked interaction cue: sends a right or wrong action shown on this client
    /// to the other users of the room. Attached to the cue's <see cref="InteractionCueDriver"/> by the application in
    /// multiplayer; a cue without one shows its actions on this client only.
    /// </summary>
    public interface IInteractionCueNetworkBridge
    {
        /// <summary>Sends a right (true) or wrong (false) action to the others, once; they show it and never send it on.</summary>
        void SendResult(bool right);
    }
}
