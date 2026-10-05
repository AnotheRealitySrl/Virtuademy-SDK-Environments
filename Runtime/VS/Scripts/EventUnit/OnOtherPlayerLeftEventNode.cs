using Unity.VisualScripting;

using System;

using Virtuademy.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Networking: On Other Player Left")]
    [UnitSurtitle("Networking")]
    [UnitShortTitle("On Other Player Left")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnOtherPlayerLeftEventNode : ActionEventUnit<(int, string), int, string>
    {
        public static string eventName = "NetworkingOnOtherPlayerLeft";

        [DoNotSerialize]
        public ValueOutput UserId { get; private set; }
        [DoNotSerialize]
        public ValueOutput SessionId { get; private set; }
        protected override bool register => true;

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(eventName);
        }

        protected override void Definition()
        {
            base.Definition();
            UserId = ValueOutput<int>(nameof(UserId));
            SessionId = ValueOutput<string>(nameof(SessionId));
        }

        protected override void AssignArguments(Flow flow, (int, string) args)
        {
            flow.SetValue(UserId, args.Item1);
            flow.SetValue(SessionId, args.Item2);
        }

        protected override void Subscribe(Action<int, string> handler)
            => IVirtuademyFramework.Current.Session.OtherPlayerLeft += handler;

        protected override void Unsubscribe(Action<int, string> handler)
            => IVirtuademyFramework.Current.Session.OtherPlayerLeft -= handler;

        protected override (int, string) GetArguments(GraphReference reference, int userId, string sessionId)
        {
            return (userId, sessionId);
        }
    }
}
