using System.Collections;

using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Scene: Fade To Black")]
    [UnitSurtitle("Scene")]
    [UnitShortTitle("Fade To Black")]
    [UnitCategory("Virtuademy\\Flow")]
    public class FadeToBlackNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInputCoroutine(nameof(InputTrigger), FadeToBlackCoroutine);

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

        private IEnumerator FadeToBlackCoroutine(Flow flow)
        {
            bool fadeDone = false;

            IVirtuademyGameplay.Current.Screen.FadeToBlack(() => fadeDone = true);

            yield return new WaitUntil(() => fadeDone == true);

            yield return OutputTrigger;
        }
    }
}
