using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>Closes the open popup: <see cref="IUIApi.ClosePopup"/>.</summary>
    [UnitTitle("Virtuademy UI: Close Popup")]
    [UnitSurtitle("UI")]
    [UnitShortTitle("Close Popup")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ClosePopupNode : Unit
    {
        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            InputTrigger = ControlInput(nameof(InputTrigger), _ =>
            {
                IVirtuademyGameplay.Current.UI.ClosePopup();
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Succession(InputTrigger, OutputTrigger);
        }
    }
}
