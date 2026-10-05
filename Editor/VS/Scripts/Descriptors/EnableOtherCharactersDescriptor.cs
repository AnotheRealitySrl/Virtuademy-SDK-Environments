using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(EnableOtherCharactersNode))]
    public class EnableOtherCharactersDescriptor : UnitDescriptor<EnableOtherCharactersNode>
    {
        public EnableOtherCharactersDescriptor(EnableOtherCharactersNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will disable or enable the meshes of avatars not " +
                "controlled by the local player. The avatar meshes will stay disabled " +
                "until the number of times this setting has been enabled will equals " +
                "the number of times this setting has been disabled. " +
                "After using this node to hide avatar meshes, remember to use it " +
                "again to re-enable them if you want to revert the effect.";
        }
    }
}
