using Virtuademy.Environments.ScriptingApi.Placeholders;
using System.Threading.Tasks;
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public interface IPickable
    {
        Task Init(SceneComponentPlaceholderBase placeholder);
        public string GetPickableName();
    }
}
