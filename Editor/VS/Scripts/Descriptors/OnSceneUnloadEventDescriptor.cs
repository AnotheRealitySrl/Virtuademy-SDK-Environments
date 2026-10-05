using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(OnSceneUnloadEventUnit))]
    public class OnSceneUnloadEventDescriptor : UnitDescriptor<OnSceneUnloadEventUnit>
    {
        public OnSceneUnloadEventDescriptor(OnSceneUnloadEventUnit unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This event unit will be triggered when the scene is unloaded, hence when we are exiting " +
                "the current room to move to another room. The execution of all the flows started " +
                "from this node trigger will be awaited before proceeding with the next scene load. " +
                "This unit will NOT be called if the user exits the game or closes the browser's tab.";
        }

    }
}
