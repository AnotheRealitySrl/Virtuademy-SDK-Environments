using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class SceneChangerPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField, Tooltip("Name of an environment that has a static event associated from the backoffice")]
        private string sceneAddressableName;

        [SerializeField, Tooltip("If true, the environment selected will be choisen from the tenant environments. Default = false")]
        private bool isTenantEnvironment = false;

        public string SceneAddressableName => sceneAddressableName;
        //public bool IsTenantEnvironment => isTenantEnvironment;
    }
}
