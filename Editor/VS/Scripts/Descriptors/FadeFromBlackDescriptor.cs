using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    public class FadeFromBlackDescriptor : UnitDescriptor<FadeFromBlackNode>
    {
        public FadeFromBlackDescriptor(FadeFromBlackNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will smoothly fade the screen from black to the current scene.";
        }
    }
}
