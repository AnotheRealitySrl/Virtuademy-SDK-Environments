using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class TeleportationPlaceholder : SceneComponentPlaceholderBase
    {
        [Header("Common references")]
        [SerializeField, Tooltip("If this object is the teleporter, drag its own collider here")] private Collider teleportCollider;
        [SerializeField, Tooltip("Drag the destination point that has the TeleportDestinationPlaceholder script attached to it")] private TeleportationDestinationPlaceholder teleportDestination;

        [Header("VR references")]
        [SerializeField] private GameObject customReticleVR;

        public Collider TeleportCollider => teleportCollider;
        public TeleportationDestinationPlaceholder TeleportDestination => teleportDestination;
        public GameObject CustomReticleVR => customReticleVR;
    }
}