using Virtuademy.SDK.Environments;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using System.IO;


#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif



namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class SpawnableHandlerPlaceholder : SceneComponentPlaceholderBase
    {

        protected string spawnableObjectListPath = "Assets/SpawnableObject/SpawnableObjectList.asset";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!string.IsNullOrEmpty(gameObject.scene.name))
            {
                SpawnableObjectListData spawnList = LoadOrCreateData();
                SpawnableObjectListReference spawnableReference = FindFirstObjectByType<SpawnableObjectListReference>();
                if (spawnableReference == null)
                {
                    GameObject emptyGO = new GameObject("SpawnableReference");
                    emptyGO.AddComponent<SpawnableObjectListReference>();
                    SpawnableObjectListReference spawnRef = emptyGO.GetComponent<SpawnableObjectListReference>();
                    spawnRef.SetReference(spawnList);
                }
            }
        }

        private SpawnableObjectListData LoadOrCreateData()
        {
            SpawnableObjectListData spawnableList = AssetDatabase.LoadAssetAtPath<SpawnableObjectListData>(spawnableObjectListPath);
            if (spawnableList == null)
            {
                // Ensure folder exists
                string dir = Path.GetDirectoryName(spawnableObjectListPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Create instance and save
                spawnableList = ScriptableObject.CreateInstance<SpawnableObjectListData>();
                AssetDatabase.CreateAsset(spawnableList, spawnableObjectListPath);
                Debug.Log("Created new Gimmi.asset at " + spawnableObjectListPath);
            }

            return spawnableList;
        }
#endif


    }
}
