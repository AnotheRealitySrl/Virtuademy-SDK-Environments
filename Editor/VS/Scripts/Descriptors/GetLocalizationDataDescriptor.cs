using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(GetLocalizationData))]
    public class GetLocalizationDataDescriptor : UnitDescriptor<GetLocalizationData>
    {
        public GetLocalizationDataDescriptor(GetLocalizationData unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "Node used to retrieve informations about the language localization";
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            switch (port.key)
            {
                case "CurrentLanguage":
                    description.summary = "The language that is currently set";
                    break;
                case "LanguageList":
                    description.summary = "A list of all the languages currently available";
                    break;
                case "CurrentLanguageCode":
                    description.summary = "The language code of the currently selected language";
                    break;
            }
        }
    }
}
