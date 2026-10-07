
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(SceneObjectId))]
    public abstract class SceneComponentPlaceholderBase : MonoBehaviour
    {
        protected SceneObjectId sceneObjectId;

        [SerializeField] private bool automaticSetup = true;

        public bool AutomaticSetup { get => automaticSetup; set => automaticSetup = value; }

        public int UniqueID
        {
            get
            {
                if (sceneObjectId == null)
                {
                    sceneObjectId = GetComponent<SceneObjectId>();
                }
                return GetComponent<SceneObjectId>().UniqueID;
            }
        }
    }
}