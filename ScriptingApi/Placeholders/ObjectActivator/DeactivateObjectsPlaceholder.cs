using Virtuademy.Environments.ScriptingApi.Placeholders;

using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class DeactivateObjectsPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField]
        private List<GameObject> disableInVR = new List<GameObject>();

        [SerializeField]
        private List<GameObject> disableInWebGL = new List<GameObject>();

        [SerializeField]
        private List<GameObject> disableInMobile = new List<GameObject>();

        public List<GameObject> DisableInVR { get => disableInVR; set => disableInVR = value; }
        public List<GameObject> DisableInWebGL { get => disableInWebGL; set => disableInWebGL = value; }
        public List<GameObject> DisableInMobile { get => disableInMobile; set => disableInMobile = value; }
    }
}
