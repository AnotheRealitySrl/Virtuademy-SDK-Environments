using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using static Virtuademy.Environments.ScriptingApi.Interaction.IManipulable;



using SPACS.VisualScripting;

using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    public abstract class OnManipulationEventUnit : UnityEventUnit<IManipulable, EManipulableState>
    {
        [DoNotSerialize]
        public ValueOutput Manipulable { get; private set; }
        protected override bool register => true;

        protected override void Definition()
        {
            base.Definition();
            // Setting the value on our port.
            Manipulable = ValueOutput<IManipulable>(nameof(Manipulable));
        }

        protected override void AssignArguments(Flow flow, IManipulable data)
        {
            flow.SetValue(Manipulable, data);
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("Manipulable" + this.ToString().Split("EventUnit")[0]);
        }


        protected override UnityEvent<EManipulableState> GetEvent(GraphReference reference)
        {
            var manipulablePlaceholderReference = reference.gameObject.transform.GetComponentInactive<ManipulablePlaceholder>();
            //If we do not find the placeholder we try to find the manipulable component directly on the gameObject.
            if (manipulablePlaceholderReference == null)
            {
                var imanipulable = reference.gameObject.transform.GetComponentInChildren<IManipulable>(true);
                if (imanipulable is Component manipulable)
                {
                    if (manipulable.gameObject == reference.gameObject)
                    {
                        return imanipulable.OnCurrentStateChange;
                    }
                }
                Debug.LogError($"There is no manipulable placeholder or manipulable attached to the gameObject {reference.gameObject}! " +
                                   $"The node {this.ToString().Split("EventUnit")[0]} will not be called on this gameobject.", reference.gameObject);
                return new UnityEvent<EManipulableState>();

            }

            return manipulablePlaceholderReference.OnCurrentStateChange;
        }

        protected override IManipulable GetArguments(GraphReference reference, EManipulableState state)
        {
            return reference.gameObject.GetComponent<IManipulable>();
        }

        protected override UnityAction<EManipulableState> GetData(GraphReference reference)
        {
            return (x) =>
            {
                if (ShouldTriggerOnChange(x))
                {
                    Trigger(reference, GetArguments(reference, x));
                }
            };
        }

        protected abstract bool ShouldTriggerOnChange(EManipulableState manipulableState);
    }
}
