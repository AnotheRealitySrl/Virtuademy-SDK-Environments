using Virtuademy.Environments.ScriptingApi.Interaction;

using Unity.VisualScripting;



using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Visual Scripting Interactable: Unselect OnDestroy")]
    [UnitSurtitle("Visual Scripting Interactable")]
    [UnitShortTitle("Unselect OnDestroy")]
    [UnitCategory("Events\\Virtuademy")]
    public class UnselectOnDestroyUnit : AwaitableEventUnit<IVisualScriptingInteractable>
    {
        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("VisualScriptingInteractable" + this.ToString().Split("Unit")[0]);
        }
    }
}
