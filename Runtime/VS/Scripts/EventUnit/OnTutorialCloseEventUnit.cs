using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using System;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Tutorial: On Tutorial Closed")]
    [UnitSurtitle("Tutorial")]
    [UnitShortTitle("On Tutorial Closed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTutorialCloseEventUnit : ActionEventUnit<Null>
    {

        public static string eventName = "OnTutorialClosed";

        protected override bool register => true;

        protected override void Definition()
        {
            base.Definition();
        }

        public override EventHook GetHook(GraphReference reference)
        {

            return new EventHook("tutorial closed" + this.ToString().Split("EventUnit")[0]);
        }

        protected override void Subscribe(Action handler)
        {
            if (!IVirtuademyGameplay.Current.Help.IsAvailable)
            {
                return;
            }

            IVirtuademyGameplay.Current.Help.Closed += handler;
        }

        protected override void Unsubscribe(Action handler)
        {
            // Unconditional, unlike the subscribe: whether the host has a help panel can change
            // between the two, and removing a handler that was never added does nothing.
            IVirtuademyGameplay.Current.Help.Closed -= handler;
        }

        public override void Uninstantiate(GraphReference instance)
        {
            base.Uninstantiate(instance);
        }

        protected override Null GetArguments(GraphReference reference)
        {
            return null;
        }
    }
}
