using Virtuademy.ScriptingApi;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Expose: CMEvent")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("CMEvent")]
    [UnitCategory("Virtuademy\\Expose")]
    public class CollectEventDataNode : Unit
    {
        [DoNotSerialize]
        public ValueInput CMEvent { get; private set; }

        [DoNotSerialize]
        public ValueOutput ID { get; private set; }
        [DoNotSerialize]
        public ValueOutput Title { get; private set; }
        [DoNotSerialize]
        public ValueOutput Description { get; private set; }
        [DoNotSerialize]
        public ValueOutput StartDateTime { get; private set; }
        [DoNotSerialize]
        public ValueOutput EndDateTime { get; private set; }
        [DoNotSerialize]
        public ValueOutput Tags { get; private set; }
        [DoNotSerialize]
        public ValueOutput IsEventPublic { get; private set; }
        [DoNotSerialize]
        public ValueOutput IsEventStatic { get; private set; }

        protected override void Definition()
        {
            CMEvent = ValueInput<SessionView>(nameof(CMEvent), null).NullMeansSelf();

            ID = ValueOutput(nameof(ID), (flow) => flow.GetValue<SessionView>(CMEvent).Id);

            Title = ValueOutput(nameof(Title), (flow) => flow.GetValue<SessionView>(CMEvent).Experience.Title);

            Description = ValueOutput(nameof(Description), (flow) => flow.GetValue<SessionView>(CMEvent).Experience.Description);

            StartDateTime = ValueOutput(nameof(StartDateTime), (flow) => flow.GetValue<SessionView>(CMEvent).StartDateTime);

            EndDateTime = ValueOutput(nameof(EndDateTime), (flow) => flow.GetValue<SessionView>(CMEvent).EndDateTime);

            Tags = ValueOutput(nameof(Tags), (flow) => flow.GetValue<SessionView>(CMEvent).Tags);

            IsEventPublic = ValueOutput(nameof(IsEventPublic), (flow) => flow.GetValue<SessionView>(CMEvent).IsPublic);
        }
    }
}