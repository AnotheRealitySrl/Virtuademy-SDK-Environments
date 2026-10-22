using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>The item name of the tool the local player holds; empty when the hand is empty.</summary>
    [UnitTitle("Virtuademy Tools: Get Held Tool")]
    [UnitSurtitle("Tools")]
    [UnitShortTitle("Get Held Tool")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetHeldToolNode : Unit
    {
        [DoNotSerialize]
        public ValueOutput ToolName { get; private set; }

        protected override void Definition()
        {
            ToolName = ValueOutput<string>(nameof(ToolName), (flow) => IVirtuademyGameplay.Current.Tools.HeldToolName);
        }
    }
}
