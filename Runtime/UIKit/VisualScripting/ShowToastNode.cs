using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>Shows a toast: <see cref="IUIApi.ShowToast"/>.</summary>
    [UnitTitle("Virtuademy UI: Show Toast")]
    [UnitSurtitle("UI")]
    [UnitShortTitle("Show Toast")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ShowToastNode : Unit
    {
        [DoNotSerialize] public ValueInput Text { get; private set; }
        [DoNotSerialize] public ValueInput Severity { get; private set; }

        /// <summary>Seconds on screen; 0 uses the host's default.</summary>
        [DoNotSerialize] public ValueInput Duration { get; private set; }

        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize, PortLabelHidden] public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            Text = ValueInput<string>(nameof(Text), string.Empty);
            Severity = ValueInput(nameof(Severity), EToastSeverity.Info);
            Duration = ValueInput(nameof(Duration), 0f);

            InputTrigger = ControlInput(nameof(InputTrigger), f =>
            {
                IVirtuademyGameplay.Current.UI.ShowToast(f.GetValue<string>(Text),
                    f.GetValue<EToastSeverity>(Severity), f.GetValue<float>(Duration));
                return OutputTrigger;
            });
            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Requirement(Text, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
        }
    }
}
