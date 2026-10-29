using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

using Virtuademy.SDK.TenantConfiguration.Editor;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    /// <summary>
    /// The Placeholder IDs window (menu Virtuademy > Advanced > Placeholder IDs; the class kept its old name, Scene objects
    /// management): every <see cref="SceneObjectId"/> of the active scene, which every placeholder carries, with its ID. The ID tells
    /// a placeholder apart from the others of the scene, in the app and across the network, so one that is 0 or used twice is
    /// marked, and the window gives new IDs to those (Fix IDs), or to all of them.
    /// </summary>
    public class SceneObjectsManagementWindow : EditorWindow
    {
        private const string window_title = "Placeholder IDs";
        private const string styles_path = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Editor/Scripts/AddressablesConfigurationWindow.uss";

        private static readonly Color problem_color = new(0.95f, 0.45f, 0.45f);

        private readonly List<SceneObjectId> sceneObjects = new();

        private Label sceneLabel;
        private Label summaryLabel;
        private Label unsavedLabel;
        private VisualElement list;
        private Button fixButton;
        private Button resetAllButton;
        private string signature;

        [MenuItem("Virtuademy/Advanced/Placeholder IDs", false, 42)]
        public static void ShowWindow()
        {
            SceneObjectsManagementWindow window = GetWindow<SceneObjectsManagementWindow>();
            window.titleContent = new GUIContent(window_title);
            window.minSize = new Vector2(380, 260);
        }

        public void CreateGUI()
        {
            // A window restored from a layout saved before the rename keeps the title it was saved with.
            titleContent = new GUIContent(window_title);

            VisualElement root = rootVisualElement;
            StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(styles_path);
            if (styles != null)
                root.styleSheets.Add(styles);

            ScrollView scroll = new();
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            scroll.Add(VirtuademyWindowHeader.Create(window_title));

            Label intro = new("Every placeholder has an ID that tells it apart from the others of the scene, in the app and across the " +
                              "network. An ID that is 0 or used twice is marked: Fix IDs gives those a new one.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginLeft = 8, marginRight = 8, marginBottom = 4 },
            };
            intro.AddToClassList("muted");
            scroll.Add(intro);

            VisualElement section = new();
            section.AddToClassList("section");

            VisualElement header = new();
            header.AddToClassList("section-header");
            sceneLabel = new Label();
            sceneLabel.AddToClassList("section-title");
            header.Add(sceneLabel);
            summaryLabel = new Label();
            summaryLabel.AddToClassList("section-note");
            header.Add(summaryLabel);
            section.Add(header);

            // SceneObjectId gives an ID only to a placeholder of a scene that has a name: in an unsaved scene it takes it for a prefab.
            unsavedLabel = new Label("Save the scene first: a placeholder gets its ID only in a saved scene.");
            unsavedLabel.AddToClassList("section-notice");
            section.Add(unsavedLabel);

            list = new VisualElement();
            list.AddToClassList("section-body");
            section.Add(list);

            VisualElement bar = new();
            bar.AddToClassList("section-bar");
            bar.Add(new VisualElement { style = { flexGrow = 1 } });
            resetAllButton = new Button(ResetAllIds)
            {
                text = "New IDs for all...",
                tooltip = "Gives every placeholder of the scene a new ID. Asks first.",
            };
            bar.Add(resetAllButton);
            fixButton = new Button(FixIds)
            {
                text = "Fix IDs",
                tooltip = "Gives a new ID to each placeholder whose ID is 0 or used by another one. The first of two that share an ID keeps it.",
            };
            bar.Add(fixButton);
            section.Add(bar);
            scroll.Add(section);

            Refresh();

            // A placeholder added, removed or renamed, another scene opened, an undo: the list is read again when it changed.
            EditorApplication.hierarchyChanged += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            Undo.undoRedoPerformed += Refresh;
            root.schedule.Execute(Refresh).Every(1000);
        }

        private void OnDestroy()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void OnActiveSceneChanged(Scene previous, Scene current) => Refresh();

        private void FindPlaceholders()
        {
            sceneObjects.Clear();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject obj in scene.GetRootGameObjects())
                sceneObjects.AddRange(obj.GetComponentsInChildren<SceneObjectId>(true));
        }

        /// <summary>
        /// The list, rebuilt only when what it shows changed: each placeholder with its name (a click selects it), its ID and, for an
        /// ID that is 0 or already used above it, why it has to change.
        /// </summary>
        private void Refresh()
        {
            if (list == null)
                return;

            FindPlaceholders();
            Scene scene = SceneManager.GetActiveScene();

            // Read through UniqueID, as the app does: a placeholder in a scene with an ID of 0 gets one on the first read.
            List<(SceneObjectId Object, int Id)> rows = sceneObjects.Where(item => item != null).Select(item => (item, item.UniqueID)).ToList();
            string current = scene.path + "|" + string.Join("|", rows.Select(row => $"{row.Object.GetInstanceID()}:{row.Id}:{row.Object.gameObject.name}"));
            if (current == signature)
                return;

            signature = current;
            list.Clear();

            bool saved = !string.IsNullOrEmpty(scene.name);
            sceneLabel.text = saved ? scene.name : "Untitled scene";
            unsavedLabel.style.display = saved || rows.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            Dictionary<int, string> used = new();
            int problems = 0;
            foreach ((SceneObjectId item, int id) in rows)
            {
                string problem = null;
                if (id == 0)
                    problem = "No ID yet.";
                else if (used.TryGetValue(id, out string first))
                    problem = $"Same ID as {first}.";
                else
                    used.Add(id, item.gameObject.name);

                if (problem != null)
                    problems++;

                list.Add(Row(item, id, problem));
            }

            if (rows.Count == 0)
            {
                Label empty = new("No placeholder in this scene.");
                empty.AddToClassList("muted");
                list.Add(empty);
            }

            summaryLabel.text = rows.Count == 0 ? string.Empty
                : $"{rows.Count} placeholder{(rows.Count == 1 ? string.Empty : "s")}" + (problems > 0 ? $", {problems} to fix" : ", all IDs unique");
            summaryLabel.style.color = problems > 0 ? new StyleColor(problem_color) : new StyleColor(StyleKeyword.Null);
            fixButton.SetEnabled(saved && problems > 0);
            resetAllButton.SetEnabled(saved && rows.Count > 0);
        }

        private static VisualElement Row(SceneObjectId item, int id, string problem)
        {
            VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 18 } };

            Label name = new(item.gameObject.name)
            {
                tooltip = "Click to select it in the Hierarchy.",
                style = { flexGrow = 1, flexShrink = 1, unityFontStyleAndWeight = FontStyle.Bold, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis },
            };
            name.AddToClassList("scene-name");
            name.RegisterCallback<ClickEvent>(_ =>
            {
                Selection.activeGameObject = item.gameObject;
                EditorGUIUtility.PingObject(item.gameObject);
            });
            row.Add(name);

            if (problem != null)
            {
                Label why = new(problem) { style = { color = problem_color, marginRight = 6, flexShrink = 1 } };
                row.Add(why);
            }

            Label idLabel = new(id.ToString())
            {
                style = { minWidth = 80, unityTextAlign = TextAnchor.MiddleRight, color = problem != null ? new StyleColor(problem_color) : new StyleColor(StyleKeyword.Null) },
            };
            row.Add(idLabel);
            return row;
        }

        /// <summary>Gives a new ID to each placeholder whose ID is 0 or already used: the first that holds an ID keeps it.</summary>
        private void FixIds()
        {
            FindPlaceholders();
            int undoGroup = StartUndo("Fix placeholder IDs");

            // The IDs in use that are not 0. Each is kept by the first placeholder that holds it; a second one gets a new ID.
            List<int> unclaimed = sceneObjects.Select(item => item.UniqueID).Distinct().Where(id => id > 0).ToList();
            foreach (SceneObjectId item in sceneObjects)
            {
                int id = item.UniqueID;
                if (id == 0 || !unclaimed.Contains(id))
                {
                    item.UniqueID = 0;
                    item.GenerateObjectUniqueID();
                    EditorUtility.SetDirty(item);
                }
                else
                {
                    unclaimed.Remove(id);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneDirty();
            Refresh();
        }

        /// <summary>Gives every placeholder of the scene a new ID, after a confirmation.</summary>
        private void ResetAllIds()
        {
            FindPlaceholders();
            if (!EditorUtility.DisplayDialog("New IDs for all placeholders",
                    $"Give all {sceneObjects.Count} placeholders of this scene a new ID? Use it only when their IDs came from another " +
                    "scene: Fix IDs already repairs an ID that is missing or used twice. Edit > Undo puts the previous IDs back.",
                    "New IDs for all", "Cancel"))
                return;

            int undoGroup = StartUndo("New placeholder IDs");
            foreach (SceneObjectId item in sceneObjects)
                item.UniqueID = 0;
            foreach (SceneObjectId item in sceneObjects)
            {
                item.GenerateObjectUniqueID();
                EditorUtility.SetDirty(item);
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneDirty();
            Refresh();
        }

        /// <summary>
        /// One undo step for the whole change: <see cref="SceneObjectId.GenerateObjectUniqueID"/> records its own through a
        /// SerializedObject. An ID put back by an undo that another placeholder holds too is given a new one again by
        /// <c>SceneObjectId.OnValidate</c>, so undoing Fix IDs does not bring a shared ID back.
        /// </summary>
        private int StartUndo(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            int group = Undo.GetCurrentGroup();
            Undo.RecordObjects(sceneObjects.Cast<Object>().ToArray(), name);
            return group;
        }

        private static void MarkSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
