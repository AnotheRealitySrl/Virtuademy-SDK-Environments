using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class MirrorPlaceholder : SceneComponentPlaceholderBase, IAddressablePlaceholder
    {
        [SerializeField] private string addressableKey;

        [SerializeField] private Transform panTransform;
        [SerializeField] private Transform teleportTarget;

        public string AddressableKey => addressableKey;

        public Transform PanTransform => panTransform;
        public Transform TeleportTarget => teleportTarget;
    }
}
