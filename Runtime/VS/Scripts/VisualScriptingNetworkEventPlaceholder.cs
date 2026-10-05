using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    public class VisualScriptingNetworkEventPlaceholder : SceneComponentPlaceholderNetwork
    {
        [HideInInspector]
        public UnityEvent<string> action = new UnityEvent<string>();

        public void ActionInvoke(string eventName)
        {
            action?.Invoke(eventName);
        }
    }
}