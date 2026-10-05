using Virtuademy.Environments.ScriptingApi.Interaction;
using Unity.VisualScripting;

using System;

using Virtuademy.Environments.ScriptingApi;



using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Visual Scripting Interactable: On Selected Change")]
    [UnitSurtitle("VisualScriptingInteractable")]
    [UnitShortTitle("On Selected Change")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnSelectedVisualScriptingInteractableChange : ActionEventUnit<IVisualScriptingInteractable, IVisualScriptingInteractable>
    {
        [DoNotSerialize]
        public ValueOutput VisualScriptingInteractable { get; private set; }
        protected override bool register => true;

        protected override void Definition()
        {
            base.Definition();
            // Setting the value on our port.
            VisualScriptingInteractable = ValueOutput<IVisualScriptingInteractable>(nameof(VisualScriptingInteractable));
        }

        protected override void AssignArguments(Flow flow, IVisualScriptingInteractable data)
        {
            flow.SetValue(VisualScriptingInteractable, data);
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("VisualScriptingInteractable" + this.ToString().Split("EventUnit")[0]);
        }

        protected override void Subscribe(Action<IVisualScriptingInteractable> handler)
            => IVirtuademyGameplay.Current.Interaction.SelectedChanged += handler;

        protected override void Unsubscribe(Action<IVisualScriptingInteractable> handler)
            => IVirtuademyGameplay.Current.Interaction.SelectedChanged -= handler;

        protected override IVisualScriptingInteractable GetArguments(GraphReference reference, IVisualScriptingInteractable eventData)
        {
            return eventData;
        }
    }
}
