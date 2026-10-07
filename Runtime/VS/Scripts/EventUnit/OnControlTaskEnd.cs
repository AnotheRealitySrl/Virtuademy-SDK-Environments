using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;
using UnityEngine.Events;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ControlManager: On Informative Item End")]
    [UnitSurtitle("Control Manager")]
    [UnitShortTitle("On Informative Item End")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnControlTaskEnd : UnityEventUnit<InformativeItem_Abstract>
    {
        protected override bool register => true;

        [DoNotSerialize]
        public ValueInput InformativeItemReference { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            InformativeItemReference = ValueInput<InformativeItem_Abstract>(nameof(InformativeItemReference), null).NullMeansSelf();
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("InformativeItem_Abstract" + this.ToString().Split("EventUnit")[0]);
        }

        protected override UnityEvent GetEvent(GraphReference reference)
        {
            var informativeReference = Flow.New(reference).GetValue<InformativeItem_Abstract>(InformativeItemReference);
            if (informativeReference == null)
            {
                return new UnityEvent();
            }
            else
            {
                return informativeReference.onTaskEnd;
            }
        }

        protected override InformativeItem_Abstract GetArguments(GraphReference reference)
        {
            return Flow.New(reference).GetValue<InformativeItem_Abstract>(InformativeItemReference);
        }
    }
}
