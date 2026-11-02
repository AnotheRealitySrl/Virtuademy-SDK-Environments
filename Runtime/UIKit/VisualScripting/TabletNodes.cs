using System;

using SPACS.VisualScripting;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>Whether the application's tablet is open (<see cref="ITabletApi.IsOpen"/>). False outside the application.</summary>
    [UnitTitle("Virtuademy Tablet: Is Tablet Open")]
    [UnitSurtitle("Tablet")]
    [UnitShortTitle("Is Tablet Open")]
    [UnitCategory("Virtuademy\\Get")]
    public class IsTabletOpenNode : Unit
    {
        [DoNotSerialize] public ValueOutput IsOpen { get; private set; }

        protected override void Definition()
        {
            IsOpen = ValueOutput<bool>(nameof(IsOpen),
                _ => IVirtuademyGameplay.IsAvailable && IVirtuademyGameplay.Current.Tablet.IsOpen);
        }
    }

    /// <summary>The tablet opened: <see cref="ITabletApi.Opened"/>.</summary>
    [UnitTitle("Virtuademy Tablet: On Tablet Opened")]
    [UnitSurtitle("Tablet")]
    [UnitShortTitle("On Tablet Opened")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTabletOpenedEventUnit : ActionEventUnit<Null>
    {
        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
            => new EventHook("tablet opened" + ToString().Split("EventUnit")[0]);

        protected override void Subscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Tablet.Opened += handler;
            }
        }

        protected override void Unsubscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Tablet.Opened -= handler;
            }
        }

        protected override Null GetArguments(GraphReference reference) => null;
    }

    /// <summary>The tablet closed: <see cref="ITabletApi.Closed"/>.</summary>
    [UnitTitle("Virtuademy Tablet: On Tablet Closed")]
    [UnitSurtitle("Tablet")]
    [UnitShortTitle("On Tablet Closed")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnTabletClosedEventUnit : ActionEventUnit<Null>
    {
        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
            => new EventHook("tablet closed" + ToString().Split("EventUnit")[0]);

        protected override void Subscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Tablet.Closed += handler;
            }
        }

        protected override void Unsubscribe(Action handler)
        {
            if (IVirtuademyGameplay.IsAvailable)
            {
                IVirtuademyGameplay.Current.Tablet.Closed -= handler;
            }
        }

        protected override Null GetArguments(GraphReference reference) => null;
    }
}
