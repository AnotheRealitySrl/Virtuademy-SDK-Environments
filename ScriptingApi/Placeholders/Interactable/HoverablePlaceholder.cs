using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(Collider))]
    public class HoverablePlaceholder : SceneComponentPlaceholderBase
    {
        private string hoverActionName = "TriggerHoverEvent";
        private string unhoverActionName = "TriggerUnhoverEvent";

        public string HoverActionName => hoverActionName;
        public string UnhoverActionName => unhoverActionName;
    }
}
