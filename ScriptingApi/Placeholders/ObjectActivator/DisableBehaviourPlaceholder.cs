using Virtuademy.Environments.ScriptingApi.Placeholders;

using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class DisableBehaviourPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField]
        private List<MonoBehaviour> disableInVR = new List<MonoBehaviour>();

        [SerializeField]
        private List<MonoBehaviour> disableInWebGL = new List<MonoBehaviour>();

        [SerializeField]
        private List<MonoBehaviour> disableInMobile = new List<MonoBehaviour>();

        public List<MonoBehaviour> DisableInVR { get => disableInVR; set => disableInVR = value; }
        public List<MonoBehaviour> DisableInWebGL { get => disableInWebGL; set => disableInWebGL = value; }
        public List<MonoBehaviour> DisableInMobile { get => disableInMobile; set => disableInMobile = value; }
    }
}
