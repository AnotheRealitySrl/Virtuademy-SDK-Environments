using Virtuademy.Environments.ScriptingApi.ChatBot;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ChatBotPlaceholder : ChatbotPlaceholderBase
    {
        [Space]

        [Header("Chatbot configuration")]
        [HelpBox("How to configure a chatbot, using the template avatars:\n" +
            "First of all, drag&drop one of the avatar templates inside the \"Avatar\" transform in the hierarchy. " +
            "Move or resize its transform, if needed. " +
            "IMPORTANT: to enable user's interaction, " +
            "Add a reference to the collider of the avatar to the \"InteractablePlaceholder\"'s collider list. " +
            "\n" +
            "\n" +
            "How to integrate a custom avatar:\n" +
            "Add the custom avatar to the \"Avatar\" transform in the hierarchy. " +
            "Check that the avatar has a collider, needed for the interaction with the chatbot. " +
            "If the avatar is a RPM avatar and you want to add eye-blink and lip-sync behaviors, " +
            "add respectively the \"EyeAnimationHandler\" and \"VoiceHandler\" components to the avatar " +
            "added. Set the \"AudioProvider\" field of \"VoiceHandler\" to \"Audio Clip\".\n" +
            "Add a reference of the audio source of the avatar into the \"VoiceHandler\" component. " +
            "If you want to move the audio source, to personalize where comes the audio of the avatar, " +
            "move it into a transform, keeping its reference to \"VoiceHandler\" component. " +
            "You can add an animator to the avatar, in which you can define any state, " +
            "but keep in mind that, in order to configure one or more speech animations properly, " +
            "the trigger that activates the transitions to/from those states must be a boolean" +
            "with the name \"Speak\".", HelpBoxMessageType.Info)]


        [Tooltip("This will be the name of the avatar, displayed in the chat panel.")]
        [SerializeField] private string chatbotName = "ChatBot";

        [SerializeField, TextArea(10, 30)]
        [Tooltip("Specify here the behaviour of the chatbot, in natural language.")]
        private string instructions;

        [Tooltip("Select avatar voice from the available ones.")]
        [SerializeField] private EChatBotVoice voice = EChatBotVoice.alloy;

        public string ChatbotName => chatbotName;
        public string Instructions => instructions;
        public EChatBotVoice Voice => voice;
    }
}
