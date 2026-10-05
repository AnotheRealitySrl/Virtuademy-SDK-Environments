using Virtuademy.Environments.ScriptingApi.ObjectSpawner;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class SpawnObjectData : MonoBehaviour
    {
        [SerializeField]
        private GameObject prefab;
        [SerializeField]
        private SpawnPosition spawnPosition;

        public GameObject Prefab { get => prefab; }
        public SpawnPosition SpawnPosition { get => spawnPosition; }
    }
}
