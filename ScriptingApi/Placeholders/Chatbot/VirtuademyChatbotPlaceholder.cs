using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RenamedFrom("Virtuademy.Environments.ScriptingApi.Placeholders.ReflectisChatbotPlaceholder")]
    public class VirtuademyChatbotPlaceholder : ChatbotPlaceholderBase
    {
        [Tooltip("This is the name of the agent that you create in the section Virtuademy AI of the Back Office." +
            "Copy-paste the name here to make this chatbot use such agent.")]
        [SerializeField] private string agent = "minimario";

        public string Agent => agent;
    }
}
