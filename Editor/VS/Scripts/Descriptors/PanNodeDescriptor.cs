using Virtuademy.SDK.Environments.VisualScripting;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(PanNode))]
    public class PanNodeDescriptor : UnitDescriptor<PanNode>
    {
        public PanNodeDescriptor(PanNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit requires a coroutine flow to run properly. This unit will move the camera lerping its position to a specific transform. " +
                "To exit the pan use the " +
                "pan exit unit.";
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            switch (port.key)
            {
                case "TargetTransform":
                    description.summary = "The transform the camera will end at.";
                    break;
            }
        }
    }
}
