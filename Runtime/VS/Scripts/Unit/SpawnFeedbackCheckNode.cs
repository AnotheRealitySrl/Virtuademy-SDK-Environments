using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;
using UnityEngine;



using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy general: Spawn Feedback (WebGL only)")]
    [UnitSurtitle("General")]
    [UnitShortTitle("SpawnFeedback (WebGL only)")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SpawnFeedbackCheckNode : Unit
    {
        [NullMeansSelf]
        public ValueInput Correctness { get; private set; }
        [NullMeansSelf]
        public ValueInput SpawnTransform { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput inputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput outputTrigger { get; private set; }

        protected override void Definition()
        {
            inputTrigger = ControlInput(nameof(inputTrigger), Output);
            outputTrigger = ControlOutput("outputTrigger");

            Correctness = ValueInput<bool>(nameof(Correctness), true);
            SpawnTransform = ValueInput<Transform>(nameof(SpawnTransform), null);

            Succession(inputTrigger, outputTrigger);
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            IVirtuademyGameplay.Current.Tools.ShowAnswerFeedback(flow.GetValue<Transform>(SpawnTransform), flow.GetValue<bool>(Correctness));
            return outputTrigger;
        }
    }
}