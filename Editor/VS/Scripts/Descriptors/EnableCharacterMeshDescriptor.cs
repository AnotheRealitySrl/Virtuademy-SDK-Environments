using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(EnableCharacterMeshNode))]
    public class EnableCharacterMeshDescriptor : UnitDescriptor<EnableCharacterMeshNode>
    {
        public EnableCharacterMeshDescriptor(EnableCharacterMeshNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will disable or enable the character mesh. The player mesh will stay disable until the number of time " +
                "the character has been enabled corrisponds to the number of time character has been disabled. Remember to reenable the character" +
                " after the interaction is completed.";
        }
    }
}
