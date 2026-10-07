using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;
using UnityEngine.Serialization;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class SceneComponentPlaceholderNetwork : SceneComponentPlaceholderBase, INetworkPlaceholder
    {
        [field: SerializeField, FormerlySerializedAs("isNetworked")] public bool IsNetworked { get; set; }
    }
}
