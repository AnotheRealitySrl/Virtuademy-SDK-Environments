using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class VoiceChatGroupPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField] private string voiceRoomName = "Global";
        [SerializeField] private bool isMainChannel = false;

        public string VoiceRoomName => voiceRoomName;
        public bool IsMainChannel => isMainChannel;
    }
}
