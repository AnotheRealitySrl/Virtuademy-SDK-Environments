using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Synced Variables: On Synced Variable Init")]
    [UnitSurtitle("Synced Variables Init")]
    [UnitShortTitle("On Synced Variable Init")]
    [UnitCategory("Events\\Virtuademy")]
    ///this unit is always called during the first deserialization, it will start a flow and will comunicate if the value of
    ///the variable was changed or not
    ///it differs from OnSyncedVariableInitEventUnit
    public class OnSyncedVariableInit : SyncedVariableBaseEventUnit<(string, object, bool)>
    {
        public static string eventName = "SyncedVariablesOnVariableInit";


        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        [DoNotSerialize]
        [Tooltip("This variable returns true if the variable in the variable name field has been changed at least once from its default state. Otherwise it returns false")]
        public ValueOutput IsChanged { get; private set; }


        protected override bool register => true;
        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(eventName);
        }

        protected override void Definition()
        {
            base.Definition();
            Value = ValueOutput<object>(nameof(Value));
            IsChanged = ValueOutput<bool>(nameof(IsChanged));
        }

        protected override bool ShouldTrigger(Flow flow, (string, object, bool) args)
        {
            return flow.GetValue<string>(VariableName) == args.Item1;
        }

        protected override void AssignArguments(Flow flow, (string, object, bool) args)
        {
            flow.SetValue(Value, args.Item2);
            flow.SetValue(IsChanged, args.Item3);
        }
    }
}
