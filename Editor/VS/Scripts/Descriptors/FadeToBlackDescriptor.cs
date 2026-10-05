using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    public class FadeToBlackDescriptor : UnitDescriptor<FadeToBlackNode>
    {
        public FadeToBlackDescriptor(FadeToBlackNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit will smoothly fade the screen to black.";
        }
    }
}
