using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Localization: Get translation")]
    [UnitSurtitle("Localization")]
    [UnitShortTitle("Get Translation")]
    [UnitCategory("Virtuademy\\Flow")]
    public class LocalizationGetStringFromKey : Unit
    {
        [NullMeansSelf]

        [DoNotSerialize]
        public ValueInput Key { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueOutput Translation { get; private set; }


        protected override void Definition()
        {
            Key = ValueInput<string>(nameof(Key), string.Empty);

            Translation = ValueOutput<string>(nameof(Translation), (f) =>
                {
                    return IVirtuademyGameplay.Current.Localization.Translate(f.GetValue<string>(Key));
                });
        }
    }
}
