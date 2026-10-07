using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Virtuademy.SDK.Environments
{
    [CreateAssetMenu(menuName = "Virtuademy/SDK-ObjectSpawner/SpawnableObjectListData", fileName = "SpawnableObjectListData")]
    public class SpawnableObjectListData : ScriptableObject
    {
        public List<GameObject> spawnableObjectList;

        public int AddToList(GameObject spawnObject)
        {
#if UNITY_EDITOR
            string targetPath = AssetDatabase.GetAssetPath(spawnObject);
            if (!string.IsNullOrEmpty(targetPath))
            {
                // Check if already present BEFORE adding
                int existingIndex = spawnableObjectList.FindIndex(obj =>
                    obj != null && AssetDatabase.GetAssetPath(obj) == targetPath);

                if (existingIndex != -1)
                    return existingIndex; // Already there, don't add again

                spawnableObjectList.Add(spawnObject);
                return spawnableObjectList.Count - 1; // Safe: we just added it at the end
            }
#endif
            // Runtime: check before adding
            int runtimeIndex = spawnableObjectList.IndexOf(spawnObject);
            if (runtimeIndex != -1)
                return runtimeIndex;

            spawnableObjectList.Add(spawnObject);
            return spawnableObjectList.Count - 1;
        }

        public List<GameObject> GetList()
        {
            return spawnableObjectList;
        }

        public int GetObjectIndex(GameObject value)
        {
            /*if (spawnableObjectList.Contains(value))
            {
                return spawnableObjectList.IndexOf(value);
            }
            else
            {
                return -1;
            }*/

#if UNITY_EDITOR
            string targetPath = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(targetPath))
            {
                return spawnableObjectList.FindIndex(obj =>
                    obj != null && AssetDatabase.GetAssetPath(obj) == targetPath);
            }
#endif
            // Runtime path — plain reference equality
            return spawnableObjectList.IndexOf(value);
        }

        public GameObject GetObjectInPosition(int index)
        {
            return spawnableObjectList[index];
        }
    }
}
