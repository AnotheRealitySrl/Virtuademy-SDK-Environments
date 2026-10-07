using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;
using UnityEngine.InputSystem;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(SpawnObjectData))]
    public class ObjectSpawnerPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField]
        private InputActionReference vrInput;
        [SerializeField]
        private InputActionReference desktopInput;

        public InputActionReference VrInput { get => vrInput; }
        public InputActionReference DesktopInput { get => desktopInput; }
    }
}
