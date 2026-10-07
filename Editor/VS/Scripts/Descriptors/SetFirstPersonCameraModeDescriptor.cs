using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(SetFirstPersonCameraModeNode))]
    public class SetFirstPersonCameraModeDescriptor : UnitDescriptor<SetFirstPersonCameraModeNode>
    {
        public SetFirstPersonCameraModeDescriptor(SetFirstPersonCameraModeNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will switch the character view to first person. This is effective only " +
                "on WebGL/Desktop platform.";
        }
    }
}
