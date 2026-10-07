using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ContextualMenu: Hide")]
    [UnitSurtitle("ContextualMenu")]
    [UnitShortTitle("Hide")]
    [UnitCategory("Virtuademy\\Flow")]

    public class HideContextualMenuUnit : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyGameplay.Current.Tools.HideContextualMenu();
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }


    }
}
