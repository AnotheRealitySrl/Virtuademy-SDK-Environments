using Unity.VisualScripting;

using static Virtuademy.Environments.ScriptingApi.Interaction.IManipulable;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Manipulable: On Manipulation End")]
    [UnitSurtitle("Manipulable")]
    [UnitShortTitle("On Manipulation End")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnManipulationEndEventUnit : OnManipulationEventUnit
    {
        protected override bool ShouldTriggerOnChange(EManipulableState manipulableState)
        {
            return manipulableState == EManipulableState.Idle;
        }

    }
}
