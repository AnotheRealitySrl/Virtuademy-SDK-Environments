using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Whether the help panel is open, opening or closing (<see cref="IHelpApi.IsOpen"/>). False
    /// outside the application.
    /// </summary>
    [UnitTitle("Virtuademy Tutorial: Is Tutorial Open")]
    [UnitSurtitle("Tutorial")]
    [UnitShortTitle("Is Tutorial Open")]
    [UnitCategory("Virtuademy\\Get")]
    public class IsTutorialOpenNode : Unit
    {
        [DoNotSerialize] public ValueOutput IsOpen { get; private set; }

        protected override void Definition()
        {
            IsOpen = ValueOutput<bool>(nameof(IsOpen), _ => Virtuademy.SDK.Environments.UIKit.PanelEnvironment.IsHelpOpen);
        }
    }
}
