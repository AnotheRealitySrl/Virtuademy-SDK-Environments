namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    /// <summary>
    /// What the user interacted with an element through.
    /// </summary>
    public enum EInteractionControllerType
    {
        /// <summary>
        /// Not caused by an input in the same frame: a script, a node or the application, the object
        /// being destroyed, or a selection change that had to wait for a previous one to finish.
        /// </summary>
        None = 0,
        /// <summary>The mouse pointer (desktop and WebGL on a computer).</summary>
        Mouse = 1,
        /// <summary>A controller's ray (VR).</summary>
        Ray = 2,
        /// <summary>A hand touching the element (VR).</summary>
        Hand = 3,
        /// <summary>A finger on the touch screen (mobile).</summary>
        Touch = 4,
    }

    /// <summary>
    /// The hand behind an interaction. Only VR has one: the mouse and the touch screen say
    /// <see cref="None"/>.
    /// </summary>
    public enum EInteractionHand
    {
        None = 0,
        Left = 1,
        Right = 2,
    }

    /// <summary>
    /// Carried by the interaction events of <see cref="Placeholders.InteractablePlaceholder"/>:
    /// which controller, and which hand, caused the event.
    /// </summary>
    /// <remarks>
    /// A class rather than a struct on purpose: an <c>Action</c> of a value type declared here
    /// would be a generic instance the player never compiled, which an interpreted script cannot
    /// call.
    /// </remarks>
    public sealed class InteractionEventArgs
    {
        /// <summary>An event with no controller and no hand.</summary>
        public static readonly InteractionEventArgs None = new(EInteractionControllerType.None, EInteractionHand.None);

        public InteractionEventArgs(EInteractionControllerType controllerType, EInteractionHand hand)
        {
            ControllerType = controllerType;
            Hand = hand;
        }

        /// <summary>The controller the user interacted through.</summary>
        public EInteractionControllerType ControllerType { get; }

        /// <summary>The hand holding that controller, in VR; <see cref="EInteractionHand.None"/> elsewhere.</summary>
        public EInteractionHand Hand { get; }

        public override string ToString() => Hand == EInteractionHand.None ? ControllerType.ToString() : $"{ControllerType} ({Hand})";
    }
}
