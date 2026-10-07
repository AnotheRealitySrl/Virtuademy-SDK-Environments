using Unity.VisualScripting;


namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(CheckSceneAvailabilityNode))]
    public class CheckSceneAvailabilityDescriptor : UnitDescriptor<CheckSceneAvailabilityNode>
    {
        public CheckSceneAvailabilityDescriptor(CheckSceneAvailabilityNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit requires a coroutine flow to run properly. This unit will " +
                "check if an environment scene is available.";
        }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            switch (port.key)
            {
                case "SceneAddressableName":
                    description.summary = "The name of the environment scene used by the target static event.";
                    break;
            }
        }
    }
}
