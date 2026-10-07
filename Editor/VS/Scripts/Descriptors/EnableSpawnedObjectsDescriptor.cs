using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(EnableSpawnedObjectsNode))]
    public class EnableSpawnedObjectsDescriptor : UnitDescriptor<EnableSpawnedObjectsNode>
    {
        public EnableSpawnedObjectsDescriptor(EnableSpawnedObjectsNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "NOT IMPLEMENTED YET!";
        }
    }
}
