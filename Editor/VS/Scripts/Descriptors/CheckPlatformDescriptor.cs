using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(CheckPlatformUnit))]
    public class CheckPlatformDescriptor : UnitDescriptor<CheckPlatformUnit>
    {
        public CheckPlatformDescriptor(CheckPlatformUnit unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit allows to define a flow based on which platform the experience is running.";
        }
    }
}
