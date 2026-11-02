using Unity.VisualScripting;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.UI;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// The colour of a theme role now (<see cref="IUIApi.GetThemeColor"/>): the tenant's when the
    /// application published one, the SDK default otherwise, also outside the application.
    /// </summary>
    [UnitTitle("Virtuademy UI: Get Theme Color")]
    [UnitSurtitle("UI")]
    [UnitShortTitle("Get Theme Color")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetThemeColorNode : Unit
    {
        [DoNotSerialize] public ValueInput Role { get; private set; }
        [DoNotSerialize] public ValueOutput Color { get; private set; }

        protected override void Definition()
        {
            Role = ValueInput(nameof(Role), EThemeRole.Action);
            Color = ValueOutput<Color>(nameof(Color), f =>
            {
                EThemeRole role = f.GetValue<EThemeRole>(Role);
                return IVirtuademyGameplay.IsAvailable
                    ? IVirtuademyGameplay.Current.UI.GetThemeColor(role)
                    : VirtuademyThemeRuntime.Resolve(role);
            });
            Requirement(Role, Color);
        }
    }
}
