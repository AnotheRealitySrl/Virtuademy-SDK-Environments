using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Synced Variables: On Synced Variable Changed Init")]
    [UnitSurtitle("Synced Variables Init")]
    [UnitShortTitle("On Synced Variable Changed Init")]
    [UnitCategory("Events\\Virtuademy")]
    ///this unit is called once at the first deserialization if the variable was changed at least 1 time
    ///it is different from OnSyncedVariableInit
    public class OnSyncedVariableInitEventUnit : SyncedVariableBaseEventUnit<(string, object)>
    {
        public static string eventName = "SyncedVariablesOnVariableChangedInit";

        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput Value { get; private set; }

        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(eventName);
        }

        protected override void Definition()
        {
            base.Definition();
            Value = ValueOutput<object>(nameof(Value));
        }

        protected override bool ShouldTrigger(Flow flow, (string, object) args)
        {
            return flow.GetValue<string>(VariableName) == args.Item1;
        }


        protected override void AssignArguments(Flow flow, (string, object) args)
        {
            flow.SetValue(Value, args.Item2);
        }
    }
}
