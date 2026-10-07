using Virtuademy.Environments.ScriptingApi.Placeholders;

using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    public class SceneObjectsManagementWindow : EditorWindow
    {
        private List<SceneObjectId> sceneObjects = new();

        private Vector2 scrollPosition = Vector2.zero;

        [MenuItem("Virtuademy/Development/Scene objects management")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            GetWindow(typeof(SceneObjectsManagementWindow));
        }

        private void OnGUI()
        {
            FindPlaceholders();
            DisplayPlaceholders();

            GUIStyle btnStyle = new(GUI.skin.button)
            {
                richText = true
            };

            if (GUILayout.Button("Initialize invalid network placeholders IDs", btnStyle))
            {
                SetPlaceholdersNewIDs();
            }
            if (GUILayout.Button("Initialize <b>ALL</b> network placeholders IDs", btnStyle))
            {
                SetPlaceholdersNewIDs(true);
            }
        }

        private void FindPlaceholders()
        {
            Scene s = SceneManager.GetActiveScene();
            GameObject[] gameObjects = s.GetRootGameObjects();

            sceneObjects.Clear();

            foreach (GameObject obj in gameObjects)
            {
                sceneObjects.AddRange(obj.GetComponentsInChildren<SceneObjectId>(true));
            }
        }

        private void SetPlaceholdersNewIDs(bool overrideAll = false)
        {
            System.Random rnd = new();

            if (overrideAll)
            {
                sceneObjects.ForEach(sceneObject =>
                {
                    sceneObject.UniqueID = 0;
                });
                sceneObjects.ForEach(sceneObject =>
                {
                    sceneObject.GenerateObjectUniqueID();
                    EditorUtility.SetDirty(sceneObject);
                });
            }
            else
            {
                // Here are stored the recalculated (or preserved if correct) previous IDs during the update session.
                List<int> updatedIds = new List<int>();

                // First of all, get distinct of all used IDs greater than 0.
                List<int> oldIds = sceneObjects.Select(p => (p.UniqueID)).Distinct().Where(id => id > 0).ToList();

                // Then, for each value, keep it if it is the first time it appears.
                // Else, if it is duplicated or 0, then recalculate it with a brand new value.
                sceneObjects.ForEach(p =>
                {
                    int currentId = (p.UniqueID);

                    bool needsOverride = currentId == 0 || !oldIds.Contains(currentId);

                    if (needsOverride)
                    {
                        p.UniqueID = 0;
                        p.GenerateObjectUniqueID();
                        EditorUtility.SetDirty(p);
                    }
                    else
                    {
                        oldIds.Remove(currentId);
                    }

                    updatedIds.Add((p.UniqueID));
                });
            }
        }

        private void DisplayPlaceholders()
        {
            GUIStyle style = new(EditorStyles.label)
            {
                richText = true
            };

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);
            {
                Dictionary<int, string> usedCodes = new Dictionary<int, string>();

                foreach (var objectId in sceneObjects)
                {
                    int tmpCode = objectId.UniqueID;

                    string tmpCodeStr = tmpCode.ToString();
                    string tmpFeedback = string.Empty;

                    if (tmpCode != 0)
                    {
                        if (usedCodes.TryGetValue(tmpCode, out string alreadyUsedGOName))
                        {
                            // Code already used.
                            tmpCodeStr = "<color=red>" + tmpCodeStr + "</color>";
                            tmpFeedback = "Used by: <b>" + alreadyUsedGOName + "</b>";
                        }
                        else
                        {
                            usedCodes.Add(tmpCode, objectId.gameObject.name);
                        }
                    }
                    else
                    {
                        // Not inizialized.
                        tmpCodeStr = "<color=red>" + tmpCodeStr + "</color>";
                        tmpFeedback = "This GameObject is <b>not initialized</b>.";
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        float codeWidth = 50f;
                        float btnWidth = 20f;
                        float margin = 35f;
                        float remainingWidth = EditorGUIUtility.currentViewWidth - codeWidth - btnWidth - margin;

                        if (GUILayout.Button(">", GUILayout.Width(btnWidth)))
                        {
                            EditorGUIUtility.PingObject(objectId.gameObject);
                        }

                        EditorGUILayout.LabelField($"<b>{objectId.gameObject.name}</b>", style, GUILayout.Width(remainingWidth * 0.4f));

                        EditorGUILayout.LabelField($"{tmpCodeStr}", style, GUILayout.Width(codeWidth));

                        EditorGUILayout.LabelField($"{tmpFeedback}", style, GUILayout.Width(remainingWidth * 0.6f));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }
    }
}
