using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(OnLanguageChangedEventUnit))]
    public class OnLanguageChangeDescriptor : UnitDescriptor<OnLanguageChangedEventUnit>
    {
        public OnLanguageChangeDescriptor(OnLanguageChangedEventUnit unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This event unit will be triggered when the language is swapped. It returns the new language chosen, as well as its code and the previously selected language and its code";
        }
    }
}
