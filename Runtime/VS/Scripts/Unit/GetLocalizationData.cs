using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Localization: Get Localization Data")]
    [UnitSurtitle("LocalizationData")]
    [UnitShortTitle("Get LocalizationData")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetLocalizationData : Unit
    {
        public ValueOutput CurrentLanguage { get; private set; }
        public ValueOutput CurrentLanguageCode { get; private set; }
        public ValueOutput LanguageList { get; private set; }

        protected override void Definition()
        {
            CurrentLanguage = ValueOutput<string>(nameof(CurrentLanguage), (flow) => IVirtuademyGameplay.Current.Localization.CurrentLanguage);
            CurrentLanguageCode = ValueOutput<string>(nameof(CurrentLanguageCode), (flow) => IVirtuademyGameplay.Current.Localization.CurrentLanguageCode);
            LanguageList = ValueOutput<List<string>>(nameof(LanguageList), f => IVirtuademyGameplay.Current.Localization.AvailableLanguages);
        }

    }
}
