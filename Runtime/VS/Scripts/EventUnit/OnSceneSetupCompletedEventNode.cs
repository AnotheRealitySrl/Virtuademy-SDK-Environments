using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Scene: On Setup Completed")]
    [UnitSurtitle("Scene")]
    [UnitShortTitle("On Setup Completed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnSceneSetupCompletedEventNode : EventUnit<string>
    {
        public static string EventName => "OnSceneSetupCompletedEvent";

        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(EventName);
        }

    }
}

