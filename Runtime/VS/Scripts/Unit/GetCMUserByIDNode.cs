using Virtuademy.ScriptingApi;

using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.VisualScripting;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get CMUserByID")]
    [UnitSurtitle("CMUserByID")]
    [UnitShortTitle("Get CMUserByID")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCMUserByIDNode : AwaitableUnit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CMUser { get; private set; }

        public ValueInput UserID { get; private set; }

        private List<Flow> runningFlows = new List<Flow>();

        private UserView cmUserData;

        protected override void Definition()
        {
            UserID = ValueInput<int>(nameof(UserID));
            CMUser = ValueOutput<UserView>(nameof(CMUser), f => cmUserData);

            base.Definition();
        }

        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> found = new();

            IVirtuademyFramework.Current.Session.GetUser(flow.GetValue<int>(UserID), user =>
            {
                cmUserData = user;
                found.TrySetResult(true);
            });

            return found.Task;
        }

    }
}
