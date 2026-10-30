using System.Collections.Generic;

using SPACS.VisualScripting;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.InteractionCue
{
    /// <summary>Raised when a cue's state changes (<see cref="InteractionCuePlaceholder.CueChanged"/>).</summary>
    [UnitTitle("Virtuademy Interaction Cue: On Cue Changed")]
    [UnitSurtitle("Interaction Cue")]
    [UnitShortTitle("On Cue Changed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnCueChangedEventUnit : InstanceDataEventUnit<OnCueChangedEventUnit.Change, InteractionCueChangedHandler>
    {
        /// <summary>One change, as the node's outputs carry it.</summary>
        public sealed class Change
        {
            public EInteractionCueState previous;
            public EInteractionCueState current;
        }

        private readonly Dictionary<GraphReference, InteractionCuePlaceholder> subscribed = new();

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Cue { get; private set; }

        [DoNotSerialize] public ValueOutput Previous { get; private set; }
        [DoNotSerialize] public ValueOutput Current { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            Cue = ValueInput<InteractionCuePlaceholder>(nameof(Cue), null).NullMeansSelf();
            Previous = ValueOutput<EInteractionCueState>(nameof(Previous));
            Current = ValueOutput<EInteractionCueState>(nameof(Current));
        }

        public override EventHook GetHook(GraphReference reference) => new("InteractionCue" + nameof(OnCueChangedEventUnit));

        protected override InteractionCueChangedHandler GetData(GraphReference instance)
        {
            return (cue, previous, current) => Trigger(instance, new Change { previous = previous, current = current });
        }

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);
            InteractionCuePlaceholder cue = Flow.New(instance).GetValue<InteractionCuePlaceholder>(Cue);
            if (cue == null)
            {
                throw new System.ArgumentNullException(nameof(Cue), "[OnCueChangedEventUnit] no InteractionCuePlaceholder: " +
                    "connect one, or put the graph on the object that has it");
            }
            cue.CueChanged += instanceData[instance];
            subscribed[instance] = cue;
        }

        public override void Uninstantiate(GraphReference instance)
        {
            if (subscribed.TryGetValue(instance, out InteractionCuePlaceholder cue))
            {
                if (cue != null)
                {
                    cue.CueChanged -= instanceData[instance];
                }
                subscribed.Remove(instance);
            }
            base.Uninstantiate(instance);
        }

        protected override void AssignArguments(Flow flow, Change args)
        {
            flow.SetValue(Previous, args.previous);
            flow.SetValue(Current, args.current);
        }
    }
}
