
using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Transition Provider: Do Transition")]
    [UnitSurtitle("Transition Provider")]
    [UnitShortTitle("Do Transition")]
    [UnitCategory("Virtuademy\\Flow")]
    public class TransitionProviderActivatorNode : Unit
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
        public ValueInput Enter { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput GameobjectVal { get; private set; }

        protected override void Definition()
        {
            Enter = ValueInput<bool>(nameof(Enter));

            GameobjectVal = ValueInput<GameObject>(nameof(GameobjectVal), null).NullMeansSelf();

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Scene.RunTransition(f.GetValue<GameObject>(GameobjectVal), f.GetValue<bool>(Enter));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
