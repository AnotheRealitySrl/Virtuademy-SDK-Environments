using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;
using UnityEngine.Events;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Video: On Video Paused")]
    [UnitSurtitle("BigScreen")]
    [UnitShortTitle("On Video Paused")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnVideoPausedEventUnit : UnityEventUnit<BigScreenPlaceholder>
    {
        protected override bool register => true;

        protected override void Definition()
        {
            base.Definition();
            //VideoPlayerReference = ValueInput<BigScreenPlaceholder>(nameof(videoPlayerReference), null).NullMeansSelf(); //this should always be self
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("BigScreen" + this.ToString().Split("EventUnit")[0]);
        }


        protected override UnityEvent GetEvent(GraphReference reference)
        {
            return reference.gameObject.GetComponent<BigScreenPlaceholder>().onVideoPaused;
        }

        protected override BigScreenPlaceholder GetArguments(GraphReference reference)
        {
            return reference.gameObject.GetComponent<BigScreenPlaceholder>();
        }
    }
}
