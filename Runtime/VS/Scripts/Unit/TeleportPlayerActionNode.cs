using System.Collections;
using System.Collections.Generic;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Character: Teleport")]
    [UnitSurtitle("Character")]
    [UnitShortTitle("Teleport")]
    [UnitCategory("Virtuademy\\Flow")]
    public class TeleportPlayerActionNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput TransformVal { get; private set; }

        private List<Flow> runningFlows = new List<Flow>();

        protected override void Definition()
        {
            TransformVal = ValueInput<Transform>(nameof(TransformVal));

            InputTrigger = ControlInputCoroutine(nameof(InputTrigger), TeleportPlayerCoroutine);

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

        private IEnumerator TeleportPlayerCoroutine(Flow flow)
        {
            runningFlows.Add(flow);

            // The fade sandwich moved behind Teleport: fade out, move while the screen is black,
            // fade back in, then report. It was here and nowhere else, which is why the grouped
            // surface has one member where the flat one had three calls.
            IVirtuademyGameplay.Current.Player.Teleport(flow.GetValue<Transform>(TransformVal),
                                                        () => runningFlows.Remove(flow));

            yield return new WaitUntil(() => !runningFlows.Contains(flow));

            yield return OutputTrigger;
        }
    }
}
