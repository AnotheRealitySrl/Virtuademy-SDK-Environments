using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ButtonOpenAppPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField] private string appLink;

        public string AppLink => appLink;
    }
}
