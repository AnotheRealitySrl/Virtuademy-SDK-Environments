using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class MascottePlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField]
        private string mascotteName;
        [Header("Animator")]
        [SerializeField]
        private Animator animator;
        [Header("Pan")]
        [SerializeField]
        private bool panOnInit;

        public Animator Animator { get => animator; }
        public bool PanOnInit { get => panOnInit; }
        public string MascotteName { get => mascotteName; set => mascotteName = value; }
    }
}
