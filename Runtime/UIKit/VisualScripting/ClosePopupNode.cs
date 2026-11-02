using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Closes a popup: the one whose id is given (Show Popup's Popup Id), or with 0 whichever is
    /// open. A popup that is already closed or replaced is left alone.
    /// </summary>
    [UnitTitle("Virtuademy UI: Close Popup")]
    [UnitSurtitle("UI")]
    [UnitShortTitle("Close Popup")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ClosePopupNode : Unit
    {
        /// <summary>Show Popup's Popup Id; 0 closes whichever popup is open.</summary>
        [DoNotSerialize] public ValueInput PopupId { get; private set; }

        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            PopupId = ValueInput(nameof(PopupId), 0);
            InputTrigger = ControlInput(nameof(InputTrigger), f =>
            {
                int id = f.GetValue<int>(PopupId);
                if (id > 0)
                {
                    IVirtuademyGameplay.Current.UI.ClosePopup(id);
                }
                else
                {
                    IVirtuademyGameplay.Current.UI.ClosePopup();
                }
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Succession(InputTrigger, OutputTrigger);
        }
    }
}
