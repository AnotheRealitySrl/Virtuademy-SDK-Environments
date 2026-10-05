
using System.Threading.Tasks;

using Unity.VisualScripting;

using Virtuademy.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Platform: Reload Scene")]
    [UnitSurtitle("Platform")]
    [UnitShortTitle("Reload Scene")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ReloadSceneNode : AwaitableUnit
    {
        //[NullMeansSelf]
        //[DoNotSerialize]
        //[PortLabelHidden]
        //public ValueInput IsTenantEnvironment { get; private set; }

        /*[NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Multiplayer { get; private set; }*/

        protected override void Definition()
        {
            //IsTenantEnvironment = ValueInput<bool>(nameof(IsTenantEnvironment), false);

            base.Definition();
        }

        protected override Task AwaitableAction(Flow flow)
        {
            bool multiplayer = IVirtuademyFramework.Current.Session.IsEnvironmentMultiplayer;
            TaskCompletionSource<bool> done = new();

            IVirtuademyFramework.Current.Session.FindExperience(
                IVirtuademyFramework.Current.Session.EnvironmentName, experience =>
            {
                if (experience == null)
                {
                    done.TrySetResult(false);

                    return;
                }

                IVirtuademyFramework.Current.Session.JoinExperience(experience, multiplayer,
                                                                   joined => done.TrySetResult(joined));
            });

            return done.Task;
        }
    }
}
