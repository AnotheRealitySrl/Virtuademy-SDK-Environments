using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// The translation of a key, or the fallback text when the environment's Localization File has
    /// none: <see cref="ILocalizationApi.TranslateOr"/>. Outside the application, the fallback.
    /// </summary>
    [UnitTitle("Virtuademy Localization: Translate Or")]
    [UnitSurtitle("Localization")]
    [UnitShortTitle("Translate Or")]
    [UnitCategory("Virtuademy\\Get")]
    public class TranslateOrNode : Unit
    {
        [DoNotSerialize] public ValueInput Key { get; private set; }
        [DoNotSerialize] public ValueInput Fallback { get; private set; }
        [DoNotSerialize] public ValueOutput Translation { get; private set; }

        protected override void Definition()
        {
            Key = ValueInput<string>(nameof(Key), string.Empty);
            Fallback = ValueInput<string>(nameof(Fallback), string.Empty);
            Translation = ValueOutput<string>(nameof(Translation),
                f => PanelEnvironment.Text(f.GetValue<string>(Key), f.GetValue<string>(Fallback)));
            Requirement(Key, Translation);
            Requirement(Fallback, Translation);
        }
    }
}
