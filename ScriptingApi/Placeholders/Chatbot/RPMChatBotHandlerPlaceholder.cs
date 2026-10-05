using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class RPMChatBotHandlerPlaceholder : SceneComponentPlaceholderBase
    {
        public enum EVoiceHandlerAudioProvider
        {
            Microphone,
            AudioClip
        }

        [SerializeField] private bool useEyeAnimationHandler = true;

        [SerializeField, DrawIf(nameof(useEyeAnimationHandler), true), Range(0, 1)] private float blinkDuration = 0.1f;
        [SerializeField, DrawIf(nameof(useEyeAnimationHandler), true), Range(0, 10)] private float blinkInterval = 3f;

        [SerializeField] private bool useVoiceHandler = true;

        [SerializeField, DrawIf(nameof(useVoiceHandler), true)] private AudioClip audioClip;
        [SerializeField, DrawIf(nameof(useVoiceHandler), true)] private AudioSource audioSource;
        [SerializeField, DrawIf(nameof(useVoiceHandler), true)] private EVoiceHandlerAudioProvider audioProvider = EVoiceHandlerAudioProvider.AudioClip;

        public bool UseEyeAnimationHandler => useEyeAnimationHandler;
        public float BlinkDuration => blinkDuration;
        public float BlinkInterval => blinkInterval;

        public bool UseVoiceHandler => useVoiceHandler;
        public AudioClip AudioClip => audioClip;
        public AudioSource AudioSource => audioSource;
        public EVoiceHandlerAudioProvider AudioProvider => audioProvider;
    }
}
