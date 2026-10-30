#if !VIRTUADEMY_APPLICATION
using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The Environment Details window (opened by the <c>i</c> button of a scene's row in Publish Environments): for one scene of the scene list, where its
    /// environment is published (each world and tenant level, on each connection of the computer), what thumbnail and localization file each place holds
    /// and whether the project holds the same files, the experiences that use it in each world, and the actions on them: save the online files into the
    /// project, replace them in one place or in all, create the Core experience, set an experience's state. Also the scene's default files, which a deploy
    /// sends the first time it makes the scene's environment in a place.
    /// </summary>
    /// <remarks>
    /// <b>Online is the source.</b> The window reads every connection each time it opens, on Refresh and when the scene changes; what it shows while you
    /// browse is cached under <c>Library/Virtuademy/EnvironmentDetails/</c>. It is a client of <see cref="EnvironmentFilesService"/> and
    /// <see cref="EnvironmentExperiencesService"/>, which the command line uses too: everything here also exists there. Every request is asynchronous and
    /// goes out with a cancellation that a scene change, Refresh and closing the window raise; a result that arrives late is dropped.
    /// <para>
    /// <b>The window draws only from <see cref="DetailsConnection"/> objects</b> (one per connection: its list, its experiences, its load state).
    /// <see cref="SetConnectionState"/> and <see cref="ShowState"/> set them and redraw, which is how the loaders do it and how a harness shows a state
    /// without a server (with <see cref="DisableLoading"/> set before the window opens).
    /// </para>
    /// <para>The window is not compiled in the application (<c>VIRTUADEMY_APPLICATION</c>), like Publish Environments.</para>
    /// </remarks>
    internal sealed partial class EnvironmentDetailsWindow : EditorWindow
    {
        private const string window_title = "Environment Details";
        private const string styles_folder = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Editor/Scripts/";

        private static readonly Vector2 min_window_size = new(760, 480);

        /// <summary>When true, opening the window and changing the scene read nothing from the platform: a harness sets the states itself.</summary>
        internal static bool DisableLoading;

        // Where the window keeps its size and the last scene it showed: per user (EditorPrefs), and per project for the scene.
        private static string ProjectPrefix => "Virtuademy.EnvironmentDetails." + Hash128.Compute(Application.dataPath).ToString().Substring(0, 8) + ".";
        private static string PrefScene => ProjectPrefix + "Scene";
        private const string pref_width = "Virtuademy.EnvironmentDetails.Width";
        private const string pref_height = "Virtuademy.EnvironmentDetails.Height";

        private VisualElement root;
        private DropdownField scenePopup;
        private Button refreshButton;
        private Label sceneFolder;
        private ObjectField thumbnailField;
        private ObjectField localizationField;
        private Label defaultsRefusal;
        private VisualElement body;
        private VisualElement split;
        private ScrollView leftScroll;
        private ScrollView rightScroll;
        private ScrollView fullScroll;

        private SceneListScriptableObject list;
        private List<SceneRef> scenes = new();
        private SceneRef scene;
        private string pendingSceneKey;

        private readonly List<DetailsConnection> connections = new();
        private bool allSelected;
        private PlaceKey? selected;

        private string connectionsSignature = string.Empty;
        private bool renderPending;
        private IVisualElementScheduledItem saveSizeItem;

        #region Opening

        // Under Publish Environments in the Virtuademy menu: opens on the scene the window showed last (the first of the list the first time).
        [MenuItem("Virtuademy/Environment Details", false, 2)]
        private static void OpenFromMenu() => Open(null);

        /// <summary>Opens the single window on a scene (its environment name, <see cref="PublishedEnvironmentsIndex.Key"/>), or brings the open one to it.</summary>
        internal static EnvironmentDetailsWindow Open(string sceneKey)
        {
            bool existed = HasOpenInstances<EnvironmentDetailsWindow>();
            EnvironmentDetailsWindow window = GetWindow<EnvironmentDetailsWindow>();
            window.titleContent = new GUIContent(window_title);
            window.minSize = min_window_size;

            if (!existed && EditorPrefs.HasKey(pref_width) && EditorPrefs.HasKey(pref_height))
            {
                Rect at = window.position;
                window.position = new Rect(at.x, at.y,
                    Mathf.Max(min_window_size.x, EditorPrefs.GetFloat(pref_width)), Mathf.Max(min_window_size.y, EditorPrefs.GetFloat(pref_height)));
            }

            window.Focus();
            window.ShowScene(sceneKey);
            return window;
        }

        private void ShowScene(string sceneKey)
        {
            if (root == null)
            {
                // The window was just made: its UI is built next, and starts on this scene.
                pendingSceneKey = sceneKey;
                return;
            }

            SceneRef next = scenes.FirstOrDefault(s => string.Equals(s.Key, sceneKey, StringComparison.OrdinalIgnoreCase));
            if (next == null)
            {
                // A scene the list did not have when the window last read it.
                ReloadSceneList();
                next = scenes.FirstOrDefault(s => string.Equals(s.Key, sceneKey, StringComparison.OrdinalIgnoreCase));
            }

            if (next == null)
                return;

            if (scene == null || !string.Equals(next.Key, scene.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (!Busy)
                    SetScene(next);
                return;
            }

            // The scene already shown: the window only comes to the front. It is read again only when nothing is open (a strip, a preview) or running.
            if (!Busy && placeFlow == null && allFlow.Rows == null && connections.All(c => c.Load != ConnectionLoad.Loading))
                Refresh();
        }

        /// <summary>A replace, a save or a creation is running: what the window shows is that run's, so the scene and the data are not changed under it.</summary>
        private bool Busy => allFlow.Applying || allFlow.Previewing || savingAll || creatingAll || (placeFlow != null && placeFlow.Busy);

        private void OnEnable()
        {
            minSize = min_window_size;
            EditorLoginState.OnLoginStateChanged += OnLoginStateChanged;
            SceneDefaults.Changed += OnDefaultsChanged;
            Undo.undoRedoPerformed += OnDefaultsChanged;
        }

        private void OnDisable() => Release();

        private void OnDestroy() => Release();

        private void Release()
        {
            EditorLoginState.OnLoginStateChanged -= OnLoginStateChanged;
            SceneDefaults.Changed -= OnDefaultsChanged;
            Undo.undoRedoPerformed -= OnDefaultsChanged;
            CancelAll();
            CancelLogins();
            ClearPreviews();
        }

        private void OnFocus()
        {
            if (root == null)
                return;

            // Scenes created or renamed while the window was behind, and defaults changed in Publish Environments.
            ReloadSceneList();
            RefreshDefaultsStrip();
        }

        #endregion

        #region Building

        public void CreateGUI()
        {
            titleContent = new GUIContent(window_title);
            minSize = min_window_size;

            root = rootVisualElement;
            root.AddToClassList("vd-details");
            foreach (string sheet in new[] { "AddressablesConfigurationWindow.uss", "EnvironmentDetailsWindow.uss" })
            {
                StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(styles_folder + sheet);
                if (styles != null)
                    root.styleSheets.Add(styles);
            }

            ApplySkin();
            root.Add(VirtuademyWindowHeader.Create(window_title));
            root.Add(BuildSceneBar());
            root.Add(BuildDefaultsStrip());
            root.Add(BuildBody());

            root.RegisterCallback<GeometryChangedEvent>(_ => SaveSizeSoon());

            list = SceneListScriptableObject.Find();
            scenes = EnvironmentFilesService.ScenesOf(list);
            string wanted = pendingSceneKey ?? EditorPrefs.GetString(PrefScene, string.Empty);
            pendingSceneKey = null;
            scene = scenes.FirstOrDefault(s => string.Equals(s.Key, wanted, StringComparison.OrdinalIgnoreCase)) ?? scenes.FirstOrDefault();
            RefreshSceneBar();
            RefreshDefaultsStrip();
            LoadAll();
        }

        /// <summary>The window's colours come in two sets, one for each editor skin.</summary>
        private void ApplySkin()
        {
            root.EnableInClassList("vd-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("vd-light", !EditorGUIUtility.isProSkin);
        }

        private void SaveSizeSoon()
        {
            saveSizeItem?.Pause();
            saveSizeItem = root.schedule.Execute(() =>
            {
                Rect at = position;
                if (at.width < min_window_size.x || at.height < min_window_size.y)
                    return;

                EditorPrefs.SetFloat(pref_width, at.width);
                EditorPrefs.SetFloat(pref_height, at.height);
            }).StartingIn(500);
        }

        private VisualElement BuildSceneBar()
        {
            VisualElement bar = new();
            bar.AddToClassList("vd-scene-bar");

            bar.Add(Text("Scene", "muted"));
            scenePopup = new DropdownField(new List<string>(), 0);
            scenePopup.style.flexGrow = 0;
            scenePopup.style.width = 240;
            scenePopup.RegisterValueChangedCallback(evt =>
            {
                SceneRef next = scenes.FirstOrDefault(s => s.Name == evt.newValue);
                if (next != null && next != scene)
                    SetScene(next);
            });
            bar.Add(scenePopup);

            sceneFolder = Text(string.Empty, "muted", "vd-grow");
            bar.Add(sceneFolder);

            refreshButton = Btn("Refresh", Refresh, "Reads every connection again.");
            bar.Add(refreshButton);
            return bar;
        }

        private VisualElement BuildDefaultsStrip()
        {
            VisualElement strip = new();
            strip.AddToClassList("vd-defaults");

            strip.Add(Text("Defaults for new places", "vd-defaults-title"));
            strip.Add(Text("Thumbnail", "muted"));
            thumbnailField = NewDefaultField(typeof(Texture2D), FileKind.Thumbnail, "None (Image)",
                "The image a deploy sends as the thumbnail when it makes this scene's environment in a place. A PNG or JPEG of the project.");
            strip.Add(thumbnailField);
            strip.Add(Text("Localization file", "muted"));
            localizationField = NewDefaultField(typeof(TextAsset), FileKind.Localization, "None (Localization file)",
                "The .csv a deploy sends as the localization file when it makes this scene's environment in a place.");
            strip.Add(localizationField);
            strip.Add(Text("Used only the first time this scene is published in a place.", "muted", "vd-defaults-note"));

            defaultsRefusal = Text(string.Empty, "vd-defaults-refusal");
            defaultsRefusal.style.display = DisplayStyle.None;
            strip.Add(defaultsRefusal);
            return strip;
        }

        private ObjectField NewDefaultField(Type type, FileKind kind, string emptyText, string tooltip)
        {
            ObjectField field = new() { objectType = type, allowSceneObjects = false, tooltip = tooltip };

            void FixEmptyText()
            {
                Label display = field.Q<Label>(className: "unity-object-field-display__label");
                if (display != null && field.value == null)
                    display.text = emptyText;
            }

            field.RegisterValueChangedCallback(evt =>
            {
                UnityEngine.Object accepted = SceneDefaults.Accept(kind, new[] { evt.newValue }, out string refusal);
                if (evt.newValue != null && accepted == null)
                {
                    // Not a PNG or JPEG, not a .csv, or not in Assets/: the field goes back to what the scene has.
                    field.SetValueWithoutNotify(evt.previousValue);
                    ShowDefaultsRefusal(refusal);
                    field.schedule.Execute(FixEmptyText);
                    return;
                }

                defaultsRefusal.style.display = DisplayStyle.None;
                if (scene != null && SceneDefaults.Set(list, scene.Key, kind, accepted))
                    field.schedule.Execute(FixEmptyText);
            });
            field.RegisterCallback<GeometryChangedEvent>(_ => FixEmptyText());
            field.RegisterCallback<AttachToPanelEvent>(_ => field.schedule.Execute(FixEmptyText));
            return field;
        }

        private void ShowDefaultsRefusal(string message)
        {
            defaultsRefusal.text = message;
            defaultsRefusal.style.display = DisplayStyle.Flex;
            defaultsRefusal.schedule.Execute(() => defaultsRefusal.style.display = DisplayStyle.None).StartingIn(4000);
        }

        private VisualElement BuildBody()
        {
            body = new VisualElement();
            body.AddToClassList("vd-body");

            split = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
            leftScroll = new ScrollView(ScrollViewMode.Vertical);
            leftScroll.AddToClassList("vd-left");
            rightScroll = new ScrollView(ScrollViewMode.Vertical);
            rightScroll.AddToClassList("vd-right");
            split.Add(leftScroll);
            split.Add(rightScroll);
            body.Add(split);

            fullScroll = new ScrollView(ScrollViewMode.Vertical);
            fullScroll.AddToClassList("vd-full");
            body.Add(fullScroll);

            // Each skeleton bar of the window dims and brightens together.
            body.schedule.Execute(() => body.Query<VisualElement>(className: "vd-skel").ForEach(bar => bar.ToggleInClassList("vd-skel--dim"))).Every(700);
            return body;
        }

        #endregion

        #region Scene bar and defaults

        private void ReloadSceneList()
        {
            // A harness has set the scene and the states itself: the project's list must not take them back.
            if (DisableLoading || Busy)
                return;

            list = SceneListScriptableObject.Find();
            List<SceneRef> next = EnvironmentFilesService.ScenesOf(list);
            bool same = next.Count == scenes.Count && next.Select(s => s.Key + "|" + s.Path).SequenceEqual(scenes.Select(s => s.Key + "|" + s.Path));
            if (same)
                return;

            scenes = next;
            SceneRef current = scene == null ? null : scenes.FirstOrDefault(s => string.Equals(s.Key, scene.Key, StringComparison.OrdinalIgnoreCase));
            if (current == null && scene != null)
            {
                scene = scenes.FirstOrDefault();
                LoadAll();
            }
            else if (current != null)
            {
                scene = current;
            }

            RefreshSceneBar();
            RefreshDefaultsStrip();
            ScheduleRender();
        }

        private void RefreshSceneBar()
        {
            scenePopup.choices = scenes.Select(s => s.Name).ToList();
            if (scene != null)
                scenePopup.SetValueWithoutNotify(scene.Name);
            else
                scenePopup.SetValueWithoutNotify(string.Empty);

            sceneFolder.text = scene == null ? string.Empty : System.IO.Path.GetDirectoryName(scene.Path)?.Replace('\\', '/') ?? string.Empty;
            sceneFolder.tooltip = scene?.Path ?? string.Empty;
        }

        /// <summary>The two fields show the scene's defaults, read from the scene list: local, no request.</summary>
        private void RefreshDefaultsStrip()
        {
            SceneListScriptableObject.SceneConfiguration config = scene == null ? null : SceneDefaults.ConfigOf(list, scene.Key);
            thumbnailField.SetEnabled(config != null);
            localizationField.SetEnabled(config != null);
            thumbnailField.SetValueWithoutNotify(config?.DefaultThumbnail);
            localizationField.SetValueWithoutNotify(config?.DefaultLocalization);
            FixEmptyDisplay(thumbnailField, "None (Image)");
            FixEmptyDisplay(localizationField, "None (Localization file)");
            thumbnailField.schedule.Execute(() => FixEmptyDisplay(thumbnailField, "None (Image)"));
            localizationField.schedule.Execute(() => FixEmptyDisplay(localizationField, "None (Localization file)"));
        }

        private static void FixEmptyDisplay(ObjectField field, string text)
        {
            Label display = field.Q<Label>(className: "unity-object-field-display__label");
            if (display != null && field.value == null)
                display.text = text;
        }

        private void OnDefaultsChanged()
        {
            if (root == null)
                return;

            RefreshDefaultsStrip();
            ScheduleRender();
        }

        #endregion

        #region Choosing the scene

        private void SetScene(SceneRef next)
        {
            if (Busy)
            {
                RefreshSceneBar();
                return;
            }

            scene = next;
            EditorPrefs.SetString(PrefScene, next.Key);
            RefreshSceneBar();
            RefreshDefaultsStrip();
            LoadAll();
        }

        /// <summary>Reads every connection again, for the scene. Anything in flight is cancelled, and a replace waiting for its Apply is dropped.</summary>
        private void Refresh()
        {
            if (!Busy)
                LoadAll();
        }

        #endregion

        #region Rendering

        /// <summary>Draws again, once: loaders that finish one after another ask for it, and a burst of results is one redraw.</summary>
        private void ScheduleRender()
        {
            if (renderPending || root == null)
                return;

            renderPending = true;
            root.schedule.Execute(() =>
            {
                renderPending = false;
                Render();
            }).StartingIn(40);
        }

        private void Render()
        {
            if (root == null)
                return;

            ApplySkin();
            EnsureSelection();

            // While a run is going, the scene and the data stay as they are.
            bool busy = Busy;
            refreshButton?.SetEnabled(!busy);
            scenePopup?.SetEnabled(!busy);
            RenderBody();
        }

        private void RenderBody()
        {
            if (scene == null)
            {
                split.style.display = DisplayStyle.None;
                fullScroll.style.display = DisplayStyle.Flex;
                fullScroll.contentContainer.Clear();
                fullScroll.contentContainer.Add(Banner(BannerKind.Info, list == null
                    ? "The project has no scene list yet. Open Virtuademy > Publish Environments once to create it."
                    : "The scene list has no scene."));
                return;
            }

            bool allRead = connections.Count > 0 && connections.All(c => c.Load == ConnectionLoad.Loaded);
            int places = PlaceCount();

            if (connections.Count == 0 && !DisableLoading)
            {
                ShowFull(content => content.Add(Banner(BannerKind.Info,
                    "No connection on this computer yet. Import the configuration file of a tenant in Virtuademy > Publish Environments: each connection then appears here.")));
                return;
            }

            if (allRead && places == 0)
            {
                ShowFull(BuildNotPublished);
                return;
            }

            if (allRead && places == 1)
            {
                ShowFull(BuildOnePlaceOnly);
                return;
            }

            split.style.display = DisplayStyle.Flex;
            fullScroll.style.display = DisplayStyle.None;

            float leftOffset = leftScroll.scrollOffset.y;
            float rightOffset = rightScroll.scrollOffset.y;
            leftScroll.contentContainer.Clear();
            rightScroll.contentContainer.Clear();
            BuildList(leftScroll.contentContainer);
            BuildPane(rightScroll.contentContainer);
            leftScroll.schedule.Execute(() => leftScroll.scrollOffset = new Vector2(0, leftOffset));
            rightScroll.schedule.Execute(() => rightScroll.scrollOffset = new Vector2(0, rightOffset));
        }

        private void ShowFull(Action<VisualElement> fill)
        {
            split.style.display = DisplayStyle.None;
            fullScroll.style.display = DisplayStyle.Flex;
            float offset = fullScroll.scrollOffset.y;
            fullScroll.contentContainer.Clear();
            fill(fullScroll.contentContainer);
            fullScroll.schedule.Execute(() => fullScroll.scrollOffset = new Vector2(0, offset));
        }

        #endregion

        #region Seam

        /// <summary>
        /// Sets one connection's state (adds it, or replaces the one with the same key) and draws again. What the loaders call when a connection
        /// has been read, and what a harness calls to show a state.
        /// </summary>
        internal void SetConnectionState(DetailsConnection state)
        {
            int at = connections.FindIndex(c => string.Equals(c.Key, state.Key, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
                connections[at] = state;
            else
                connections.Add(state);

            Render();
        }

        /// <summary>
        /// Shows a scene and connections as given, with nothing read from the platform (any load in flight is cancelled and later results are
        /// dropped): for a harness. <paramref name="select"/> is the place to select, or <paramref name="all"/> for All places; neither selects the first place.
        /// </summary>
        internal void ShowState(SceneRef sceneRef, IEnumerable<DetailsConnection> states, PlaceKey? select = null, bool all = false)
        {
            CancelAll();
            ClearPreviews();
            ResetFlows();
            scene = sceneRef;
            if (scene != null && scenes.All(s => !string.Equals(s.Key, scene.Key, StringComparison.OrdinalIgnoreCase)))
                scenes.Add(scene);
            connections.Clear();
            connections.AddRange(states);
            selected = select;
            allSelected = all;
            if (root != null)
            {
                RefreshSceneBar();
                RefreshDefaultsStrip();
                Render();
            }
        }

        /// <summary>Selects a place of the list (a connection and a world, null for its tenant level) and draws it.</summary>
        internal void Select(string connectionKey, int? worldId)
        {
            // A replace in one place is running: what is open is its, and it is not dropped by looking at another place.
            if (placeFlow != null && placeFlow.Busy)
                return;

            allSelected = false;
            selected = new PlaceKey(connectionKey, worldId);
            placeFlow = null;
            Render();
        }

        /// <summary>Selects All places and draws it.</summary>
        internal void SelectAllPlaces()
        {
            if (placeFlow != null && placeFlow.Busy)
                return;

            allSelected = true;
            selected = null;
            placeFlow = null;
            Render();
        }

        /// <summary>The connections as the window holds them: a harness reads and changes them, then calls <see cref="Render"/> through <see cref="SetConnectionState"/>.</summary>
        internal IReadOnlyList<DetailsConnection> Connections => connections;

        /// <summary>The replace in one place that is waiting for its Apply, or null.</summary>
        internal PlaceReplaceFlow PlaceFlow { get => placeFlow; set { placeFlow = value; Render(); } }

        /// <summary>Replace in all places: the assets, the preview table and its results.</summary>
        internal ReplaceAllFlow AllFlow { get => allFlow; set { allFlow = value ?? new ReplaceAllFlow(); Render(); } }

        #endregion
    }
}
#endif
