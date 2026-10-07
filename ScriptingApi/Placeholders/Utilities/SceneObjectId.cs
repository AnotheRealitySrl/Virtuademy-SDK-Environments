#if UNITY_EDITOR
using UnityEditor;

using UnityEditor.SceneManagement;

#endif
using UnityEngine;

using System.Collections.Generic;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class SceneObjectId : MonoBehaviour
    {

        [SerializeField, HideInInspector]
        private int uniqueID;

        public int UniqueID
        {
            get
            {
                if (uniqueID == 0 && !string.IsNullOrEmpty(gameObject.scene.name))
                {
                    GenerateObjectUniqueID();
                }
                return uniqueID;
            }
            set => uniqueID = value;
        }

        private void Reset()
        {
            GenerateObjectUniqueID();
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(gameObject.scene.name))
            {
                foreach (var item in FindObjectsByType<SceneObjectId>(FindObjectsSortMode.None))
                {
                    if (item != this && item.UniqueID == uniqueID)
                    {
                        uniqueID = GenerateNewUniqueID();
                        if (!Application.isPlaying)
                        {
                            EditorUtility.SetDirty(this);
                        }
                        return;
                    }
                }
            }
        }
#endif

        [ContextMenu("Generate Unique ID")]
        public void GenerateObjectUniqueID()
        {
            //Debug.Log($"Generating Unique ID for {gameObject.name}", gameObject);
            //If it is a prefab in the project, do not generate a new ID
            if (string.IsNullOrEmpty(gameObject.scene.name))
            {
                uniqueID = 0;
            }
            else
            {
                if (uniqueID == 0)
                {
                    uniqueID = GenerateNewUniqueID();
                }
            }
            Debug.Log($"Generated Unique ID {uniqueID} for {gameObject.name}", gameObject);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SerializedObject so = new SerializedObject(this);
                so.FindProperty("uniqueID").intValue = uniqueID;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(this);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }


        public static int GenerateNewUniqueID()
        {
            List<int> ids = new List<int>();
            var objects = FindObjectsByType<SceneObjectId>(FindObjectsSortMode.None);
            //Using select makes unity crash if we are adding a gameobject istance to the scene in editor
            foreach (var item in objects)
            {
                ids.Add(item.uniqueID);
            }
            int uniqueID = Random.Range(1, int.MaxValue);
            while (ids.Contains(uniqueID))
            {
                uniqueID = Random.Range(1, int.MaxValue);
            }
            return uniqueID;
        }
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(SceneObjectId))]
public class UniqueIDAssignerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Ottieni il riferimento al componente
        SceneObjectId assigner = (SceneObjectId)target;

        // Mostra un messaggio con l'ID univoco
        EditorGUILayout.LabelField($"ObjectID: [{assigner.UniqueID}]");

    }
}
#endif
