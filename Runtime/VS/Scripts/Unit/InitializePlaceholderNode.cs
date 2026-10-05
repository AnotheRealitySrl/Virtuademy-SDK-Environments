
using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Placeholder: Initialize Placeholder")]
    [UnitSurtitle("Placeholder")]
    [UnitShortTitle("Initialize Placeholder")]
    [UnitCategory("Virtuademy\\Flow")]
    public class InitializePlaceholderNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [DoNotSerialize]
        [NullMeansSelf]
        [Serialize]
        [AllowsNull]
        [PortLabel("Target")]
        public ValueInput Target { get; private set; }
        [DoNotSerialize]
        [PortLabel("Placeholders in children")]
        public ValueInput PlaceholdersInChildren { get; private set; }

        protected override void Definition()
        {
            Target = ValueInput<GameObject>(nameof(Target), null).NullMeansSelf();
            PlaceholdersInChildren = ValueInput(nameof(PlaceholdersInChildren), false);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Scene.InitializePlaceholders(f.GetValue<GameObject>(Target), f.GetValue<bool>(PlaceholdersInChildren));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
