using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;
using UnityEngine.Events;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public abstract class ChatbotPlaceholderBase : SceneComponentPlaceholderBase
    {
        [Header("Chatbot structure.\n" +
             "Do not modify it, unless you need to create custom avatars.")]
        [SerializeField] protected Transform avatarContainer;
        [SerializeField] protected RectTransform chatPanel;

        [Tooltip("By default, the animator is searched automatically in avatar's hierarchy. " +
            "Specify the animator in the case the avatar in use has multiple animators in the hierarchy.")]
        [SerializeField] protected Animator animator;

        [Header("Chatbot parameters")]
        [Tooltip("Select this if the conversation with the chatbot should start automatically, " +
            "without waiting for the user to send the first input.")]
        [SerializeField] private bool startTheConversation = true;

        [DrawIf(nameof(startTheConversation), true)]
        [Tooltip("The initial sentence that is used by the user to start the conversation. " +
            "This will be sent \"under the hood\" to the chatbot, and will not be displayed in the UI.")]
        [SerializeField] private string initialConversationSentence;


        public Transform AvatarContainer => avatarContainer;
        public RectTransform ChatPanel => chatPanel;
        public Animator Animator => animator;

        public bool StartTheConversation => startTheConversation;
        public string InitialConversationSentence => initialConversationSentence;

        public UnityEvent OnChatBotSelect { get; } = new();
        public UnityEvent OnChatBotUnselected { get; } = new();
    }
}
