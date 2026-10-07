using Virtuademy.ScriptingApi;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Expose: CMEnvironment")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("CMEnvironment")]
    [UnitCategory("Virtuademy\\Expose")]
    public class CollectEnvironmentsDataNode : Unit
    {
        [DoNotSerialize]
        public ValueInput CMEnvironment { get; private set; }
        [DoNotSerialize]
        public ValueOutput ID { get; private set; }
        [DoNotSerialize]
        public ValueOutput Name { get; private set; }
        [DoNotSerialize]
        public ValueOutput Description { get; private set; }
        [DoNotSerialize]
        public ValueOutput AddressableKey { get; private set; }

        protected override void Definition()
        {
            CMEnvironment = ValueInput<EnvironmentView>(nameof(CMEnvironment), null).NullMeansSelf();

            ID = ValueOutput(nameof(ID), (flow) => flow.GetValue<EnvironmentView>(CMEnvironment).ID);

            Name = ValueOutput(nameof(Name), (flow) => flow.GetValue<EnvironmentView>(CMEnvironment).Name);

            Description = ValueOutput(nameof(Description), (flow) => flow.GetValue<EnvironmentView>(CMEnvironment).Description);

            AddressableKey = ValueOutput(nameof(AddressableKey), (flow) => flow.GetValue<EnvironmentView>(CMEnvironment).AddressableKey);

        }
    }
}