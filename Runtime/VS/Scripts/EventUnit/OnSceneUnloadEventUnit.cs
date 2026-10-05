
using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.VisualScripting;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Scene: On Unload")]
    [UnitSurtitle("Scene")]
    [UnitShortTitle("On Unload")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnSceneUnloadEventUnit : AwaitableEventUnit<string>
    {
        // "OnSceneLoad" until 2026-09-14, the same string the load node uses, so both node
        // types registered the same EventHook. Nothing fired through it — the application calls
        // TriggerAllNodes, which is scoped to one node type and bypasses the bus — so the
        // collision never showed. It would have, the first time someone raised this event the
        // way Visual Scripting raises events: On Unload running when the scene loads.
        public static string eventName = "OnSceneUnload";

        public static Dictionary<GraphReference, List<OnSceneUnloadEventUnit>> instances = new Dictionary<GraphReference, List<OnSceneUnloadEventUnit>>();

        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
        {
            if (instances.TryGetValue(reference, out var value))
            {
                if (!value.Contains(this))
                {
                    value.Add(this);
                }
            }
            else
            {
                List<OnSceneUnloadEventUnit> variableList = new List<OnSceneUnloadEventUnit>
                {
                    this
                };

                instances.Add(reference, variableList);
            }

            return new EventHook(eventName);
        }

        public override void Uninstantiate(GraphReference instance)
        {
            base.Uninstantiate(instance);
            instances.Remove(instance);
        }

        public static async Task TriggerAllNodes()
        {
            List<Task> providerEnterTask = new List<Task>();

            foreach (GraphReference reference in instances.Keys)
            {
                foreach (var node in instances[reference])
                {
                    providerEnterTask.Add(node.AwaitableTrigger(reference, ""));
                }
            }

            await Task.WhenAll(providerEnterTask);

        }
    }
}

