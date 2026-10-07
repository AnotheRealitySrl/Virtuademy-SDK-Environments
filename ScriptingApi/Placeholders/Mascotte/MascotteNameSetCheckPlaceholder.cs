using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class MascotteNameSetCheckPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField] private string mascotteName;

        public string MascotteName { get => mascotteName; set => mascotteName = value; }
    }
}
