
using System.Collections;

using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Tutorial: Enable")]
    [UnitSurtitle("Tutorial")]
    [UnitShortTitle("Enable")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ManageTutorialActionNode : Unit
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
        public ValueInput Enable { get; private set; }

        bool awaitableMethodRuning = false;

        protected override void Definition()
        {
            Enable = ValueInput<bool>(nameof(Enable));

            InputTrigger = ControlInputCoroutine(nameof(InputTrigger), ManageTutorialCoroutine);

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

        private IEnumerator ManageTutorialCoroutine(Flow flow)
        {
            awaitableMethodRuning = false;

            CallAwaitableMethod(flow);

            while (!awaitableMethodRuning)
            {
                yield return null;
            }

            yield return OutputTrigger;
        }

        private void CallAwaitableMethod(Flow flow)
        {
            if (flow.GetValue<bool>(Enable))
            {
                IVirtuademyGameplay.Current.Help.Open();
            }
            else
            {
                IVirtuademyGameplay.Current.Help.Close();
            }

            awaitableMethodRuning = true;
        }
    }
}
