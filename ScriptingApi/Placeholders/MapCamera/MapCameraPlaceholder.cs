using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(Camera))]
    public class MapCameraPlaceholder : SceneComponentPlaceholderBase
    {
        public Camera Cam => GetComponent<Camera>();

        private void Awake()
        {
            Cam.enabled = false;
        }
    }
}