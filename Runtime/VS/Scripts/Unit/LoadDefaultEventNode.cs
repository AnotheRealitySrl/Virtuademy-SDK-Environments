
using System.Threading.Tasks;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Platform: Load Lobby")]
    [UnitSurtitle("Platform")]
    [UnitShortTitle("Load Lobby")]
    [UnitCategory("Virtuademy\\Flow")]
    public class LoadDefaultEventNode : AwaitableUnit
    {
        protected override Task AwaitableAction(Flow flow)
        {
            // Nothing to wait for: by the time the lobby is up this world is gone, and so is the
            // graph that would have received the output trigger.
            IVirtuademyGameplay.Current.Scene.ReturnToLobby();

            return Task.CompletedTask;
        }
    }
}
