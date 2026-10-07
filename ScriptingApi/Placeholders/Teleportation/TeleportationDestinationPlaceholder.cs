using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class TeleportationDestinationPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField, Tooltip("Write the teleportation point's name that will appear in the map")]
        private string teleportAreaName;

        public string TeleportAreaName => teleportAreaName;
    }
}
