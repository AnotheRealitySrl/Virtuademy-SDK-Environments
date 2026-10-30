using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.InteractionCue
{
    /// <summary>Resolves a node's cue input: no cue is the graph's error, not something to skip (review S8).</summary>
    internal static class InteractionCueNodeInput
    {
        public static InteractionCuePlaceholder Get(Flow flow, ValueInput input, string node)
        {
            InteractionCuePlaceholder cue = flow.GetValue<InteractionCuePlaceholder>(input);
            if (cue == null)
            {
                throw new System.ArgumentNullException("Cue", "[" + node + "] no InteractionCuePlaceholder: connect one, or " +
                    "put the graph on the object that has it");
            }
            return cue;
        }
    }

    /// <summary>The cue's state: <see cref="InteractionCuePlaceholder.State"/> and the values it is resolved from.</summary>
    [UnitTitle("Virtuademy Interaction Cue: Get Cue State")]
    [UnitSurtitle("Interaction Cue")]
    [UnitShortTitle("Get Cue State")]
    [UnitCategory("Virtuademy\\Interaction Cue")]
    public class GetCueStateNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Cue { get; private set; }

        [DoNotSerialize] public ValueOutput State { get; private set; }
        [DoNotSerialize] public ValueOutput BaseState { get; private set; }
        [DoNotSerialize] public ValueOutput IsHovered { get; private set; }
        [DoNotSerialize] public ValueOutput IsSelected { get; private set; }
        [DoNotSerialize] public ValueOutput IsLocked { get; private set; }

        protected override void Definition()
        {
            Cue = ValueInput<InteractionCuePlaceholder>(nameof(Cue), null).NullMeansSelf();
            State = ValueOutput(nameof(State), f => InteractionCueNodeInput.Get(f, Cue, nameof(GetCueStateNode)).State);
            BaseState = ValueOutput(nameof(BaseState), f => InteractionCueNodeInput.Get(f, Cue, nameof(GetCueStateNode)).BaseState);
            IsHovered = ValueOutput(nameof(IsHovered), f => InteractionCueNodeInput.Get(f, Cue, nameof(GetCueStateNode)).IsHovered);
            IsSelected = ValueOutput(nameof(IsSelected), f => InteractionCueNodeInput.Get(f, Cue, nameof(GetCueStateNode)).IsSelected);
            IsLocked = ValueOutput(nameof(IsLocked), f => InteractionCueNodeInput.Get(f, Cue, nameof(GetCueStateNode)).IsLocked);
            Requirement(Cue, State);
            Requirement(Cue, BaseState);
            Requirement(Cue, IsHovered);
            Requirement(Cue, IsSelected);
            Requirement(Cue, IsLocked);
        }
    }

    /// <summary>Sets the cue's base state (<see cref="InteractionCuePlaceholder.SetBaseState"/>): Available, Next Target
    /// or Completed. This client only.</summary>
    [UnitTitle("Virtuademy Interaction Cue: Set Cue State")]
    [UnitSurtitle("Interaction Cue")]
    [UnitShortTitle("Set Cue State")]
    [UnitCategory("Virtuademy\\Interaction Cue")]
    public class SetCueStateNode : Unit
    {
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Cue { get; private set; }

        [DoNotSerialize] public ValueInput State { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                InteractionCueNodeInput.Get(flow, Cue, nameof(SetCueStateNode))
                    .SetBaseState(flow.GetValue<EInteractionCueState>(State));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Cue = ValueInput<InteractionCuePlaceholder>(nameof(Cue), null).NullMeansSelf();
            State = ValueInput(nameof(State), EInteractionCueState.NextTarget);
            Requirement(Cue, InputTrigger);
            Requirement(State, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>Locks or unlocks the cue (<see cref="InteractionCuePlaceholder.SetLocked"/>). This client only.</summary>
    [UnitTitle("Virtuademy Interaction Cue: Set Cue Locked")]
    [UnitSurtitle("Interaction Cue")]
    [UnitShortTitle("Set Cue Locked")]
    [UnitCategory("Virtuademy\\Interaction Cue")]
    public class SetCueLockedNode : Unit
    {
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Cue { get; private set; }

        [DoNotSerialize] public ValueInput Locked { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                InteractionCueNodeInput.Get(flow, Cue, nameof(SetCueLockedNode)).SetLocked(flow.GetValue<bool>(Locked));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Cue = ValueInput<InteractionCuePlaceholder>(nameof(Cue), null).NullMeansSelf();
            Locked = ValueInput(nameof(Locked), true);
            Requirement(Cue, InputTrigger);
            Requirement(Locked, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    /// <summary>Shows a right or wrong action on the cue (<see cref="InteractionCuePlaceholder.ShowResult"/>): to every
    /// user of the room when the cue is networked.</summary>
    [UnitTitle("Virtuademy Interaction Cue: Show Cue Feedback")]
    [UnitSurtitle("Interaction Cue")]
    [UnitShortTitle("Show Cue Feedback")]
    [UnitCategory("Virtuademy\\Interaction Cue")]
    public class ShowCueFeedbackNode : Unit
    {
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Cue { get; private set; }

        [DoNotSerialize] public ValueInput Right { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                InteractionCueNodeInput.Get(flow, Cue, nameof(ShowCueFeedbackNode)).ShowResult(flow.GetValue<bool>(Right));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Cue = ValueInput<InteractionCuePlaceholder>(nameof(Cue), null).NullMeansSelf();
            Right = ValueInput(nameof(Right), true);
            Requirement(Cue, InputTrigger);
            Requirement(Right, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }
}
