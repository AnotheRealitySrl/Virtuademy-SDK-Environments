using System.Collections.Generic;

using SPACS.VisualScripting;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Resolves a node's procedure input: no procedure is the graph's error (review S8).</summary>
    internal static class ProcedureNodeInput
    {
        public static ProcedurePlaceholder Get(Flow flow, ValueInput input, string node)
        {
            ProcedurePlaceholder procedure = flow.GetValue<ProcedurePlaceholder>(input);
            if (procedure == null)
            {
                throw new System.ArgumentNullException("Procedure", "[" + node + "] no Procedure placeholder: connect one, " +
                    "or put the graph on the task system's object");
            }
            return procedure;
        }
    }

    /// <summary>A procedure's live state.</summary>
    [UnitTitle("Virtuademy Procedure: Get Procedure State")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("Get Procedure State")]
    [UnitCategory("Virtuademy\\Procedure")]
    public class GetProcedureStateNode : Unit
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Procedure { get; private set; }
        [DoNotSerialize] public ValueOutput Score { get; private set; }
        [DoNotSerialize] public ValueOutput MaxScore { get; private set; }
        [DoNotSerialize] public ValueOutput Percent { get; private set; }
        [DoNotSerialize] public ValueOutput Errors { get; private set; }
        [DoNotSerialize] public ValueOutput HintsUsed { get; private set; }
        [DoNotSerialize] public ValueOutput ElapsedSeconds { get; private set; }
        [DoNotSerialize] public ValueOutput RemainingSeconds { get; private set; }
        [DoNotSerialize] public ValueOutput IsRunning { get; private set; }
        [DoNotSerialize] public ValueOutput IsFinished { get; private set; }

        protected override void Definition()
        {
            Procedure = ValueInput<ProcedurePlaceholder>(nameof(Procedure), null).NullMeansSelf();
            ProcedurePlaceholder P(Flow f) => ProcedureNodeInput.Get(f, Procedure, nameof(GetProcedureStateNode));
            Score = ValueOutput(nameof(Score), f => P(f).Score);
            MaxScore = ValueOutput(nameof(MaxScore), f => P(f).MaxScore);
            Percent = ValueOutput(nameof(Percent), f => P(f).Percent);
            Errors = ValueOutput(nameof(Errors), f => P(f).Errors);
            HintsUsed = ValueOutput(nameof(HintsUsed), f => P(f).HintsUsed);
            ElapsedSeconds = ValueOutput(nameof(ElapsedSeconds), f => P(f).ElapsedSeconds);
            RemainingSeconds = ValueOutput(nameof(RemainingSeconds), f => P(f).RemainingSeconds);
            IsRunning = ValueOutput(nameof(IsRunning), f => P(f).IsRunning);
            IsFinished = ValueOutput(nameof(IsFinished), f => P(f).IsFinished);
            foreach (ValueOutput output in new[] { Score, MaxScore, Percent, Errors, HintsUsed, ElapsedSeconds, RemainingSeconds, IsRunning, IsFinished })
            {
                Requirement(Procedure, output);
            }
        }
    }

    /// <summary>Runs one of the procedure's methods.</summary>
    public abstract class ProcedureActionNode : Unit
    {
        [NullMeansSelf, DoNotSerialize] public ValueInput Procedure { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected abstract void Act(ProcedurePlaceholder procedure);

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), flow =>
            {
                Act(ProcedureNodeInput.Get(flow, Procedure, GetType().Name));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Procedure = ValueInput<ProcedurePlaceholder>(nameof(Procedure), null).NullMeansSelf();
            Requirement(Procedure, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }

    [UnitTitle("Virtuademy Procedure: Finish Procedure")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("Finish Procedure")]
    [UnitCategory("Virtuademy\\Procedure")]
    public class FinishProcedureNode : ProcedureActionNode
    {
        protected override void Act(ProcedurePlaceholder procedure) => procedure.Finish();
    }

    [UnitTitle("Virtuademy Procedure: Retry Procedure")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("Retry Procedure")]
    [UnitCategory("Virtuademy\\Procedure")]
    public class RetryProcedureNode : ProcedureActionNode
    {
        protected override void Act(ProcedurePlaceholder procedure) => procedure.Retry();
    }

    [UnitTitle("Virtuademy Procedure: Request Hint")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("Request Hint")]
    [UnitCategory("Virtuademy\\Procedure")]
    public class RequestHintNode : ProcedureActionNode
    {
        protected override void Act(ProcedurePlaceholder procedure) => procedure.RequestHint();
    }

    /// <summary>Raised when the procedure finishes.</summary>
    [UnitTitle("Virtuademy Procedure: On Procedure Finished")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("On Procedure Finished")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnProcedureFinishedEventUnit : InstanceDataEventUnit<ProcedureSummary, ProcedureFinishedHandler>
    {
        private readonly Dictionary<GraphReference, ProcedurePlaceholder> subscribed = new();

        [NullMeansSelf, DoNotSerialize] public ValueInput Procedure { get; private set; }
        [DoNotSerialize] public ValueOutput Passed { get; private set; }
        [DoNotSerialize] public ValueOutput Score { get; private set; }
        [DoNotSerialize] public ValueOutput MaxScore { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            Procedure = ValueInput<ProcedurePlaceholder>(nameof(Procedure), null).NullMeansSelf();
            Passed = ValueOutput<bool>(nameof(Passed));
            Score = ValueOutput<int>(nameof(Score));
            MaxScore = ValueOutput<int>(nameof(MaxScore));
        }

        public override EventHook GetHook(GraphReference reference) => new("Procedure" + nameof(OnProcedureFinishedEventUnit));

        protected override ProcedureFinishedHandler GetData(GraphReference instance) => summary => Trigger(instance, summary);

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);
            ProcedurePlaceholder procedure = Flow.New(instance).GetValue<ProcedurePlaceholder>(Procedure);
            if (procedure == null)
            {
                throw new System.ArgumentNullException(nameof(Procedure), "[OnProcedureFinishedEventUnit] no Procedure placeholder");
            }
            procedure.Finished += instanceData[instance];
            subscribed[instance] = procedure;
        }

        public override void Uninstantiate(GraphReference instance)
        {
            if (subscribed.TryGetValue(instance, out ProcedurePlaceholder procedure) && procedure != null)
            {
                procedure.Finished -= instanceData[instance];
            }
            subscribed.Remove(instance);
            base.Uninstantiate(instance);
        }

        protected override void AssignArguments(Flow flow, ProcedureSummary summary)
        {
            flow.SetValue(Passed, summary.Outcome == EProcedureOutcome.Passed);
            flow.SetValue(Score, summary.Score);
            flow.SetValue(MaxScore, summary.MaxScore);
        }
    }

    /// <summary>Raised when the procedure's score, errors or hints change.</summary>
    [UnitTitle("Virtuademy Procedure: On Score Changed")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("On Score Changed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnProcedureScoreChangedEventUnit : InstanceDataEventUnit<GameObject, ProcedureEventHandler>
    {
        private readonly Dictionary<GraphReference, ProcedurePlaceholder> subscribed = new();

        [NullMeansSelf, DoNotSerialize] public ValueInput Procedure { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            Procedure = ValueInput<ProcedurePlaceholder>(nameof(Procedure), null).NullMeansSelf();
        }

        public override EventHook GetHook(GraphReference reference) => new("Procedure" + nameof(OnProcedureScoreChangedEventUnit));

        protected override ProcedureEventHandler GetData(GraphReference instance) => go => Trigger(instance, go);

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);
            ProcedurePlaceholder procedure = Flow.New(instance).GetValue<ProcedurePlaceholder>(Procedure);
            if (procedure == null)
            {
                throw new System.ArgumentNullException(nameof(Procedure), "[OnProcedureScoreChangedEventUnit] no Procedure placeholder");
            }
            procedure.ScoreChanged += instanceData[instance];
            subscribed[instance] = procedure;
        }

        public override void Uninstantiate(GraphReference instance)
        {
            if (subscribed.TryGetValue(instance, out ProcedurePlaceholder procedure) && procedure != null)
            {
                procedure.ScoreChanged -= instanceData[instance];
            }
            subscribed.Remove(instance);
            base.Uninstantiate(instance);
        }
    }

    /// <summary>Raised when a hint is used; outputs the task and the procedure's hints so far.</summary>
    [UnitTitle("Virtuademy Procedure: On Hint Used")]
    [UnitSurtitle("Procedure")]
    [UnitShortTitle("On Hint Used")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnProcedureHintUsedEventUnit : InstanceDataEventUnit<OnProcedureHintUsedEventUnit.Hint, ProcedureHintHandler>
    {
        /// <summary>One hint, as the node's outputs carry it.</summary>
        public sealed class Hint
        {
            public GameObject task;
            public int hintsUsed;
        }

        private readonly Dictionary<GraphReference, ProcedurePlaceholder> subscribed = new();

        [NullMeansSelf, DoNotSerialize] public ValueInput Procedure { get; private set; }
        [DoNotSerialize] public ValueOutput Task { get; private set; }
        [DoNotSerialize] public ValueOutput HintsUsed { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            Procedure = ValueInput<ProcedurePlaceholder>(nameof(Procedure), null).NullMeansSelf();
            Task = ValueOutput<GameObject>(nameof(Task));
            HintsUsed = ValueOutput<int>(nameof(HintsUsed));
        }

        public override EventHook GetHook(GraphReference reference) => new("Procedure" + nameof(OnProcedureHintUsedEventUnit));

        protected override ProcedureHintHandler GetData(GraphReference instance) =>
            (task, hints) => Trigger(instance, new Hint { task = task, hintsUsed = hints });

        public override void Instantiate(GraphReference instance)
        {
            base.Instantiate(instance);
            ProcedurePlaceholder procedure = Flow.New(instance).GetValue<ProcedurePlaceholder>(Procedure);
            if (procedure == null)
            {
                throw new System.ArgumentNullException(nameof(Procedure), "[OnProcedureHintUsedEventUnit] no Procedure placeholder");
            }
            procedure.HintUsed += instanceData[instance];
            subscribed[instance] = procedure;
        }

        public override void Uninstantiate(GraphReference instance)
        {
            if (subscribed.TryGetValue(instance, out ProcedurePlaceholder procedure) && procedure != null)
            {
                procedure.HintUsed -= instanceData[instance];
            }
            subscribed.Remove(instance);
            base.Uninstantiate(instance);
        }

        protected override void AssignArguments(Flow flow, Hint hint)
        {
            flow.SetValue(Task, hint.task);
            flow.SetValue(HintsUsed, hint.hintsUsed);
        }
    }
}
