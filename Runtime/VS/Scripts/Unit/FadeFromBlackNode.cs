using System.Collections;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Scene: Fade From Black")]
    [UnitSurtitle("Scene")]
    [UnitShortTitle("Fade From Black")]
    [UnitCategory("Virtuademy\\Flow")]
    public class FadeFromBlackNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInputCoroutine(nameof(InputTrigger), FadeFromBlackCoroutine);

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

        private IEnumerator FadeFromBlackCoroutine(Flow flow)
        {
            bool fadeDone = false;

            IVirtuademyGameplay.Current.Screen.FadeFromBlack(() => fadeDone = true);

            yield return new WaitUntil(() => fadeDone == true);

            yield return OutputTrigger;
        }
    }
}
