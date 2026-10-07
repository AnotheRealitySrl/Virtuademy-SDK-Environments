using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class FloorPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField] private GameObject customReticleVR;

        public GameObject CustomReticleVR => customReticleVR;
    }
}

