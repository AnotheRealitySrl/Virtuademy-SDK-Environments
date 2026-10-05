using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class DisableColliderPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField]
        private List<Collider> disableInVR = new List<Collider>();

        [SerializeField]
        private List<Collider> disableInWebGL = new List<Collider>();

        [SerializeField]
        private List<Collider> disableInMobile = new List<Collider>();

        public List<Collider> DisableInVR { get => disableInVR; set => disableInVR = value; }
        public List<Collider> DisableInWebGL { get => disableInWebGL; set => disableInWebGL = value; }
        public List<Collider> DisableInMobile { get => disableInMobile; set => disableInMobile = value; }
    }
}
