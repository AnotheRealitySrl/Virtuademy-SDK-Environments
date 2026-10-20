using Newtonsoft.Json;
using Virtuademy.SDK.TenantConfiguration.Editor;
using Virtuademy.SDK.TenantConfiguration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Callbacks;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;


namespace Virtuademy.SDK.Environments.Editor
{
    public class AddressablesManagementWindow : EditorWindow
    {
        private enum EBuildError
        {
            None,
            FolderMissing,
            BinaryCatalog,
            MissingModule,
            FirstpassScripts,
        }

        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        private VisualElement root;

        private SceneListScriptableObject sceneConfigurations;

        private AddressableAssetSettings settings;

        private const string addressables_output_folder = AddressablesConfiguration.OutputFolder;

        [SerializeField] private EBuildError buildResult = EBuildError.None;

        [CreateProperty] private string ActiveProfileName => settings.profileSettings.GetProfileName(settings.activeProfileId);

        [CreateProperty] private string CurrentRemoteBuildPathVariableValue => AddressablesConfiguration.GetProfileValue(settings, AddressablesConfiguration.RemoteBuildPathVariableName);
        [CreateProperty] private bool IsRemoteBuildPathConfigured => CurrentRemoteBuildPathVariableValue == AddressablesConfiguration.RemoteBuildPath;

        [CreateProperty] private string CurrentRemoteLoadPathVariableValue => AddressablesConfiguration.GetProfileValue(settings, AddressablesConfiguration.RemoteLoadPathVariableName);
        [CreateProperty] private bool IsRemoteLoadPathConfigured => CurrentRemoteLoadPathVariableValue == AddressablesConfiguration.RemoteLoadPath;

        [CreateProperty] private string CurrentBuildTargetVariableValue => AddressablesConfiguration.GetProfileValue(settings, AddressablesConfiguration.BuildTargetVariableName);
        [CreateProperty] private bool IsBuildTargetConfigured => CurrentBuildTargetVariableValue == AddressablesConfiguration.BuildTargetVariableValue;

        [CreateProperty] private string CurrentPlayerVersionOverrideVariableValue => AddressablesConfiguration.GetProfileValue(settings, AddressablesConfiguration.PlayerVersionOverrideVariableName);
        [CreateProperty] private bool IsPlayerVersionOverrideConfigured => CurrentPlayerVersionOverrideVariableValue == AddressablesConfiguration.PlayerVersionOverrideVariableValue;

        [CreateProperty] private bool AreAddressablesConfigured => AddressablesConfiguration.IsConfiguredFor(settings);


        [Obsolete("Reading the bearer token directly skips expiry checks and renewal. Route API calls " +
                  "through EditorSessionManager.SendAuthorizedAsync instead.")]
        public static string token
        {
            get
            {
                if (EditorLoginState.IsLoggedIn)
                    return EditorLoginState.BearerToken;
                return _legacyToken;
            }
            set => _legacyToken = value;
        }
        private static string _legacyToken = "";

        // Worlds state
        private List<PublishableWorld> availableWorlds = new();
        private Dictionary<int, bool> selectedWorlds = new();

        // UI references
        private Label loginStatusLabel;
        private IVisualElementScheduledItem sessionExpiryRefresh;
        private VisualElement deploySection;
        private ScrollView worldsList;
        private Label worldsLoadingLabel;
        private Button deployButton;
        private Button buildAndDeployButton;
        private Button openTenantSelectionButton;
        private Button logoutButton;
        private Button buildAddressablesButton;

        // Tenant deploy UI references
        private VisualElement tenantDeploySection;
        private Button tenantDeployButton;
        private Button tenantBuildAndDeployButton;

        // Platform module warnings
        private VisualElement platformWarningsContainer;
        private SerializedObject sceneSerializedObject;
        private VisualElement sceneListContainer;
        private TextField sceneSearchField;
        private Toggle onlyInBuildToggle;
        private DropdownField publicationFilter;
        private Label sceneCountLabel;
        private readonly Dictionary<int, VisualElement> worldDeployPreviews = new();
        private VisualElement tenantDeployPreview;

        // Deploy error log
        private VisualElement deployErrorsContainer;
        private ScrollView deployErrorsScrollView;

        [MenuItem("Virtuademy/Addressables management")]
        public static void ShowExample()
        {
            AddressablesManagementWindow wnd = GetWindow<AddressablesManagementWindow>();
            wnd.titleContent = new GUIContent("Addressables management");
        }

        private void OnApplicationQuit()
        {
            SaveAsset(sceneConfigurations);
        }

        private void OnDestroy()
        {
            SaveAsset(sceneConfigurations);
            EditorLoginState.OnLoginStateChanged -= OnLoginStateChanged;
            EditorLoginState.OnTokenChanged -= OnTokenChanged;
            PublishedEnvironmentsIndex.Changed -= OnPublishedIndexChanged;
        }

        private void OnFocus()
        {
            // Refresh warnings whenever the window regains focus (e.g. after the user
            // installs a module via Unity Hub and returns to the editor).
            RefreshPlatformWarnings();

            // Scenes created, deleted or moved while the window was in the background.
            SyncSceneRegistry();

            // The session label ages on its own (the token expires while the window sits idle),
            // so re-render it here. Without reloading the worlds: that is a network round trip
            // per focus change, and nothing about the world list changed.
            if (loginStatusLabel != null)
                RefreshLoginState(reloadWorlds: false);
        }


        public void CreateGUI()
        {
            root = rootVisualElement;

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            LoadSettings();
            AddDataBindings();
            SetupLoginAwareUI();

            EditorLoginState.OnLoginStateChanged += OnLoginStateChanged;
            EditorLoginState.OnTokenChanged += OnTokenChanged;
        }

        #region Login-aware UI

        private void SetupLoginAwareUI()
        {
            loginStatusLabel = root.Q<Label>("login-status-label");
            deploySection = root.Q<VisualElement>("deploy-section");
            worldsList = root.Q<ScrollView>("worlds-list");
            worldsLoadingLabel = root.Q<Label>("worlds-loading-label");
            deployButton = root.Q<Button>("deploy-button");
            buildAndDeployButton = root.Q<Button>("build-and-deploy-button");

            openTenantSelectionButton = root.Q<Button>("open-tenant-selection-button");
            openTenantSelectionButton.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Virtuademy/Available Tenants");
            };

            // Through the session manager, so the Azure refresh token goes away too: clearing
            // only EditorLoginState would leave a cache that logs the user straight back in.
            logoutButton = new Button(async () => await EditorSessionManager.LogoutAsync())
            {
                text = "Logout"
            };
            logoutButton.style.display = DisplayStyle.None;
            logoutButton.style.marginLeft = 8;     // as the Login button, which it replaces
            logoutButton.style.flexShrink = 0;     // the status label wraps; the button keeps its width
            loginStatusLabel.parent.Add(logoutButton);

            deployButton.clicked += OnDeployClicked;
            buildAndDeployButton.clicked += OnBuildAndDeployClicked;

            // Tenant deploy
            tenantDeploySection = root.Q<VisualElement>("tenant-deploy-section");
            tenantDeployButton = root.Q<Button>("tenant-deploy-button");
            tenantBuildAndDeployButton = root.Q<Button>("tenant-build-and-deploy-button");

            tenantDeployButton.clicked += OnTenantDeployClicked;
            tenantBuildAndDeployButton.clicked += OnTenantBuildAndDeployClicked;

            tenantDeployPreview = new VisualElement { style = { marginBottom = 4, marginLeft = 8, marginRight = 8 } };
            tenantDeploySection.Insert(tenantDeploySection.IndexOf(root.Q<VisualElement>("tenant-deploy-buttons")), tenantDeployPreview);

            InitDeployErrorsContainer();

            RefreshLoginState();
        }

        private void InitDeployErrorsContainer()
        {
            deployErrorsContainer = new VisualElement();
            deployErrorsContainer.style.marginTop = 8;
            deployErrorsContainer.style.marginLeft = 4;
            deployErrorsContainer.style.marginRight = 4;
            deployErrorsContainer.style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            var title = new Label("Deploy errors");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.35f, 0.35f);
            header.Add(title);

            var clearButton = new Button(ClearDeployErrors) { text = "Clear" };
            clearButton.style.paddingLeft = 8;
            clearButton.style.paddingRight = 8;
            header.Add(clearButton);

            deployErrorsContainer.Add(header);

            deployErrorsScrollView = new ScrollView(ScrollViewMode.Vertical);
            deployErrorsScrollView.style.maxHeight = 220;
            deployErrorsContainer.Add(deployErrorsScrollView);

            root.Add(deployErrorsContainer);
        }

        private void OnLoginStateChanged()
        {
            RefreshLoginState();
        }

        private void RefreshLoginState(bool reloadWorlds = true)
        {
            bool loggedIn = EditorLoginState.IsLoggedIn;

            if (openTenantSelectionButton != null)
                openTenantSelectionButton.style.display = loggedIn ? DisplayStyle.None : DisplayStyle.Flex;
            if (logoutButton != null)
                logoutButton.style.display = loggedIn ? DisplayStyle.Flex : DisplayStyle.None;

            if (loggedIn)
            {
                string tenantLabel = EditorLoginState.CurrentTenant?.Label ?? "Unknown";
                string envLabel = EditorLoginState.LoggedInEnv;
                string username = EditorLoginState.Username;
                string userPart = !string.IsNullOrEmpty(username) ? $" - {username}" : string.Empty;
                string rolePart = EditorLoginState.IsTenantManager ? " [TenantManager]" : "";

                // An expired token is not a broken state — the next operation renews it — but the
                // user should know a login prompt may appear before their deploy starts.
                string sessionPart = EditorSessionManager.DescribeSession(out bool tokenValid);

                loginStatusLabel.text = $"Logged in: {tenantLabel} {envLabel}{userPart}{rolePart}{sessionPart}";
                loginStatusLabel.style.color = tokenValid
                    ? new Color(0.2f, 0.8f, 0.2f)
                    : new Color(0.9f, 0.7f, 0.2f);

                deploySection.style.display = DisplayStyle.Flex;
                tenantDeploySection.style.display = EditorLoginState.IsTenantManager ? DisplayStyle.Flex : DisplayStyle.None;

                if (reloadWorlds)
                    LoadWorlds();
            }
            else
            {
                loginStatusLabel.text = "Not logged in";
                loginStatusLabel.style.color = new Color(0.8f, 0.4f, 0.2f);

                deploySection.style.display = DisplayStyle.None;
                tenantDeploySection.style.display = DisplayStyle.None;
                availableWorlds.Clear();
                selectedWorlds.Clear();
                worldDeployPreviews.Clear();

                // Where the scenes are published belongs to the session that just ended: drop it, and
                // with it the per-world filter choices it fed.
                PublishedEnvironmentsIndex.Clear();
                if (publicationFilter != null && publicationFilter.value != publication_all)
                {
                    publicationFilter.SetValueWithoutNotify(publication_all);
                    RebuildSceneList();
                }
            }

            ScheduleSessionExpiryRefresh();
        }

        // A renewal or a server rejection changes only the token: re-render the label, never the
        // worlds — this runs inside the API call that renewed it.
        private void OnTokenChanged() => RefreshLoginState(reloadWorlds: false);

        /// <summary>
        /// The label goes stale on its own when the token expires while the window sits idle:
        /// re-render it once, right after that moment. One run per token, re-armed on every
        /// render, so a renewal moves it and a logout drops it.
        /// </summary>
        private void ScheduleSessionExpiryRefresh()
        {
            sessionExpiryRefresh?.Pause();
            sessionExpiryRefresh = null;

            DateTime? usableUntil = EditorLoginState.TokenUsableUntilUtc;
            if (!EditorLoginState.IsTokenValid || usableUntil == null)
                return;

            // One second past the moment, so IsTokenValid has already flipped when it runs.
            long delayMs = (long)Math.Max(0, (usableUntil.Value - DateTime.UtcNow).TotalMilliseconds) + 1000;
            sessionExpiryRefresh = loginStatusLabel.schedule
                .Execute(() => RefreshLoginState(reloadWorlds: false))
                .StartingIn(delayMs);
        }

        private async void LoadWorlds()
        {
            worldsList.Clear();
            worldsLoadingLabel.style.display = DisplayStyle.Flex;
            worldsLoadingLabel.text = "Loading worlds...";
            deployButton.SetEnabled(false);
            buildAndDeployButton.SetEnabled(false);

            try
            {
                // Silent renewal only: this runs just from opening the window, and popping a
                // browser login at someone who only clicked a menu item would be startling.
                // Deploy actions, which the user explicitly asked for, do allow it.
                WorldListResult listing = await EnvironmentsDeploy.LoadWorldsAsync(
                    progress: text => worldsLoadingLabel.text = text,
                    // Where every scene is already published, shown under each scene by SceneConfigurationDrawer:
                    // computed on ALL the worlds the account can see, before the deploy-role filter.
                    onListed: visible => _ = PublishedEnvironmentsIndex.RefreshAsync(visible.Select(w => (w.Id, w.Label))));

                selectedWorlds.Clear();

                if (listing.Status != WorldListStatus.Ok)
                {
                    worldsLoadingLabel.text = listing.Message;
                    return;
                }

                availableWorlds = listing.Deployable;
                worldsLoadingLabel.style.display = DisplayStyle.None;

                HashSet<int> savedSelection = LoadSelectedWorldIds();
                worldDeployPreviews.Clear();

                if (availableWorlds.Count == 1)
                {
                    // Single world: show as label, auto-select
                    var world = availableWorlds[0];
                    selectedWorlds[world.Id] = true;
                    SaveSelectedWorldIds();

                    Label worldLabel = new Label($"{world.Label} (ID: {world.Id})");
                    worldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    worldLabel.style.marginTop = 4;
                    worldLabel.style.marginBottom = 4;
                    worldsList.Add(worldLabel);
                    worldsList.Add(worldDeployPreviews[world.Id] = NewDeployPreview());
                }
                else
                {
                    foreach (var world in availableWorlds)
                    {
                        bool wasSelected = savedSelection.Contains(world.Id);
                        selectedWorlds[world.Id] = wasSelected;

                        Toggle toggle = new Toggle
                        {
                            text = $"{world.Label} (ID: {world.Id})",
                            value = wasSelected
                        };
                        int worldId = world.Id;
                        VisualElement preview = NewDeployPreview();
                        preview.style.display = wasSelected ? DisplayStyle.Flex : DisplayStyle.None;
                        toggle.RegisterValueChangedCallback(evt =>
                        {
                            selectedWorlds[worldId] = evt.newValue;
                            SaveSelectedWorldIds();
                            preview.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                            RefreshDeployPreviews();
                        });
                        worldsList.Add(toggle);
                        worldsList.Add(worldDeployPreviews[worldId] = preview);
                    }
                }
                RefreshDeployPreviews();

                deployButton.SetEnabled(true);
                buildAndDeployButton.SetEnabled(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error loading worlds: {ex.Message}");
                worldsLoadingLabel.text = $"Error: {ex.Message}";
            }
        }

        private const string SELECTED_WORLDS_KEY = "Virtuademy_AddressablesManagement_SelectedWorlds";

        private List<int> GetSelectedWorldIds()
        {
            return selectedWorlds.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }

        private void SaveSelectedWorldIds()
        {
            var ids = GetSelectedWorldIds();
            EditorPrefs.SetString(SELECTED_WORLDS_KEY, string.Join(",", ids));
        }

        private HashSet<int> LoadSelectedWorldIds()
        {
            string saved = EditorPrefs.GetString(SELECTED_WORLDS_KEY, "");
            var result = new HashSet<int>();
            if (string.IsNullOrEmpty(saved)) return result;

            foreach (string part in saved.Split(','))
            {
                if (int.TryParse(part.Trim(), out int id))
                    result.Add(id);
            }
            return result;
        }

        private List<string> GetBuiltSceneNames()
        {
            return sceneConfigurations.SceneConfigurations
                .Where(x => x.IncludeInBuild)
                .Select(x => x.SceneNameFiltered)
                .ToList();
        }

        #endregion

        #region Deploy

        private async void OnDeployClicked()
        {
            ClearDeployErrors();

            List<int> worldIds = GetSelectedWorldIds();
            if (worldIds.Count == 0)
            {
                Debug.LogWarning("[AddressablesManagement] No worlds selected for deploy.");
                EditorUtility.DisplayDialog("Deploy", "Please select at least one world.", "OK");
                return;
            }

            List<string> scenes = GetBuiltSceneNames();
            if (scenes.Count == 0)
            {
                Debug.LogWarning("[AddressablesManagement] No scenes marked for build.");
                return;
            }

            // Check that zip files exist
            List<string> missingZips = new();
            foreach (string scene in scenes)
            {
                string zipPath = Path.Combine(addressables_output_folder, scene + ".zip");
                if (!File.Exists(zipPath))
                    missingZips.Add(scene);
            }

            if (missingZips.Count > 0)
            {
                string missing = string.Join(", ", missingZips);
                LogDeployError($"Missing zip files for: {missing}. Build first.");
                EditorUtility.DisplayDialog("Deploy", $"Missing zip files for:\n{missing}\n\nPlease build first.", "OK");
                return;
            }

            SetDeployButtonsEnabled(false);
            await DeployToWorlds(worldIds, scenes);
            SetDeployButtonsEnabled(true);
        }

        private async void OnBuildAndDeployClicked()
        {
            ClearDeployErrors();

            List<int> worldIds = GetSelectedWorldIds();
            if (worldIds.Count == 0)
            {
                Debug.LogWarning("[AddressablesManagement] No worlds selected for build & deploy.");
                EditorUtility.DisplayDialog("Build & Deploy", "Please select at least one world.", "OK");
                return;
            }

            await RunBuildAndDeployAsync(worldIds);
        }

        /// <summary>The build and deploy itself, with the world ids passed in rather than read off the window.</summary>
        /// <remarks>
        /// Assembly reloads are locked for the whole flow (<see cref="LockedReloadAsync"/>): the build switches the
        /// build target, which queues a recompilation and a domain reload, and a reload while the code below awaits
        /// the network would end it silently.
        /// </remarks>
        private Task RunBuildAndDeployAsync(List<int> worldIds) => LockedReloadAsync(() => BuildAndDeployToWorldsAsync(worldIds));

        /// <summary>
        /// Runs <paramref name="flow"/> with assembly reloads locked, and always unlocks afterwards: on every
        /// early return and on an exception too, because a lock left behind stops the editor from ever
        /// compiling a script again until it is restarted.
        /// </summary>
        private static async Task LockedReloadAsync(Func<Task> flow)
        {
            EditorApplication.LockReloadAssemblies();
            try
            {
                await flow();
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        private async Task BuildAndDeployToWorldsAsync(List<int> worldIds)
        {
            SetDeployButtonsEnabled(false);

            if (!RequireAddressablesConfigured("Build & Deploy"))
            {
                SetDeployButtonsEnabled(true);
                return;
            }

            // 0. Build + verify the interpreted DLL (local whitelist + authoritative server
            //    check). Block the whole build & deploy if it fails.
            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                LogDeployError("Interpreted script DLL failed the security checks. Build & deploy aborted (see Console).");
                EditorUtility.DisplayDialog("Build & Deploy blocked",
                    "The interpreted script DLL did not pass the security checks. See the Console. " +
                    "Nothing was built or deployed.", "OK");
                SetDeployButtonsEnabled(true);
                return;
            }

            // 1. Build
            BuildAndZipScenes();

            if (buildResult != EBuildError.None)
            {
                LogDeployError($"Build failed ({buildResult}). Aborting deploy.");
                SetDeployButtonsEnabled(true);
                return;
            }

            // 2. Deploy
            List<string> scenes = GetBuiltSceneNames();
            await DeployToWorlds(worldIds, scenes);

            SetDeployButtonsEnabled(true);
        }

        private async Task DeployToWorlds(List<int> worldIds, List<string> scenes)
        {
            int errorsBefore = deployErrorCount;
            DeployReport report = await EnvironmentsDeploy.DeployToWorldsAsync(worldIds, availableWorlds, scenes, NewDeployObserver());

            // Nothing was sent (no API URL, no session): the problem is already in the panel.
            if (report.Fatal != null)
                return;

            ReportDeployOutcome("World deploy", errorsBefore, report.ImportsStarted, report.Expected);
        }

        /// <summary>Errors go to the deploy panel and the Console, progress to the editor's progress bar.</summary>
        private DeployObserver NewDeployObserver() => new()
        {
            Error = LogDeployError,
            Progress = (title, info, fraction) => EditorUtility.DisplayProgressBar(title, info, fraction),
            ProgressDone = EditorUtility.ClearProgressBar,
        };

        private void SetDeployButtonsEnabled(bool enabled)
        {
            deployButton?.SetEnabled(enabled);
            buildAndDeployButton?.SetEnabled(enabled);
            tenantDeployButton?.SetEnabled(enabled);
            tenantBuildAndDeployButton?.SetEnabled(enabled);

            // Re-assert missing-module gating on build buttons — RefreshPlatformWarnings
            // will disable them again if any selected scene targets an uninstalled module.
            if (enabled)
                RefreshPlatformWarnings();
        }

        #endregion

        #region Tenant Deploy

        private async void OnTenantDeployClicked()
        {
            ClearDeployErrors();

            List<string> scenes = GetBuiltSceneNames();
            if (scenes.Count == 0)
            {
                Debug.LogWarning("[AddressablesManagement] No scenes marked for build.");
                return;
            }

            List<string> missingZips = new();
            foreach (string scene in scenes)
            {
                string zipPath = Path.Combine(addressables_output_folder, scene + ".zip");
                if (!File.Exists(zipPath))
                    missingZips.Add(scene);
            }

            if (missingZips.Count > 0)
            {
                string missing = string.Join(", ", missingZips);
                LogDeployError($"Missing zip files for: {missing}. Build first.");
                EditorUtility.DisplayDialog("Deploy to Tenant", $"Missing zip files for:\n{missing}\n\nPlease build first.", "OK");
                return;
            }

            SetDeployButtonsEnabled(false);
            await DeployToTenant(scenes);
            SetDeployButtonsEnabled(true);
        }

        private async void OnTenantBuildAndDeployClicked()
        {
            ClearDeployErrors();

            await RunTenantBuildAndDeployAsync();
        }

        /// <summary>Same as above for the tenant deploy, with the reloads locked the same way.</summary>
        private Task RunTenantBuildAndDeployAsync() => LockedReloadAsync(BuildAndDeployToTenantAsync);

        private async Task BuildAndDeployToTenantAsync()
        {
            SetDeployButtonsEnabled(false);

            if (!RequireAddressablesConfigured("Build & Deploy to Tenant"))
            {
                SetDeployButtonsEnabled(true);
                return;
            }

            // 0. Build + verify the interpreted DLL. Block the whole build & deploy if it fails.
            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                LogDeployError("Interpreted script DLL failed the security checks. Tenant build & deploy aborted (see Console).");
                EditorUtility.DisplayDialog("Build & Deploy blocked",
                    "The interpreted script DLL did not pass the security checks. See the Console. " +
                    "Nothing was built or deployed.", "OK");
                SetDeployButtonsEnabled(true);
                return;
            }

            BuildAndZipScenes();

            if (buildResult != EBuildError.None)
            {
                LogDeployError($"Build failed ({buildResult}). Aborting tenant deploy.");
                SetDeployButtonsEnabled(true);
                return;
            }

            List<string> scenes = GetBuiltSceneNames();
            await DeployToTenant(scenes);
            SetDeployButtonsEnabled(true);
        }

        private async Task DeployToTenant(List<string> scenes)
        {
            int tenantErrorsBefore = deployErrorCount;
            DeployReport report = await EnvironmentsDeploy.DeployToTenantAsync(scenes, NewDeployObserver());

            // Not even started, or stopped before any scene: the problem is already in the panel.
            if (report.Fatal != null || report.Targets.Count == 0 || !report.Targets[0].UploadLinkObtained)
                return;

            ReportDeployOutcome("Tenant deploy", tenantErrorsBefore, report.ImportsStarted, report.Expected);
        }

        #endregion

        #region Scene registry

        /// <summary>
        /// The scene list: every scene of the project, each with its settings (drawn by
        /// <see cref="SceneConfigurationDrawer"/>, which also says where the scene is already published),
        /// behind a search box and two filters. Entries are not added or removed by hand any more:
        /// <see cref="SceneListScriptableObject.SyncWithProject"/> keeps the list equal to the project.
        /// </summary>
        private void BuildSceneRegistryUi(VisualElement container)
        {
            container.Clear();

            VisualElement toolbar = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            sceneSearchField = new TextField { style = { flexGrow = 1, marginRight = 6 } };
            sceneSearchField.textEdition.placeholder = "Search scenes…";
            sceneSearchField.RegisterValueChangedCallback(_ => RebuildSceneList());
            onlyInBuildToggle = new Toggle("Only in build") { style = { marginRight = 6 } };
            onlyInBuildToggle.RegisterValueChangedCallback(_ => RebuildSceneList());
            publicationFilter = new DropdownField(new List<string> { publication_all, publication_published, publication_not_published }, 0)
            {
                tooltip = "Published: an environment with the scene's name exists in one of your worlds or at tenant level.\n" +
                          "A world (or Tenant level): only the scenes published there.",
                style = { marginRight = 6, minWidth = 120 }
            };
            publicationFilter.RegisterValueChangedCallback(_ => RebuildSceneList());
            RefreshPublicationFilterChoices();
            sceneCountLabel = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, opacity = 0.8f } };
            toolbar.Add(sceneSearchField);
            toolbar.Add(onlyInBuildToggle);
            toolbar.Add(publicationFilter);
            toolbar.Add(sceneCountLabel);
            container.Add(toolbar);

            Label hint = new("Every scene of the project is listed; tick \"Include in build\" on the ones to publish. New scenes appear here on their own, unticked.")
            {
                style = { whiteSpace = WhiteSpace.Normal, fontSize = 11, opacity = 0.75f, marginBottom = 4 }
            };
            container.Add(hint);

            sceneListContainer = new ScrollView { style = { maxHeight = 360 } };
            container.Add(sceneListContainer);

            // The folders whose scenes never make the list (package samples, third-party assets, plugins).
            Foldout excluded = new() { text = "Excluded folders", value = false, style = { marginTop = 4 } };
            excluded.tooltip = "Scenes under these folders are not the project's own and are left out of the list. Edit to fit the project.";
            PropertyField excludedField = new(sceneSerializedObject.FindProperty("excludedFolders"), string.Empty);
            excludedField.Bind(sceneSerializedObject);
            excludedField.RegisterCallback<SerializedPropertyChangeEvent>(_ => SyncSceneRegistry());
            excluded.Add(excludedField);
            container.Add(excluded);

            PublishedEnvironmentsIndex.Changed -= OnPublishedIndexChanged;
            PublishedEnvironmentsIndex.Changed += OnPublishedIndexChanged;
            RebuildSceneList();
        }

        private void OnPublishedIndexChanged()
        {
            // The rows refresh their own "Published in" line; only the filter needs the list rebuilt.
            // Checked before the refresh too: a place filter that disappears falls back to All, and the list must follow.
            bool wasFiltered = publicationFilter != null && publicationFilter.value != publication_all;
            RefreshPublicationFilterChoices();
            if (wasFiltered || (publicationFilter != null && publicationFilter.value != publication_all))
                RebuildSceneList();
            RefreshDeployPreviews();
        }

        /// <summary>
        /// All / Published / Not published, then one entry per world the index knows about (every
        /// world the account can see, not only the deployable ones) and Tenant level when readable.
        /// Each place carries, in brackets, how many of the project's scenes are published there — the
        /// number of rows the filter would show. Keeps the current choice when it still exists (by
        /// place, since its text changes with the count), falls back to All otherwise.
        /// </summary>
        private void RefreshPublicationFilterChoices()
        {
            if (publicationFilter == null) return;

            string current = publicationFilter.value;
            bool currentIsPlace = publicationPlaceChoices.TryGetValue(current ?? string.Empty, out int? currentPlace);

            publicationPlaceChoices.Clear();
            List<string> choices = new() { publication_all, publication_published, publication_not_published };

            List<string> sceneKeys = (sceneConfigurations?.SceneConfigurations ?? new List<SceneConfiguration>())
                .Where(c => c.Scene != null)
                .Select(c => PublishedEnvironmentsIndex.Key(c.Scene.name))
                .ToList();
            int CountIn(int? place) => sceneKeys.Count(k => PublishedEnvironmentsIndex.IsPublishedIn(k, place));

            if (PublishedEnvironmentsIndex.HasData && !PublishedEnvironmentsIndex.TenantUnknown)
            {
                string tenantChoice = $"{publication_tenant} ({CountIn(null)})";
                choices.Add(tenantChoice);
                publicationPlaceChoices[tenantChoice] = null;
            }

            // Without data (logged out, first load in progress) there is nothing to filter a world on.
            IReadOnlyList<(int Id, string Label)> worlds = PublishedEnvironmentsIndex.HasData
                ? PublishedEnvironmentsIndex.Worlds
                : Array.Empty<(int, string)>();
            HashSet<string> duplicateLabels = new(worlds.GroupBy(w => w.Label).Where(g => g.Count() > 1).Select(g => g.Key));
            foreach ((int id, string label) in worlds.OrderBy(w => w.Label, StringComparer.OrdinalIgnoreCase))
            {
                // The dropdown shows its value as text, so two worlds with the same name need the id to tell them apart.
                string worldName = duplicateLabels.Contains(label) ? $"{label} [ID: {id}]" : label;
                string choice = $"{publication_world_prefix}{worldName} ({CountIn(id)})";
                if (publicationPlaceChoices.ContainsKey(choice)) continue;
                choices.Add(choice);
                publicationPlaceChoices[choice] = id;
            }

            string next = currentIsPlace
                ? publicationPlaceChoices.FirstOrDefault(p => p.Value == currentPlace).Key ?? publication_all
                : choices.Contains(current) ? current : publication_all;
            publicationFilter.choices = choices;
            publicationFilter.SetValueWithoutNotify(next);
        }

        private void RebuildSceneList()
        {
            if (sceneListContainer == null || sceneSerializedObject == null) return;
            sceneSerializedObject.Update();
            sceneListContainer.Clear();

            SerializedProperty list = sceneSerializedObject.FindProperty("sceneConfigurations");
            List<SceneConfiguration> configs = sceneConfigurations.SceneConfigurations ?? new List<SceneConfiguration>();
            string search = sceneSearchField?.value?.Trim() ?? string.Empty;
            bool onlyInBuild = onlyInBuildToggle?.value ?? false;
            string publication = publicationFilter?.value ?? publication_all;

            int shown = 0;
            for (int i = 0; i < list.arraySize && i < configs.Count; i++)
            {
                SceneConfiguration cfg = configs[i];
                if (cfg.Scene == null) continue;
                if (search.Length > 0 && cfg.Scene.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (onlyInBuild && !cfg.IncludeInBuild) continue;
                if (publication != publication_all && PublishedEnvironmentsIndex.HasData)
                {
                    string key = PublishedEnvironmentsIndex.Key(cfg.Scene.name);
                    if (publicationPlaceChoices.TryGetValue(publication, out int? placeId))
                    {
                        if (!PublishedEnvironmentsIndex.IsPublishedIn(key, placeId)) continue;
                    }
                    else
                    {
                        bool published = PublishedEnvironmentsIndex.Get(key).Count > 0;
                        if (published != (publication == publication_published)) continue;
                    }
                }

                PropertyField row = new(list.GetArrayElementAtIndex(i));
                row.Bind(sceneSerializedObject);
                // Refresh platform warnings whenever a property changes (e.g. toggling a platform flag)
                row.RegisterCallback<SerializedPropertyChangeEvent>(_ => { RefreshPlatformWarnings(); RefreshDeployPreviews(); });
                sceneListContainer.Add(row);
                shown++;
            }

            sceneCountLabel.text = shown == configs.Count ? $"{configs.Count} scenes" : $"{shown} of {configs.Count} scenes";
        }

        /// <summary>Mirrors the project's scenes into the list and redraws it when something changed.</summary>
        private void SyncSceneRegistry()
        {
            if (sceneConfigurations == null) return;
            if (!sceneConfigurations.SyncWithProject()) return;
            SaveAsset(sceneConfigurations);
            RefreshPublicationFilterChoices();   // the per-world counts are over the project's scenes
            RebuildSceneList();
            RefreshPlatformWarnings();
            RefreshDeployPreviews();
        }

        #endregion

        #region Deploy preview

        private const string publication_all = "All";
        private const string publication_published = "Published";
        private const string publication_not_published = "Not published";
        private const string publication_tenant = "Tenant level";
        private const string publication_world_prefix = "World: ";

        /// <summary>Filter choices that point at one place: a world id, or null for tenant level.</summary>
        private readonly Dictionary<string, int?> publicationPlaceChoices = new();

        private static readonly Color preview_new_color = new(0.42f, 0.78f, 0.45f);
        private static readonly Color preview_update_color = new(0.95f, 0.66f, 0.28f);
        private static readonly Color preview_note_color = new(0.62f, 0.62f, 0.62f);

        private static VisualElement NewDeployPreview()
        {
            return new VisualElement { style = { marginLeft = 22, marginBottom = 4 } };
        }

        /// <summary>
        /// Under every selected world (and the tenant section): what the current "Include in build"
        /// selection does there — the scenes that arrive as new environments (green) and the ones that
        /// overwrite an environment already published with that name (orange).
        /// </summary>
        private void RefreshDeployPreviews()
        {
            foreach (KeyValuePair<int, VisualElement> pair in worldDeployPreviews)
            {
                if (selectedWorlds.TryGetValue(pair.Key, out bool selected) && selected)
                    FillDeployPreview(pair.Value, pair.Key);
                else
                    pair.Value.Clear();
            }
            if (tenantDeployPreview != null)
                FillDeployPreview(tenantDeployPreview, null);
        }

        private void FillDeployPreview(VisualElement target, int? worldId)
        {
            target.Clear();
            if (sceneConfigurations?.SceneConfigurations == null) return;

            DeployPreview preview = EnvironmentsDeploy.Preview(EnvironmentsBuild.IncludedScenes(sceneConfigurations), worldId);

            switch (preview.State)
            {
                case DeployPreviewState.NothingIncluded:
                    target.Add(PreviewLine("Nothing is included in the build.", preview_note_color));
                    return;
                case DeployPreviewState.Checking:
                    target.Add(PreviewLine("Checking what is already published…", preview_note_color));
                    return;
                case DeployPreviewState.Unreadable:
                    target.Add(PreviewLine($"{preview.Included} scene(s) in the build; what is already published could not be read.", preview_note_color));
                    return;
            }

            if (preview.Updated.Count > 0) target.Add(PreviewLine($"Updated ({preview.Updated.Count}): {string.Join(", ", preview.Updated)}", preview_update_color));
            if (preview.Added.Count > 0) target.Add(PreviewLine($"New ({preview.Added.Count}): {string.Join(", ", preview.Added)}", preview_new_color));
        }

        private static Label PreviewLine(string text, Color color)
        {
            return new Label(text) { style = { color = color, fontSize = 11, whiteSpace = WhiteSpace.Normal, marginBottom = 1 } };
        }

        #endregion

        #region Settings Loading

        private void LoadSettings()
        {
            sceneConfigurations = SceneListScriptableObject.LoadOrCreate();

            // The list is a registry of the project's scenes (see SceneListScriptableObject.SyncWithProject).
            if (sceneConfigurations.SyncWithProject())
                SaveAsset(sceneConfigurations);

            settings = AddressablesConfiguration.GetOrCreateSettings();
        }

        private void AddDataBindings()
        {
            sceneSerializedObject = new SerializedObject(sceneConfigurations);

            VisualElement sceneConfigContainer = root.Q<VisualElement>("scene-configuration-scriptable");
            BuildSceneRegistryUi(sceneConfigContainer);

            sceneSerializedObject.ApplyModifiedProperties();

            // Platform module warnings — injected just below the scene configuration block
            platformWarningsContainer = new VisualElement();
            platformWarningsContainer.style.marginTop = 6;
            sceneConfigContainer.parent.Insert(sceneConfigContainer.parent.IndexOf(sceneConfigContainer) + 1, platformWarningsContainer);
            RefreshPlatformWarnings();

            VisualElement addressablesSettings = root.Q<VisualElement>("addressables-settings");
            addressablesSettings.dataSource = this;

            List<(string, string)> settingIcons = new()
            {
                { ("addressables-profile-check", nameof(IsAddressablesSettingsConfigured)) },
                { ("remote-buildpath-check", nameof(IsRemoteBuildPathConfigured)) },
                { ("remote-loadpath-check", nameof(IsRemoteLoadPathConfigured)) },
                { ("build-target-check", nameof(IsBuildTargetConfigured)) },
                { ("player-version-override-check", nameof(IsPlayerVersionOverrideConfigured)) },
                { ("default-local-group-check", nameof(AreAddressablesConfigured)) },
            };
            foreach (var entry in settingIcons)
            {
                VisualElement projectSettingsItemIcon = addressablesSettings.Q<VisualElement>(entry.Item1);
                DataBinding styleBinding = new() { dataSourcePath = PropertyPath.FromName(entry.Item2) };
                styleBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                {
                    projectSettingsItemIcon.RemoveFromClassList("settings-item-green-icon");
                    projectSettingsItemIcon.RemoveFromClassList("settings-item-red-icon");
                    projectSettingsItemIcon.AddToClassList(value ? "settings-item-green-icon" : "settings-item-red-icon");
                    return true;
                });
                projectSettingsItemIcon.SetBinding(nameof(projectSettingsItemIcon.visible), styleBinding);
            }

            Label addressablesProfileValue = addressablesSettings.Q<Label>("addressables-profile-value");
            addressablesProfileValue.SetBinding(nameof(addressablesProfileValue.text), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(ActiveProfileName)),
                bindingMode = BindingMode.ToTarget
            });

            Label remoteBuildPathValue = addressablesSettings.Q<Label>("remote-buildpath-value");
            remoteBuildPathValue.SetBinding(nameof(remoteBuildPathValue.text), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(CurrentRemoteBuildPathVariableValue)),
                bindingMode = BindingMode.ToTarget
            });

            Label remoteLoadPathValue = addressablesSettings.Q<Label>("remote-loadpath-value");
            remoteLoadPathValue.SetBinding(nameof(remoteLoadPathValue.text), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(CurrentRemoteLoadPathVariableValue)),
                bindingMode = BindingMode.ToTarget
            });

            Label buildTargetValue = addressablesSettings.Q<Label>("build-target-value");
            buildTargetValue.SetBinding(nameof(buildTargetValue.text), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(CurrentBuildTargetVariableValue)),
                bindingMode = BindingMode.ToTarget
            });

            Label playerVersionOverrideValue = addressablesSettings.Q<Label>("player-version-override-value");
            playerVersionOverrideValue.SetBinding(nameof(playerVersionOverrideValue.text), new DataBinding() { dataSourcePath = PropertyPath.FromName(nameof(CurrentPlayerVersionOverrideVariableValue)) });

            Button topLevelSettingsButton = root.Q<Button>("top-level-settings-button");
            topLevelSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Settings");

            Button profileSettingsButton = root.Q<Button>("profile-settings-button");
            profileSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");

            Button groupsSettingsButton = root.Q<Button>("addressables-groups-button");
            groupsSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

            Button defaultLocalGroupButton = root.Q<Button>("default-local-group-button");
            defaultLocalGroupButton.clicked += () => Selection.activeObject = settings.DefaultGroup;

            // Build button — always visible (logged in or not)
            buildAddressablesButton = root.Q<Button>("build-addressables-button");
            buildAddressablesButton.dataSource = this;
            DataBinding buildAddressablesButtonDataBinding = new()
            {
                dataSourcePath = PropertyPath.FromName(nameof(AreAddressablesConfigured)),
                bindingMode = BindingMode.ToTarget
            };
            buildAddressablesButtonDataBinding.sourceToUiConverters.AddConverter((ref bool value) => AreAddressablesConfigured ? "Build Addressables" : "Fix Addressables configurations");
            buildAddressablesButton.SetBinding(nameof(buildAddressablesButton.text), buildAddressablesButtonDataBinding);
            buildAddressablesButton.clicked += async () =>
            {
                if (AreAddressablesConfigured)
                {
                    await RunBuildAddressablesAsync();
                }
                else
                {
                    AddressablesConfiguration.Apply();
                    settings = AddressablesConfiguration.GetOrCreateSettings();
                }
            };

            VisualElement buildErrors = root.Q<VisualElement>("build-result-errors");
            buildErrors.dataSource = this;

            VisualElement folderMissing = buildErrors.Q<VisualElement>("folder-missing");
            DataBinding folderMissingDataBinding = new()
            {
                dataSourcePath = PropertyPath.FromName(nameof(buildResult)),
                bindingMode = BindingMode.ToTarget
            };
            folderMissingDataBinding.sourceToUiConverters.AddConverter((ref EBuildError value) =>
            {
                folderMissing.style.display = buildResult == EBuildError.FolderMissing ? DisplayStyle.Flex : DisplayStyle.None;
                return true;
            });
            folderMissing.SetBinding(nameof(folderMissing.visible), folderMissingDataBinding);

            VisualElement binaryCatalog = buildErrors.Q<VisualElement>("binary-catalog");
            DataBinding binaryCatalogDataBinding = new()
            {
                dataSourcePath = PropertyPath.FromName(nameof(buildResult)),
                bindingMode = BindingMode.ToTarget
            };
            binaryCatalogDataBinding.sourceToUiConverters.AddConverter((ref EBuildError value) =>
            {
                binaryCatalog.style.display = buildResult == EBuildError.BinaryCatalog ? DisplayStyle.Flex : DisplayStyle.None;
                return true;
            });
            binaryCatalog.SetBinding(nameof(folderMissing.visible), binaryCatalogDataBinding);
        }

        #endregion

        #region Top-Level settings configuration

        [CreateProperty]
        public bool IsAddressablesSettingsConfigured => AddressablesConfiguration.IsSettingsConfigured(settings);

        #endregion

        #region Utility

        /// <summary>
        /// Rebuilds the platform-module warnings panel.
        /// Shows a HelpBox for every scene whose required build modules are not installed.
        /// Safe to call at any time; silently does nothing if the container isn't ready yet.
        /// </summary>
        private void RefreshPlatformWarnings()
        {
            if (platformWarningsContainer == null || sceneConfigurations == null) return;

            platformWarningsContainer.Clear();

            bool anyWarning = false;
            var configs = sceneConfigurations.SceneConfigurations;
            if (configs == null)
            {
                platformWarningsContainer.style.display = DisplayStyle.None;
                return;
            }
            foreach (var scene in configs)
            {
                if (scene == null || !scene.IncludeInBuild || scene.Scene == null) continue;

                List<BuildTarget> missingTargets = scene.GetMissingBuildTargets();
                if (missingTargets.Count == 0) continue;

                anyWarning = true;

                // Build a human-readable description of which platform each missing module covers
                var descriptions = missingTargets.Select(t => t switch
                {
                    BuildTarget.Android => "Android (VR / Mobile)",
                    BuildTarget.iOS => "iOS (Mobile)",
                    BuildTarget.WebGL => "WebGL",
                    _ => t.ToString()
                });

                string modulesText = string.Join(", ", descriptions);
                var helpBox = new HelpBox(
                    $"⚠  \"{scene.SceneNameFiltered}\": missing build module(s) — {modulesText}.\n" +
                    "Install them via Unity Hub → Installs → Add modules, then return here.",
                    HelpBoxMessageType.Warning
                );
                helpBox.style.marginBottom = 4;
                platformWarningsContainer.Add(helpBox);
            }

            platformWarningsContainer.style.display = anyWarning ? DisplayStyle.Flex : DisplayStyle.None;

            // Disable build buttons while any selected scene targets a non-installed module.
            // Plain deploy buttons (which only re-upload already-built zips) stay enabled.
            const string missingModulesTooltip =
                "Disabled: one or more selected scenes require build modules that are not installed. " +
                "See the warnings above.";

            if (buildAddressablesButton != null)
            {
                buildAddressablesButton.SetEnabled(!anyWarning);
                buildAddressablesButton.tooltip = anyWarning ? missingModulesTooltip : string.Empty;
            }
            if (buildAndDeployButton != null)
            {
                buildAndDeployButton.SetEnabled(!anyWarning);
                buildAndDeployButton.tooltip = anyWarning ? missingModulesTooltip : string.Empty;
            }
            if (tenantBuildAndDeployButton != null)
            {
                tenantBuildAndDeployButton.SetEnabled(!anyWarning);
                tenantBuildAndDeployButton.tooltip = anyWarning ? missingModulesTooltip : string.Empty;
            }
        }

        /// <summary>
        /// Logs an error both to the Unity console and to the in-window deploy error panel.
        /// </summary>
        private void LogDeployError(string message)
        {
            Debug.LogError(message);
            deployErrorCount++;

            if (deployErrorsScrollView == null) return;

            var entry = new HelpBox(message, HelpBoxMessageType.Error);
            entry.style.marginBottom = 2;
            deployErrorsScrollView.Add(entry);
            deployErrorsContainer.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Errors logged through <see cref="LogDeployError"/> since the window opened. A deploy reads
        /// it before and after, so its last line can say whether anything went wrong.
        /// </summary>
        private int deployErrorCount;

        /// <summary>
        /// The last line of a deploy: a plain completion only when nothing went wrong, otherwise how
        /// many problems there were and what got published, in the Console and in a dialog — the
        /// panel lists the problems, but a creator who looked away needs to be told to look.
        /// </summary>
        private void ReportDeployOutcome(string what, int errorsBefore, int importsStarted, int expected)
        {
            int problems = deployErrorCount - errorsBefore;
            if (problems == 0)
            {
                Debug.Log($"[AddressablesManagement] {what} completed: {importsStarted} scene import(s) started on the platform.");
                return;
            }

            string summary = $"{what} finished with {problems} problem(s): {importsStarted} of {expected} scene import(s) " +
                             "started on the platform. The problems are listed in the Addressables window and in the Console.";
            Debug.LogWarning("[AddressablesManagement] " + summary);
            EditorUtility.DisplayDialog($"{what} finished with problems", summary, "OK");
        }

        private void ClearDeployErrors()
        {
            deployErrorsScrollView?.Clear();
            if (deployErrorsContainer != null)
                deployErrorsContainer.style.display = DisplayStyle.None;
        }

        private void SaveAsset(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        #endregion

        #region Build

        /// <summary>Builds the interpreted DLL, verifies it, and builds the addressables only if it passed.</summary>
        private async Task RunBuildAddressablesAsync()
        {
            if (!RequireAddressablesConfigured("Build Addressables"))
                return;

            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                EditorUtility.DisplayDialog("Build blocked",
                    "The interpreted script DLL did not pass the security checks. " +
                    "See the Console for the offending lines. Addressables were NOT built.",
                    "OK");
                return;
            }

            BuildAndZipScenes();
        }

        /// <summary>
        /// The check every build entry makes before it starts: without the SDK's Addressables
        /// configuration a build runs for minutes and fails only at the end, because the bundles land
        /// in <c>Library/com.unity.addressables</c> instead of <c>ServerData</c>. Returns false after
        /// saying so in the deploy error panel, the Console and a dialog. It does not apply the
        /// configuration itself: that deletes every group but the default one and every entry in it,
        /// which the creator has to agree to by pressing the button.
        /// </summary>
        private bool RequireAddressablesConfigured(string what)
        {
            if (AddressablesConfiguration.IsConfigured)
                return true;

            LogDeployError($"{what} aborted: the project's Addressables are not configured the way the SDK needs. " +
                           "Press \"Fix Addressables configurations\" (the Build Addressables button turns into it; " +
                           "\"Show advanced addressables configurations\" lists what is wrong), then try again. Nothing was built.");
            EditorUtility.DisplayDialog($"{what} blocked",
                "The project's Addressables are not configured the way the SDK needs, so a build would fail " +
                "at the end. Press \"Fix Addressables configurations\" in this window first (it resets the " +
                "Addressables groups to a single default group), then try again. Nothing was built.", "OK");
            return false;
        }

        /// <summary>
        /// Builds the interpreted hot-update DLL and verifies it (local whitelist + authoritative
        /// server check). Returns true if the build may proceed; see
        /// <see cref="EnvironmentsBuild.VerifyInterpretedScriptsAsync"/>.
        /// </summary>
        private Task<bool> BuildAndVerifyInterpretedDLLAsync() => EnvironmentsBuild.VerifyInterpretedScriptsAsync();

        /// <summary>
        /// Builds addressables for all platforms and creates zip files.
        /// Does NOT upload — used by both "Build Addressables" button and "Build & Deploy".
        /// </summary>
        private void BuildAndZipScenes()
        {
            EnvironmentsBuildResult result = EnvironmentsBuild.BuildAndZip(EnvironmentsBuild.IncludedScenes(sceneConfigurations));

            if (result.Failure == EnvironmentsBuildFailure.MissingModule)
                EditorUtility.DisplayDialog("Missing Build Modules", result.Message, "OK");
            else if (result.Failure == EnvironmentsBuildFailure.FirstpassScripts)
                EditorUtility.DisplayDialog("Scripts the app cannot load", result.Message, "OK");

            buildResult = result.Failure switch
            {
                EnvironmentsBuildFailure.MissingModule => EBuildError.MissingModule,
                EnvironmentsBuildFailure.FirstpassScripts => EBuildError.FirstpassScripts,
                EnvironmentsBuildFailure.FolderMissing => EBuildError.FolderMissing,
                EnvironmentsBuildFailure.BinaryCatalog => EBuildError.BinaryCatalog,
                _ => EBuildError.None,
            };
        }

        #endregion

        #region Moved to EnvironmentsDeploy

        // The platform calls were public members of this window until they moved to EnvironmentsDeploy, which the
        // window and EnvironmentsCli share. These forward to it, so code written against the window keeps compiling.

        /// <summary>Moved to <see cref="EnvironmentsDeploy.GetUploadLinkForWorld"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.GetUploadLinkForWorld.")]
        public static Task<string> GetUploadLinkForWorld(string applicationApiUrl, int worldId)
            => EnvironmentsDeploy.GetUploadLinkForWorld(applicationApiUrl, worldId);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.ImportSceneToWorld"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.ImportSceneToWorld.")]
        public static Task<bool> ImportSceneToWorld(string applicationApiUrl, int worldId, string zipName)
            => EnvironmentsDeploy.ImportSceneToWorld(applicationApiUrl, worldId, zipName);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.FtpUploadConfig"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.FtpUploadConfig.")]
        public class FtpUploadConfig : EnvironmentsDeploy.FtpUploadConfig { }

        /// <summary>Moved to <see cref="EnvironmentsDeploy.UploadZip"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.UploadZip.")]
        public Task<bool> UploadZip(string filePath, string sasUrl)
            => EnvironmentsDeploy.UploadZip(filePath, sasUrl);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.UploadZipViaFtp"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.UploadZipViaFtp.")]
        public Task<bool> UploadZipViaFtp(string filePath, EnvironmentsDeploy.FtpUploadConfig ftpConfig)
            => EnvironmentsDeploy.UploadZipViaFtp(filePath, ftpConfig);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.GetTenantUploadLink"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.GetTenantUploadLink.")]
        public static Task<string> GetTenantUploadLink(string applicationApiUrl)
            => EnvironmentsDeploy.GetTenantUploadLink(applicationApiUrl);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.ImportEnvironmentDll"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.ImportEnvironmentDll.")]
        public static Task<(bool Ok, string Reason)> ImportEnvironmentDll(string applicationApiUrl, int worldId, string zipName)
            => EnvironmentsDeploy.ImportEnvironmentDll(applicationApiUrl, worldId, zipName);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.LinkEnvironmentDll"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.LinkEnvironmentDll.")]
        public static Task<bool> LinkEnvironmentDll(string applicationApiUrl, int worldId, string catalogName, string assemblyName)
            => EnvironmentsDeploy.LinkEnvironmentDll(applicationApiUrl, worldId, catalogName, assemblyName);

        /// <summary>Moved to <see cref="EnvironmentsDeploy.ImportSceneToTenant"/>.</summary>
        [Obsolete("Use EnvironmentsDeploy.ImportSceneToTenant.")]
        public static Task<bool> ImportSceneToTenant(string applicationApiUrl, string zipName)
            => EnvironmentsDeploy.ImportSceneToTenant(applicationApiUrl, zipName);

        #endregion
    }
}
