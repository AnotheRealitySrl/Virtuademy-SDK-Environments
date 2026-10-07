using Virtuademy.ScriptingApi;

using System.Collections.Generic;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Expose: CMUser")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("CMUser")]
    [UnitCategory("Virtuademy\\Expose")]
    public class CollectPlayerDataNode : Unit
    {
        [DoNotSerialize]
        public ValueInput CMUser { get; private set; }
        [DoNotSerialize]
        public ValueOutput ID { get; private set; }
        [DoNotSerialize]
        public ValueOutput Name { get; private set; }
        [DoNotSerialize]
        public ValueOutput EMail { get; private set; }
        [DoNotSerialize]
        public ValueOutput Roles { get; private set; }

        [DoNotSerialize]
        public ValueOutput ProfileImageURL { get; private set; }

        protected override void Definition()
        {
            CMUser = ValueInput<UserView>(nameof(CMUser), null).NullMeansSelf();

            ID = ValueOutput(nameof(ID), (flow) => flow.GetValue<UserView>(CMUser).Id);

            Name = ValueOutput(nameof(Name), (flow) => flow.GetValue<UserView>(CMUser).DisplayName);

            EMail = ValueOutput(nameof(EMail), (flow) => flow.GetValue<UserView>(CMUser).Email);

            ProfileImageURL = ValueOutput(nameof(ProfileImageURL), (flow) => flow.GetValue<UserView>(CMUser).ProfileImageUrl);

            Roles = ValueOutput(nameof(Roles), (flow) =>
            {
                List<string> roles = new List<string>();
                foreach (var role in flow.GetValue<UserView>(CMUser).Tags)
                    roles.Add(role.Label);

                return roles;
            });
        }
    }
}
