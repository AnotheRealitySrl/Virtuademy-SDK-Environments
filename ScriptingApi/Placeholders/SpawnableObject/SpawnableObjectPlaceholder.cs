using Virtuademy.Environments.ScriptingApi.Placeholders;
using System.Collections.Generic;
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
                if (p.AutomaticSetup)
                {
                    Undo.RecordObject(p, "Update Spawnable Object");
                    p.AutomaticSetup = false;
                }
            }
            if (!isCopy && !AutomaticSetup)
            {
                Undo.RecordObject(this, "Update Spawnable Object");
                AutomaticSetup = true;
            }

            if (rightHandPivot == null)
            {
                Undo.RecordObject(this, "Create Hand Pivot");
                rightHandPivot = CreatePivot("RightHandPivot");
            }

            if (leftHandPivot == null)
            {
                Undo.RecordObject(this, "Create Hand Pivot");
                leftHandPivot = CreatePivot("LeftHandPivot");
            }

            // Check if we're in Prefab Mode (isolated editing view)

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            bool isInPrefabMode = prefabStage != null && prefabStage.IsPartOfPrefabContents(gameObject);
            bool isActualPrefabAsset = EditorUtility.IsPersistent(gameObject)
                                       && PrefabUtility.IsPartOfPrefabAsset(gameObject);

            bool isSceneInstance = !EditorUtility.IsPersistent(gameObject)
                                   && !PrefabUtility.IsPartOfPrefabAsset(gameObject)
                                    && !isInPrefabMode; // <-- critical exclusion

            //Check whether or not I am a prefab and I am in prefab mode
            if (isActualPrefabAsset || isInPrefabMode)
            {
                SpawnableObjectListData spawnList = LoadOrCreateData();
                if (spawnList == null) return;

                // Resolve the actual prefab asset, not the staging copy
                GameObject prefabAsset = isInPrefabMode
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath)
                    : gameObject;
                if (prefabAsset == null) return;

                spawnList.spawnableObjectList ??= new List<GameObject>();
                bool listChanged = false;

                // Dedup first
                List<GameObject> deduplicated = spawnList.spawnableObjectList
                    .Where(obj => obj != null)
                    .GroupBy(obj => AssetDatabase.GetAssetPath(obj))
                    .Select(g => g.First())
                    .ToList();
                if (!deduplicated.SequenceEqual(spawnList.spawnableObjectList))
                {
                    spawnList.spawnableObjectList = deduplicated;
                    listChanged = true;
                }

                // Add the real asset reference
                int countBefore = spawnList.spawnableObjectList.Count;
                int index = spawnList.AddToList(prefabAsset);
                listChanged |= spawnList.spawnableObjectList.Count != countBefore;

                if (indexSpawnReference != index || listToUse != 0)
                {
                    indexSpawnReference = index;
                    listToUse = 0;
                    EditorUtility.SetDirty(this);
                }

                if (listChanged)
                {
                    EditorUtility.SetDirty(spawnList);
                    ScheduleSaveAssets();
                }
            }
            else if (isSceneInstance)
            {
                //Not in prefab mode, search for the object "SpawnableObjectsHolder" and add the item to the go.
                GameObject spawnableObjectsHolder = GameObject.Find("SpawnableObjectsHolder");
                if (spawnableObjectsHolder == null)
                {
                    spawnableObjectsHolder = new GameObject("SpawnableObjectsHolder");
                    spawnableObjectsHolder.AddComponent<SpawnableObjectListReference>();
                }

                SpawnableObjectListReference spawnListReference = spawnableObjectsHolder.GetComponent<SpawnableObjectListReference>();
                if (spawnListReference == null)
                {
                    spawnListReference = spawnableObjectsHolder.AddComponent<SpawnableObjectListReference>();
                }

                int countBefore = spawnListReference.sceneSpawnableObjectList.Count;
                int index = spawnListReference.AddSceneObjectToList(this);

                if (indexSpawnReference != index || listToUse != 1)
                {
                    indexSpawnReference = index;
                    listToUse = 1;
                    EditorUtility.SetDirty(this);
                }

                // Scene data: it is saved with the scene, so there is no asset to save here.
                if (spawnListReference.sceneSpawnableObjectList.Count != countBefore)
                {
                    EditorUtility.SetDirty(spawnListReference);
                }
            }
        }

        private static bool saveAssetsScheduled;

        /// <summary>Saves the assets once, after the validations of this editor tick, however many placeholders asked.</summary>
        private static void ScheduleSaveAssets()
        {
            if (saveAssetsScheduled)
            {
                return;
            }

            saveAssetsScheduled = true;
            EditorApplication.delayCall += () =>
            {
                saveAssetsScheduled = false;
                AssetDatabase.SaveAssets();
            };
        }

        private Transform CreatePivot(string pivotName)
        {
            GameObject pivotGO = new GameObject(pivotName);
            Undo.RegisterCreatedObjectUndo(pivotGO, "Create Hand Pivot");
            pivotGO.transform.parent = transform;
            pivotGO.transform.localPosition = Vector3.zero;
            EditorUtility.SetDirty(this);
            return pivotGO.transform;
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
                if (prefab == null || parentTransform == null)
                {
                    Debug.LogWarning($"Cannot show the hand reference: {(prefab == null ? "prefab not found at " + path : "the hand pivot does not exist yet")}.", this);
                    return;
                }

                hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentTransform);
                Undo.RegisterCreatedObjectUndo(hand, "Show Hand Reference");
                hand.transform.localPosition = Vector3.zero;
                Undo.RecordObject(this, "Show Hand Reference");
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
                Undo.DestroyObjectImmediate(hand.gameObject);
                hand = null;
            }
        }

        public void SetPositionLeftHand()
        {
            if (!symmetricHandPositioning || leftHandPivot == null || rightHandPivot == null)
            {
                return;
            }

            //leftSpawnPositionOffset = LeftHandReference.transform;
            Vector3 rightPosition = rightHandPivot.localPosition;
            Vector3 rightRotation = rightHandPivot.localEulerAngles;
            Vector3 leftPosition = new Vector3(-rightPosition.x, rightPosition.y, rightPosition.z);
            Vector3 leftRotation = new Vector3(rightRotation.x, -rightRotation.y, -rightRotation.z);

            // The inspector calls this on every repaint: the pivot is touched (and recorded for Undo) only when it
            // is not already the mirror of the right one.
            if (leftHandPivot.localPosition == leftPosition
                && Quaternion.Angle(leftHandPivot.localRotation, Quaternion.Euler(leftRotation)) < 0.001f)
            {
                return;
            }

            Undo.RecordObject(leftHandPivot, "Mirror Left Hand Pivot");
            leftHandPivot.localPosition = leftPosition;
            leftHandPivot.localEulerAngles = leftRotation;
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
