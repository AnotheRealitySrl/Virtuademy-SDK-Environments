using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;

using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy ChatBot: ChatBot Select")]
    [UnitSurtitle("ChatBot")]
    [UnitShortTitle("ChatBot Select")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ChatBotSelectUnit : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [DoNotSerialize]
        [NullMeansSelf]
        [Serialize]
        [AllowsNull]
        [PortLabel("Target")]
        public ValueInput Target { get; private set; }


        protected override void Definition()
        {
            Target = ValueInput<GameObject>(nameof(Target), null).NullMeansSelf();

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                f.GetValue<GameObject>(Target).GetComponent<ChatbotPlaceholderBase>().OnChatBotSelect?.Invoke();

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

    }
}
