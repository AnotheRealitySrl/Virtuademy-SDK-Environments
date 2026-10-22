using System;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>The local player put a tool away, or switched from it. Outputs the tool's item name.</summary>
    [UnitTitle("Virtuademy Tools: On Tool Unequipped")]
    [UnitSurtitle("Tools")]
    [UnitShortTitle("On Tool Unequipped")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnToolUnequippedEventUnit : ActionEventUnit<string, string>
    {
        public static string eventName = "ToolsOnToolUnequipped";

        [DoNotSerialize]
        public ValueOutput ToolName { get; private set; }

        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(eventName);
        }

        protected override void Definition()
        {
            base.Definition();
            ToolName = ValueOutput<string>(nameof(ToolName));
        }

        protected override void AssignArguments(Flow flow, string toolName)
        {
            flow.SetValue(ToolName, toolName);
        }

        protected override void Subscribe(Action<string> handler)
            => IVirtuademyGameplay.Current.Tools.ToolUnequipped += handler;

        protected override void Unsubscribe(Action<string> handler)
            => IVirtuademyGameplay.Current.Tools.ToolUnequipped -= handler;

        protected override string GetArguments(GraphReference reference, string toolName)
        {
            return toolName;
        }
    }
}
