
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

        protected override void Definition()
        {
            //IsTenantEnvironment = ValueInput<bool>(nameof(IsTenantEnvironment), false);

            base.Definition();
        }

        /// <remarks>
        /// The current experience, in the session's mode. Until the reload went through
        /// <see cref="ISessionApi.ReloadSession"/>, the node looked an experience up by the
        /// environment's name, which could find another experience of the same environment, and
        /// joined it single-player whatever the session was.
        /// </remarks>
        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> done = new();

            IVirtuademyFramework.Current.Session.ReloadSession(reloaded => done.TrySetResult(reloaded));

            return done.Task;
        }
    }
}
