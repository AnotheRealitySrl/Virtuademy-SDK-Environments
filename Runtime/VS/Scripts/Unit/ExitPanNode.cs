
using System.Threading.Tasks;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Character: Exit Pan")]
    [UnitSurtitle("Character")]
    [UnitShortTitle("Exit Pan")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ExitPanNode : AwaitableUnit
    {
        protected override Task AwaitableAction(Flow flow)
        {
            TaskCompletionSource<bool> left = new();
            IVirtuademyGameplay.Current.Player.ExitCameraPan(() => left.TrySetResult(true));

            return left.Task;
        }
    }
}
