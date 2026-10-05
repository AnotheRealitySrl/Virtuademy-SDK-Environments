using Virtuademy.Environments.ScriptingApi.Placeholders;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

#endif
using UnityEngine;


namespace Virtuademy.SDK.Environments
{
    public class SpawnableObjectPlaceholder : SceneComponentPlaceholderBase
    {

        private const string SpawnableObjectListDataPath = "Assets/SpawnableObject/SpawnableObjectList.asset";
        private const string LeftHandReferencePrefabPath = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Prefab/LeftHandReference.prefab";
        private const string RightHandReferencePrefabPath = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Prefab/RightHandReference.prefab";

        private bool _isSceneObject;
        public bool IsSceneObject => _isSceneObject;

        [HideInInspector] public bool spawnInHand = true; //in the future add spawnOnHand bool and if false make the spawner know where to spawn this certain item (whether in front of user or in a certain transform in env).

        //------------ These 3 parameters will be hidden if spawnInHand is false.
        public bool symmetricHandPositioning = true;
        [HideInInspector] public Transform leftHandPivot;
        [HideInInspector] public Transform rightHandPivot;
        //------------------

        [HideInInspector] public GameObject LeftHandReference;
        [HideInInspector] public GameObject RightHandReference;

        [HideInInspector] public bool isCopy = false; //Is it the copy? 
        public int indexSpawnReference = 0; //Index inside the list
        [HideInInspector] public int listToUse = 1; //Whether or not it is ther scene list or prefab list

        //Function called when the object gets instantiated, destroy the hand references
        public void DestroyHands()
        {
            if (LeftHandReference != null)
            {
                Destroy(LeftHandReference);
            }

            if (RightHandReference != null)
            {
                Destroy(RightHandReference);
            }
        }

        public void Awake()
        {
            //Se sono prefab non mettere a false;
            SceneComponentPlaceholderBase[] placeholders = GetComponentsInChildren<SceneComponentPlaceholderBase>();
            foreach(var p in placeholders)
            {
                p.AutomaticSetup = false;
            }
            if (!isCopy)
            {
                AutomaticSetup = true;
            }
            DestroyHands();
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.delayCall -= OnValidateDelayed; // prevent stacking
            EditorApplication.delayCall += OnValidateDelayed;
        }

        private void OnValidateDelayed()
        {
            if (this == null || gameObject == null) return;

            SceneComponentPlaceholderBase[] placeholders = GetComponentsInChildren<SceneComponentPlaceholderBase>();
            foreach (var p in placeholders)
            {
                p.AutomaticSetup = false;
            }
            if (!isCopy)
            {
                AutomaticSetup = true;
            }

            if (rightHandPivot == null)
            {
                GameObject rightPivotGO = new GameObject("RightHandPivot");
                rightPivotGO.transform.parent = transform;
                rightPivotGO.transform.localPosition = Vector3.zero;
                rightHandPivot = rightPivotGO.transform;
            }

            if (leftHandPivot == null)
            {
                GameObject leftPivotGO = new GameObject("LeftHandPivot");
                leftPivotGO.transform.parent = transform;
                leftPivotGO.transform.localPosition = Vector3.zero;
                leftHandPivot = leftPivotGO.transform;
            }

            // Check if we're in Prefab Mode (isolated editing view)

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            bool isInPrefabMode = prefabStage != null && prefabStage.IsPartOfPrefabContents(gameObject);
            bool isActualPrefabAsset = EditorUtility.IsPersistent(gameObject)
                                       && PrefabUtility.IsPartOfPrefabAsset(gameObject);

            bool isSceneInstance = !EditorUtility.IsPersistent(gameObject)
                                   && !PrefabUtility.IsPartOfPrefabAsset(gameObject)
                                    && !isInPrefabMode; // <-- critical exclusion

            Debug.LogError($"OnValidate called — IsPartOfPrefabAsset: {PrefabUtility.IsPartOfPrefabAsset(gameObject)}, " +
              $"IsPersistent: {EditorUtility.IsPersistent(gameObject)}, " +
              $"name: {gameObject.name}", gameObject);

            //Check whether or not I am a prefab and I am in prefab mode 
            if (isActualPrefabAsset || isInPrefabMode)
            {
                SpawnableObjectListData spawnList = LoadOrCreateData();
                if (spawnList == null) return;

                // Resolve the actual prefab asset, not the staging copy
                GameObject prefabAsset = isInPrefabMode
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(
                        PrefabStageUtility.GetCurrentPrefabStage().assetPath)
                    : gameObject;

                // Dedup first
                spawnList.spawnableObjectList = spawnList.spawnableObjectList
                    .Where(obj => obj != null)
                    .GroupBy(obj => AssetDatabase.GetAssetPath(obj))
                    .Select(g => g.First())
                    .ToList();

                // Add the real asset reference
                int index = spawnList.AddToList(prefabAsset);
                indexSpawnReference = index;
                listToUse = 0;

                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(spawnList);
                EditorApplication.delayCall += () =>
                {
                    EditorUtility.SetDirty(spawnList);
                    AssetDatabase.SaveAssets();
                };
            }
            else if (isSceneInstance)
            {
                Debug.LogError("Not in prefab mode");
                //Not in prefab mode, search for the object "SpawnableObjectsHolder" and add the item to the go.
                GameObject spawnableObjectsHolder = GameObject.Find("SpawnableObjectsHolder");
                if (spawnableObjectsHolder == null)
                {
                    spawnableObjectsHolder = new GameObject("SpawnableObjectsHolder");
                    spawnableObjectsHolder.AddComponent<SpawnableObjectListReference>();
                }

                SpawnableObjectListReference spawnListReference = spawnableObjectsHolder.GetComponent<SpawnableObjectListReference>();
                int index = spawnListReference.AddSceneObjectToList(this);
                Debug.LogError("Setting index to " + index, gameObject);
                indexSpawnReference = index;
                listToUse = 1;

                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(spawnListReference);
                EditorApplication.delayCall += () =>
                {
                    if (spawnListReference != null)
                        EditorUtility.SetDirty(spawnListReference);
                    AssetDatabase.SaveAssets();
                };


            }
        }

        private static SpawnableObjectListData LoadOrCreateData()
        {
            SpawnableObjectListData spawnableList = AssetDatabase.LoadAssetAtPath<SpawnableObjectListData>(SpawnableObjectListDataPath);
            if (spawnableList == null)
            {
                // Ensure folder exists
                string dir = Path.GetDirectoryName(SpawnableObjectListDataPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Create instance and save
                spawnableList = ScriptableObject.CreateInstance<SpawnableObjectListData>();
                AssetDatabase.CreateAsset(spawnableList, SpawnableObjectListDataPath);
            }

            return spawnableList;
        }

        public void ShowHandReference(GameObject HandReference, int handType)
        {
            GameObject hand = null;
            string path = "";
            Transform parentTransform;
            if (handType == 0){
                path = LeftHandReferencePrefabPath;
                hand = LeftHandReference;
                parentTransform = leftHandPivot;
            }
            else
            {
                path = RightHandReferencePrefabPath;
                hand = RightHandReference;
                parentTransform = rightHandPivot;
            }

            //Add the hand reference
            if (hand == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentTransform);
                hand.transform.localPosition = Vector3.zero;
                if (handType == 0)
                {
                    LeftHandReference = hand;
                }
                else
                {
                    RightHandReference = hand;
                }
                   
                EditorUtility.SetDirty(this);
            }
            else
            {
                DestroyImmediate(hand.gameObject);
                hand = null;
            }
        }

        public void SetPositionLeftHand()
        {
            if (leftHandPivot != null)
            {
                //leftSpawnPositionOffset = LeftHandReference.transform;
                if (symmetricHandPositioning)
                {
                    leftHandPivot.transform.localPosition = new Vector3(-rightHandPivot.transform.localPosition.x, rightHandPivot.transform.localPosition.y, rightHandPivot.transform.localPosition.z);
                    Vector3 rightRotation = rightHandPivot.transform.localEulerAngles;
                    leftHandPivot.transform.localEulerAngles = new Vector3(rightRotation.x, -rightRotation.y, -rightRotation.z);
                }
            }

        }

        /*public void SetPositionRightHand()
        {
            if (RightHandReference != null)
            {
                rightHandPivot = RightHandReference.transform;
            }
        }*/

        /*private void OnDestroy()
        {
            // Only handle editor-time logic (not during play mode)
            if (Application.isPlaying)
                return;

            Debug.LogError("DESTROY");
            // We're destroying the component (or the whole GameObject)
            SpawnableObjectListData spawnList = AssetDatabase.LoadAssetAtPath<SpawnableObjectListData>(SpawnableObjectListDataPath);
            if (spawnList == null) return;

            if (spawnList.spawnableObjectList.Contains(gameObject))
            {
                spawnList.spawnableObjectList.Remove(gameObject);
                EditorUtility.SetDirty(spawnList);
                AssetDatabase.SaveAssets();
                Debug.Log($"Removed {name} from spawnable list (component removed or destroyed)");
            }
        }*/
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SpawnableObjectPlaceholder))]
    public class SpawnableObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SpawnableObjectPlaceholder myScript = (SpawnableObjectPlaceholder)target;
            string leftButtonLabel = myScript.LeftHandReference == null ? "Show Left Reference" : "Hide Left Reference";
            string rigthButtonLabel = myScript.RightHandReference == null ? "Show Right Reference" : "Hide Right Reference";

            myScript.SetPositionLeftHand();
            //myScript.SetPositionRightHand();

            if (GUILayout.Button(leftButtonLabel))
            {
                myScript.ShowHandReference(myScript.LeftHandReference, 0);
            }
            if (GUILayout.Button(rigthButtonLabel))
            {
                myScript.ShowHandReference(myScript.RightHandReference, 1);
            }
        }
    }
#endif
}
