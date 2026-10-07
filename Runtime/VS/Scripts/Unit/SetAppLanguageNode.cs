using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Localization: Set Language")]
    [UnitSurtitle("SetLanguage")]
    [UnitShortTitle("Set Language")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetAppLanguageNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput LanguageChoice { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            LanguageChoice = ValueInput<string>(nameof(LanguageChoice));

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Localization.SetLanguage(f.GetValue<string>(LanguageChoice));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

    }
}
