using System;

using SPACS.VisualScripting;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>The help panel has finished opening: <see cref="IHelpApi.Opened"/>.</summary>
    [UnitTitle("Virtuademy Tutorial: On Tutorial Opened")]
    [UnitSurtitle("Tutorial")]
    [UnitShortTitle("On Tutorial Opened")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTutorialOpenedEventUnit : ActionEventUnit<Null>
    {
        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
            => new EventHook("tutorial opened" + ToString().Split("EventUnit")[0]);

        protected override void Subscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Help.Opened += handler;
            }
        }

        protected override void Unsubscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Help.Opened -= handler;
            }
        }

        protected override Null GetArguments(GraphReference reference) => null;
    }
}
