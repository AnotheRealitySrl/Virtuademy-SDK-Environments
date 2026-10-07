using System.Collections.Generic;
using UnityEngine;

namespace Virtuademy.SDK.Environments
{
    public class SpawnableObjectListReference : MonoBehaviour
    {
        public static SpawnableObjectListReference Instance { get; private set; }

        public SpawnableObjectListData spawnObjectList;
        public List<SpawnableObjectPlaceholder> sceneSpawnableObjectList = new List<SpawnableObjectPlaceholder>();

        private void Awake()
        {
            //Debug.LogError("SpawnObnject list is " + spawnObjectList, gameObject);
            // 1. Check if an instance already exists
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 2. Set the Instance
            Instance = this;

        }

        public int SubstituteSceneGO(int index, GameObject copy)
        {
            //Debug.LogError("Index is " + index);
            sceneSpawnableObjectList[index] = copy.GetComponent<SpawnableObjectPlaceholder>();
            return index;
        }

        public int GetIndex(SpawnableObjectPlaceholder go)
        {
            return sceneSpawnableObjectList.IndexOf(go);
        }

        public void SetReference(SpawnableObjectListData data)
        {
            spawnObjectList = data;
        }

        public int AddSceneObjectToList(SpawnableObjectPlaceholder obj)
        {
            //Debug.LogError("ADD SCENE OBJECT OT LIST");
            if (sceneSpawnableObjectList.Contains(obj))
            {
                return sceneSpawnableObjectList.IndexOf(obj);
            }
            else
            {
                sceneSpawnableObjectList.Add(obj);
                return sceneSpawnableObjectList.IndexOf(obj);
            }
        }

    }
}
