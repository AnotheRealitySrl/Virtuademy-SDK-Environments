using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Synced Object: On Owner Changed")]
    [UnitSurtitle("Synced Object")]
    [UnitShortTitle("On Owner Changed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnOwnerChangedEventUnit : EventUnit<SyncedObject>
    {
        public static string eventName = "SyncedObjectOnOwnerChanged";

        public static Dictionary<GraphReference, List<OnOwnerChangedEventUnit>> instances = new Dictionary<GraphReference, List<OnOwnerChangedEventUnit>>();

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
                List<OnOwnerChangedEventUnit> variableList = new List<OnOwnerChangedEventUnit>
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
