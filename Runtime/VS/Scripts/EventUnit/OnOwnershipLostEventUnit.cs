using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Synced Object: On Owner Lost")]
    [UnitSurtitle("Synced Object")]
    [UnitShortTitle("On Owner Lost")]
    [UnitCategory("Events\\Virtuademy")]

    //This node is called only by the user that lost the ownership, not by everyone
    public class OnOwnershipLostEventUnit : EventUnit<SyncedObject>
    {
        public static string eventName = "SyncedObjectOnOwnerLost";

        public static Dictionary<GraphReference, List<OnOwnershipLostEventUnit>> instances = new Dictionary<GraphReference, List<OnOwnershipLostEventUnit>>();

        [NullMeansSelf]
        [PortLabelHidden]
        [DoNotSerialize]
        public ValueInput SyncedObjectRef { get; private set; }
        protected override bool register => true;

        public static Dictionary<GameObject, List<GraphReference>> graphReferences = new Dictionary<GameObject, List<GraphReference>>();

        public override EventHook GetHook(GraphReference reference)
        {
            if (graphReferences.TryGetValue(reference.gameObject, out List<GraphReference> graphRef))
            {
                if (!graphRef.Contains(reference))
                {
                    graphRef.Add(reference);
                }
            }
            else
            {
                List<GraphReference> graphReferencesList = new List<GraphReference>
                {
                    reference
                };

                graphReferences.Add(reference.gameObject, graphReferencesList);
            }

            if (instances.TryGetValue(reference, out var value))
            {
                if (!value.Contains(this))
                {
                    value.Add(this);
                }
            }
            else
            {
                List<OnOwnershipLostEventUnit> variableList = new List<OnOwnershipLostEventUnit>
                {
                    this
                };

                instances.Add(reference, variableList);
            }

            return new EventHook(eventName);
        }

        protected override void Definition()
        {
            base.Definition();
            SyncedObjectRef = ValueInput<SyncedObject>(nameof(SyncedObjectRef), null).NullMeansSelf();
        }

        protected override bool ShouldTrigger(Flow flow, SyncedObject args)
        {
            if (args == null)
            {
                return false;
            }
            if (flow.GetValue<SyncedObject>(SyncedObjectRef) == args)
            {
                return true;
            }
            return false;
        }
    }
}
