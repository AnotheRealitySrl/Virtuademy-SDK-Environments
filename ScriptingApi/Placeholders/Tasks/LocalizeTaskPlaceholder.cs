using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class LocalizeTaskPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField, Tooltip("Enable description localization")]
        public bool localizeDescription = false;
        [Tooltip("Enable image localization")]
        private bool localizeImage = false;
        [Tooltip("Enable videoClip localization")]
        private bool localizeVideo = false;

        [Space]
        [SerializeField, Tooltip("String key relative to the task's description")]
        public string descriptionKey;
        [Tooltip("String key relative to the task's sprite")]
        private string imageKey;
        [Tooltip("String key relative to the task's video")]
        private string videoKey;
    }
}
