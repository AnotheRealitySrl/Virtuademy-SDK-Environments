using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using System;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Localization: On Language Changed")]
    [UnitSurtitle("Localization")]
    [UnitShortTitle("On Language Changed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnLanguageChangedEventUnit : ActionEventUnit<string, string>
    {

        public static string eventName = "OnLanguageChanged";
        //public static Dictionary<GraphReference, List<OnLanguageChangedEventUnit>> instances = new Dictionary<GraphReference, List<OnLanguageChangedEventUnit>>();

        protected override bool register => true;

        public ValueOutput CurrentLanguage { get; private set; }
        public ValueOutput CurrentLanguageCode { get; private set; }
        public ValueOutput PreviousLanguage { get; private set; }
        public ValueOutput PreviousLanguageCode { get; private set; }


        protected override void Definition()
        {
            base.Definition();

            CurrentLanguage = ValueOutput<string>(nameof(CurrentLanguage), (flow) => IVirtuademyGameplay.Current.Localization.CurrentLanguage);
            CurrentLanguageCode = ValueOutput<string>(nameof(CurrentLanguageCode), (flow) => IVirtuademyGameplay.Current.Localization.CurrentLanguageCode);
            PreviousLanguage = ValueOutput<string>(nameof(PreviousLanguage), (flow) => IVirtuademyGameplay.Current.Localization.PreviousLanguage);
            PreviousLanguageCode = ValueOutput<string>(nameof(PreviousLanguageCode), (flow) => IVirtuademyGameplay.Current.Localization.PreviousLanguageCode);
        }

        public override EventHook GetHook(GraphReference reference)
        {
            /*if (instances.TryGetValue(reference, out var value))
            {
                if (!value.Contains(this))
                {
                    value.Add(this);
                }
            }
            else
            {
                List<OnLanguageChangedEventUnit> variableList = new List<OnLanguageChangedEventUnit>
                {
                    this
                };

                instances.Add(reference, variableList);
            }*/

            //return new EventHook(eventName);
            return new EventHook("lANGUAGEChange" + this.ToString().Split("EventUnit")[0]);
        }

        protected override void Subscribe(Action<string> handler)
        {
            if (!IVirtuademyGameplay.Current.Localization.IsAvailable)
            {
                return;
            }

            IVirtuademyGameplay.Current.Localization.LanguageChanged += handler;
        }

        protected override void Unsubscribe(Action<string> handler)
        {
            IVirtuademyGameplay.Current.Localization.LanguageChanged -= handler;
        }

        protected override string GetArguments(GraphReference reference, string data)
        {
            return "";
        }

        public override void Uninstantiate(GraphReference instance)
        {
            base.Uninstantiate(instance);
            //instances.Remove(instance);
        }
    }
}
