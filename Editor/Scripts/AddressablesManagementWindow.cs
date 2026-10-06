using Newtonsoft.Json;
using Virtuademy.SDK.TenantConfiguration.Editor;
using Virtuademy.SDK.TenantConfiguration;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
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
    /// <summary>
    /// The two fields this window reads off the platform's world listing.
    /// </summary>
    /// <remarks>
    /// Declared here rather than taken from the platform's own <c>WorldDTO</c>, because that type
    /// belongs to the HTTP client and lives in the package an application installs and a creator
    /// does not. Naming it here would put this package — the one every creator installs — a
    /// reference away from the platform client, which is the coupling the whole package split
    /// exists to prevent. The window already does its own request and its own deserialisation;
    /// this is the shape it deserialises into.
    /// </remarks>
    internal class PublishableWorld
    {
        public int Id { get; set; }

        public string Label { get; set; }
    }
    public class AddressablesManagementWindow : EditorWindow
    {
        private enum EBuildError
        {
            None,
            FolderMissing,
            BinaryCatalog,
            MissingModule,
        }

        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        private VisualElement root;

        private SceneListScriptableObject sceneConfigurations;

        private AddressableAssetSettings settings;

        private const string settings_folder_path = "Assets/Virtuademy/Editor/Settings";
        private const string addressables_configuration_file = "AddressablesSceneList.asset";

        private const string addressables_output_folder = "ServerData";

        private const string remote_build_path_variable_name = "Remote.BuildPath";
        private const string remote_load_path_variable_name = "Remote.LoadPath";

        private const string build_target_variable_name = "BuildTarget";
        private const string build_target_variable_value = "[UnityEditor.EditorUserBuildSettings.activeBuildTarget]";
        private const string player_version_override_variable_name = "PlayerVersionOverride";
        private const string player_version_override_variable_value = "[Virtuademy.SDK.Environments.Editor.AddressablesBuildScript.PlayerVersionOverride]";

        private string remoteBuildPath;
        private string remoteLoadPath;

        [SerializeField] private EBuildError buildResult = EBuildError.None;

        [CreateProperty] private string ActiveProfileName => settings.profileSettings.GetProfileName(settings.activeProfileId);

        [CreateProperty] private string CurrentRemoteBuildPathVariableValue => settings.profileSettings.GetValueByName(settings.activeProfileId, remote_build_path_variable_name);
        [CreateProperty] private bool IsRemoteBuildPathConfigured => CurrentRemoteBuildPathVariableValue == remoteBuildPath;

        [CreateProperty] private string CurrentRemoteLoadPathVariableValue => settings.profileSettings.GetValueByName(settings.activeProfileId, remote_load_path_variable_name);
        [CreateProperty] private bool IsRemoteLoadPathConfigured => CurrentRemoteLoadPathVariableValue == remoteLoadPath;

        [CreateProperty] private string CurrentBuildTargetVariableValue => settings.profileSettings.GetValueByName(settings.activeProfileId, build_target_variable_name);
        [CreateProperty] private bool IsBuildTargetConfigured => CurrentBuildTargetVariableValue == build_target_variable_value;

        [CreateProperty] private string CurrentPlayerVersionOverrideVariableValue => settings.profileSettings.GetValueByName(settings.activeProfileId, player_version_override_variable_name);
        [CreateProperty] private bool IsPlayerVersionOverrideConfigured => CurrentPlayerVersionOverrideVariableValue == player_version_override_variable_value;

        [CreateProperty] private bool AreAddressablesConfigured => IsAddressablesSettingsConfigured && IsProfileConfigured && AreAddressablesGroupsConfigured;


        private static HttpClient httpClient = new HttpClient();

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
                EditorApplication.ExecuteMenuItem("Virtuademy/Show available tenants");
            };

            // Through the session manager, so the Azure refresh token goes away too: clearing
            // only EditorLoginState would leave a cache that logs the user straight back in.
            logoutButton = new Button(async () => await EditorSessionManager.LogoutAsync())
            {
                text = "Logout"
            };
            logoutButton.style.display = DisplayStyle.None;
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
                bool tokenValid = EditorLoginState.IsTokenValid;
                string sessionPart = tokenValid
                    ? $" (session until {EditorSessionManager.DescribeExpiry()})"
                    : " - session expired, will be renewed on the next operation";

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
                string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrl;
                if (string.IsNullOrEmpty(applicationApiUrl))
                {
                    worldsLoadingLabel.text = "Error: no application API URL in tenant config";
                    return;
                }

                string apiUrl = $"{applicationApiUrl}/worlds?api-version=2";

                // Silent renewal only: this runs just from opening the window, and popping a
                // browser login at someone who only clicked a menu item would be startling.
                // Deploy actions, which the user explicitly asked for, do allow it.
                var response = await EditorSessionManager.SendAuthorizedAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, apiUrl), httpClient, allowInteractive: false);

                if (response == null)
                {
                    worldsLoadingLabel.text = "Session expired. Log in again to load the worlds.";
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[AddressablesManagement] Failed to get worlds: {response.StatusCode} - {body}");
                    worldsLoadingLabel.text = $"Error loading worlds: {response.StatusCode}";
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                availableWorlds = JsonConvert.DeserializeObject<List<PublishableWorld>>(json) ?? new();
                // Where every scene is already published, shown under each scene by SceneConfigurationDrawer:
                // computed on ALL the worlds the account can see, before the deploy-role filter below.
                _ = PublishedEnvironmentsIndex.RefreshAsync(availableWorlds.Select(w => (w.Id, w.Label)));
                selectedWorlds.Clear();

                // Filter worlds by user roles
                worldsLoadingLabel.text = "Checking permissions...";
                List<PublishableWorld> deployableWorlds = new();
                string[] deployRoles = { "TenantManager", "EnvironmentManager", "Owner" };

                foreach (var world in availableWorlds)
                {
                    try
                    {
                        string rolesUrl = $"{applicationApiUrl}/worlds/{world.Id}/users/my/roles?api-version=2";
                        var rolesResponse = await EditorSessionManager.SendAuthorizedAsync(
                            () => new HttpRequestMessage(HttpMethod.Get, rolesUrl), httpClient, allowInteractive: false);

                        if (rolesResponse == null)
                        {
                            worldsLoadingLabel.text = "Session expired. Log in again to load the worlds.";
                            return;
                        }

                        if (rolesResponse.IsSuccessStatusCode)
                        {
                            string rolesJson = await rolesResponse.Content.ReadAsStringAsync();
                            List<string> roles = JsonConvert.DeserializeObject<List<string>>(rolesJson) ?? new();

                            if (roles.Any(r => deployRoles.Contains(r)))
                            {
                                deployableWorlds.Add(world);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AddressablesManagement] Could not check roles for world {world.Id}: {ex.Message}");
                    }
                }

                availableWorlds = deployableWorlds;
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

        /// <summary>
        /// The build and deploy itself, with the world ids passed in rather than read off the
        /// window. That is what makes it re-runnable after a domain reload: the selection lives in
        /// a dictionary that is not serialized, so it does not survive one, while the ids parked in
        /// SessionState do.
        /// </summary>
        private async Task RunBuildAndDeployAsync(List<int> worldIds)
        {
            SetDeployButtonsEnabled(false);

            // 0. Build + verify the interpreted DLL (local whitelist + authoritative server
            //    check). Block the whole build & deploy if it fails.
            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                if (InterpretedDllAwaitingRecompile())
                {
                    // Nothing failed: the gate renamed the hot-update assembly because the scripts
                    // changed, and Unity is recompiling it. Park what was asked and let the reload
                    // press the button again — the creator sees a pause, not a question.
                    ParkDeployForResume(ResumeWorlds + string.Join(",", worldIds));
                    SetDeployButtonsEnabled(true);
                    return;
                }

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
            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrl;
            if (string.IsNullOrEmpty(applicationApiUrl))
            {
                LogDeployError("No application API URL available.");
                return;
            }

            // Renew the session before the uploads start rather than discovering it is dead on
            // the first call: the renewal can need an interactive login, and that is far less
            // disruptive here than halfway through a multi-world deploy. Every call below still
            // re-checks on its own — this deploy can outlive the token it starts with.
            if (!await EditorSessionManager.EnsureValidTokenAsync())
            {
                LogDeployError("Could not establish a valid session. Nothing was deployed.");
                return;
            }

            int totalWorlds = worldIds.Count;

            try
            {
                for (int w = 0; w < totalWorlds; w++)
                {
                    int worldId = worldIds[w];
                    PublishableWorld world = availableWorlds.FirstOrDefault(wd => wd.Id == worldId);
                    string worldLabel = world?.Label ?? worldId.ToString();

                    string progressPrefix = $"Deploy ({w + 1}/{totalWorlds}) - World \"{worldLabel}\"";

                    EditorUtility.DisplayProgressBar(progressPrefix, "Getting upload link...", (float)w / totalWorlds);

                    string uploadLink = await GetUploadLinkForWorld(applicationApiUrl, worldId);
                    if (string.IsNullOrEmpty(uploadLink))
                    {
                        LogDeployError($"World \"{worldLabel}\": failed to get upload link. Skipping.");
                        continue;
                    }

                    bool useFtp = EditorLoginState.CurrentTenant?.Env == Env.Sandbox;
                    FtpUploadConfig ftpConfig = null;

                    if (useFtp)
                    {
                        try
                        {
                            ftpConfig = JsonConvert.DeserializeObject<FtpUploadConfig>(uploadLink);
                            if (ftpConfig == null || string.IsNullOrEmpty(ftpConfig.host))
                            {
                                LogDeployError($"World \"{worldLabel}\": invalid SFTP config. Skipping.");
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDeployError($"World \"{worldLabel}\": failed to parse SFTP config — {ex.Message}. Skipping.");
                            continue;
                        }
                    }

                    // Before the scenes: the archive import resolves the assembly each scene zip
                    // declares, so the assembly has to be registered when the first scene arrives.
                    HashSet<string> rejectedAssemblies = await ImportDeclaredAssembliesAsync(
                        applicationApiUrl, worldId, worldLabel, scenes, uploadLink, useFtp, ftpConfig, progressPrefix);

                    List<string> importedScenes = new();

                    for (int s = 0; s < scenes.Count; s++)
                    {
                        string scene = scenes[s];
                        string zipPath = Path.Combine(addressables_output_folder, scene + ".zip");
                        if (!File.Exists(zipPath))
                        {
                            LogDeployError($"World \"{worldLabel}\": zip not found for \"{scene}\". Skipping scene.");
                            continue;
                        }

                        string declared = ReadDeclaredAssembly(scene, out _);
                        if (declared != null && rejectedAssemblies.Contains(declared))
                        {
                            // Publishing it would hand the world a scene whose scripts were turned down.
                            LogDeployError($"World \"{worldLabel}\": \"{scene}\" was not published, because its interpreted " +
                                           "assembly was not accepted (see above).");
                            continue;
                        }

                        float progress = ((float)w + (float)s / scenes.Count) / totalWorlds;

                        EditorUtility.DisplayProgressBar(progressPrefix, $"Uploading {scene}.zip ({s + 1}/{scenes.Count})...", progress);

                        bool uploaded = useFtp
                            ? await UploadZipViaFtp(zipPath, ftpConfig)
                            : await UploadZip(zipPath, uploadLink);

                        if (!uploaded)
                        {
                            LogDeployError($"World \"{worldLabel}\": upload failed for \"{scene}.zip\". Skipping import.");
                            continue;
                        }

                        EditorUtility.DisplayProgressBar(progressPrefix, $"Importing {scene} ({s + 1}/{scenes.Count})...", progress);

                        bool imported = await ImportSceneToWorld(applicationApiUrl, worldId, scene);
                        if (imported)
                            importedScenes.Add(scene);
                        else
                            LogDeployError($"World \"{worldLabel}\": import failed for \"{scene}\". Check the console for details.");
                    }

                    await LinkDeclaredAssembliesAsync(applicationApiUrl, worldLabel, worldId, importedScenes);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[AddressablesManagement] All world deploys completed.");
            _ = PublishedEnvironmentsIndex.RefreshAsync();
        }

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

        /// <summary>Same as above for the tenant deploy, which needs no inputs to be re-runnable.</summary>
        private async Task RunTenantBuildAndDeployAsync()
        {
            SetDeployButtonsEnabled(false);

            // 0. Build + verify the interpreted DLL. Block the whole build & deploy if it fails.
            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                if (InterpretedDllAwaitingRecompile())
                {
                    ParkDeployForResume(ResumeTenant);
                    SetDeployButtonsEnabled(true);
                    return;
                }

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
            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrl;
            if (string.IsNullOrEmpty(applicationApiUrl))
            {
                LogDeployError("Tenant: no application API URL available.");
                return;
            }

            // See DeployToWorlds: renew before the uploads, not on the first failure.
            if (!await EditorSessionManager.EnsureValidTokenAsync())
            {
                LogDeployError("Tenant: could not establish a valid session. Nothing was deployed.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Deploy to Tenant", "Getting upload link...", 0f);

                string uploadLink = await GetTenantUploadLink(applicationApiUrl);
                if (string.IsNullOrEmpty(uploadLink))
                {
                    LogDeployError("Tenant: failed to get upload link.");
                    return;
                }

                bool useFtp = EditorLoginState.CurrentTenant?.Env == Env.Sandbox;
                FtpUploadConfig ftpConfig = null;

                if (useFtp)
                {
                    try
                    {
                        ftpConfig = JsonConvert.DeserializeObject<FtpUploadConfig>(uploadLink);
                        if (ftpConfig == null || string.IsNullOrEmpty(ftpConfig.host))
                        {
                            LogDeployError("Tenant: invalid SFTP config. Aborting.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDeployError($"Tenant: failed to parse SFTP config — {ex.Message}. Aborting.");
                        return;
                    }
                }

                for (int s = 0; s < scenes.Count; s++)
                {
                    string scene = scenes[s];
                    string zipPath = Path.Combine(addressables_output_folder, scene + ".zip");
                    if (!File.Exists(zipPath))
                    {
                        LogDeployError($"Tenant: zip not found for \"{scene}\". Skipping scene.");
                        continue;
                    }

                    float progress = (float)s / scenes.Count;

                    EditorUtility.DisplayProgressBar("Deploy to Tenant", $"Uploading {scene}.zip ({s + 1}/{scenes.Count})...", progress);

                    bool uploaded = useFtp
                        ? await UploadZipViaFtp(zipPath, ftpConfig)
                        : await UploadZip(zipPath, uploadLink);

                    if (!uploaded)
                    {
                        LogDeployError($"Tenant: upload failed for \"{scene}.zip\". Skipping import.");
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Deploy to Tenant", $"Importing {scene} ({s + 1}/{scenes.Count})...", progress);

                    bool imported = await ImportSceneToTenant(applicationApiUrl, scene);
                    if (!imported)
                        LogDeployError($"Tenant: import failed for \"{scene}\". Check the console for details.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[AddressablesManagement] Tenant deploy completed.");
            _ = PublishedEnvironmentsIndex.RefreshAsync();
        }

        /// <summary>The setupper lives in the HYBRIDCLR_INSTALLED-gated assembly this window does
        /// not reference, so it is reached by reflection like the verifier is.</summary>
        private static Type FindHotUpdateSetupperType() => FindEditorType("HotUpdateSetupper");

        private static Type FindEditorType(string name)
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);

        /// <summary>
        /// Uploads and imports the interpreted assemblies the scene zips declare, BEFORE the scenes:
        /// the platform's archive import resolves the assembly a scene declares and refuses the
        /// scene when it is not registered. Normally one assembly — the bundle built with the
        /// scenes, <c>ServerData/&lt;AssemblyName&gt;.zip</c>.
        ///
        /// Uploaded on every deploy, even when the scripts have not changed since the last one: the
        /// import of a name already registered is a no-op on the platform, and the marker that says
        /// "already published" is per project, not per tenant, so skipping would leave a deploy to a
        /// second tenant without its scripts.
        ///
        /// Returns the assemblies the platform did not accept, so the caller can hold back the
        /// scenes that need them.
        /// </summary>
        private async Task<HashSet<string>> ImportDeclaredAssembliesAsync(string applicationApiUrl, int worldId, string worldLabel,
                                                                         List<string> scenes, string uploadLink,
                                                                         bool useFtp, FtpUploadConfig ftpConfig, string progressPrefix)
        {
            HashSet<string> rejected = new(StringComparer.Ordinal);

            IEnumerable<string> declared = scenes
                .Select(scene => ReadDeclaredAssembly(scene, out _))
                .Where(name => name != null)
                .Distinct(StringComparer.Ordinal);

            foreach (string assemblyName in declared)
            {
                string zipPath = Path.Combine(addressables_output_folder, assemblyName + ".zip");
                if (!File.Exists(zipPath))
                {
                    // The build could not produce the bundle and said why. The scenes still go: the
                    // platform accepts them where this assembly is already registered.
                    Debug.LogWarning($"[AddressablesManagement] World \"{worldLabel}\": no bundle for '{assemblyName}' in " +
                                     $"{addressables_output_folder}. The scenes that need it import only if it is already registered.");
                    continue;
                }

                EditorUtility.DisplayProgressBar(progressPrefix, "Uploading interpreted assembly...", 0f);

                bool uploaded = useFtp
                    ? await UploadZipViaFtp(zipPath, ftpConfig)
                    : await UploadZip(zipPath, uploadLink);

                if (!uploaded)
                {
                    LogDeployError($"World \"{worldLabel}\": upload failed for \"{Path.GetFileName(zipPath)}\".");
                    rejected.Add(assemblyName);
                    continue;
                }

                if (!await ImportEnvironmentDll(applicationApiUrl, worldId, Path.GetFileName(zipPath)))
                {
                    LogDeployError($"World \"{worldLabel}\": the platform rejected the interpreted assembly '{assemblyName}'.");
                    rejected.Add(assemblyName);
                    continue;
                }

                // Only now: a marker written before the import would remember a failed publish as
                // done, and the next build would skip its compile and verification.
                FindHotUpdateSetupperType()?
                    .GetMethod("MarkBundlePublished", BindingFlags.Public | BindingFlags.Static)?
                    .Invoke(null, null);
            }

            return rejected;
        }

        /// <summary>
        /// Points each imported scene's catalog at the assembly it declares, or clears the link for
        /// a scene built without scripts. A platform that reads the declaration has already done
        /// this during the archive import, and repeating it changes nothing; one that predates it
        /// does not, and this is the only call that links its scenes.
        /// </summary>
        private async Task LinkDeclaredAssembliesAsync(string applicationApiUrl, string worldLabel, int worldId, List<string> scenes)
        {
            foreach (string scene in scenes)
            {
                string assemblyName = ReadDeclaredAssembly(scene, out bool declares);
                if (!declares)
                {
                    Debug.LogWarning($"[AddressablesManagement] World \"{worldLabel}\": \"{scene}\" was built before the " +
                                     "scene zips declared their interpreted assembly, so its script link was left as it was. " +
                                     "Build the scenes again.");
                    continue;
                }

                // An empty body clears the link.
                if (!await LinkEnvironmentDll(applicationApiUrl, worldId, scene, assemblyName ?? string.Empty))
                    LogDeployError($"World \"{worldLabel}\": could not link the interpreted assembly to \"{scene}\".");
            }
        }

        #endregion

        #region API calls

        public static async Task<string> GetUploadLinkForWorld(string applicationApiUrl, int worldId)
        {
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/upload-link?api-version=2";

            HttpResponseMessage response;
            try
            {
                response = await EditorSessionManager.SendAuthorizedAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, apiUrl), httpClient);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error getting upload link for world {worldId}: {ex.Message}");
                return null;
            }

            if (response == null)
            {
                Debug.LogError($"[AddressablesManagement] Could not get an upload link for world {worldId}: no valid session.");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[AddressablesManagement] GetUploadLink failed for world {worldId}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            string uploadLink = await response.Content.ReadAsStringAsync();
            // Remove surrounding quotes if the API returns a JSON string
            uploadLink = uploadLink.Trim('"');
            Debug.Log($"[AddressablesManagement] Upload link received for world {worldId}");
            return uploadLink;
        }

        public static async Task<bool> ImportSceneToWorld(string applicationApiUrl, int worldId, string zipName)
        {
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environments/archives/import?api-version=2";

            HttpResponseMessage response;
            try
            {
                response = await EditorSessionManager.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent($"\"{zipName + ".zip"}\"", Encoding.UTF8, "application/json")
                }, httpClient);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error importing {zipName} to world {worldId}: {ex.Message}");
                return false;
            }

            if (response == null)
            {
                Debug.LogError($"[AddressablesManagement] Could not import {zipName} to world {worldId}: no valid session.");
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[AddressablesManagement] Import failed for {zipName} to world {worldId}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                return false;
            }

            Debug.Log($"[AddressablesManagement] Import {zipName} to world {worldId} started successfully.");
            return true;
        }

        [Serializable]
        public class FtpUploadConfig
        {
            public string type;
            public string host;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [System.ComponentModel.DefaultValue(22)]
            public int port = 22;
            public string username;
            public string password;
            public string remotePath;
            public string privateKey;
            public string passphrase;
        }

        public async Task<bool> UploadZip(string filePath, string sasUrl)
        {
            string fileName = Path.GetFileName(filePath);

            var uriBuilder = new UriBuilder(sasUrl);
            uriBuilder.Path = uriBuilder.Path.TrimEnd('/') + "/" + fileName;
            Uri uploadUri = uriBuilder.Uri;

            byte[] fileBytes = File.ReadAllBytes(filePath);
            var content = new ByteArrayContent(fileBytes);
            content.Headers.Add("x-ms-blob-type", "BlockBlob");

            try
            {
                HttpResponseMessage response = await httpClient.PutAsync(uploadUri, content);
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log($"[AddressablesManagement] Upload completed: {fileName}");
                    return true;
                }
                else
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[AddressablesManagement] Upload failed: {fileName} - {response.StatusCode}\n{responseBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Upload error {fileName}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UploadZipViaFtp(string filePath, FtpUploadConfig ftpConfig)
        {
            string fileName = Path.GetFileName(filePath);
            // Resolve to absolute path before entering Task.Run — the thread-pool thread
            // may have a different working directory than the Unity project root.
            string absoluteFilePath = Path.GetFullPath(filePath);

            if (ftpConfig.port <= 0 || ftpConfig.port > 65535)
            {
                Debug.LogWarning($"[AddressablesManagement] Invalid SFTP port ({ftpConfig.port}), defaulting to 22.");
                ftpConfig.port = 22;
            }

            string remotePath = ftpConfig.remotePath ?? "/";
            remotePath = remotePath.Replace('\\', '/');
            if (!remotePath.StartsWith("/"))
                remotePath = "/" + remotePath;

            string remoteFilePath = remotePath.TrimEnd('/') + "/" + fileName;

            try
            {
                await Task.Run(() =>
                {
                    ConnectionInfo connectionInfo;

                    if (!string.IsNullOrEmpty(ftpConfig.privateKey))
                    {
                        PrivateKeyFile keyFile;
                        using (var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(ftpConfig.privateKey)))
                        {
                            keyFile = string.IsNullOrEmpty(ftpConfig.passphrase)
                                ? new PrivateKeyFile(keyStream)
                                : new PrivateKeyFile(keyStream, ftpConfig.passphrase);
                        }

                        var keyAuth = new PrivateKeyAuthenticationMethod(ftpConfig.username, keyFile);
                        connectionInfo = new ConnectionInfo(ftpConfig.host, ftpConfig.port, ftpConfig.username, keyAuth);
                    }
                    else
                    {
                        string decodedPassword = Encoding.UTF8.GetString(Convert.FromBase64String(ftpConfig.password));
                        var passAuth = new PasswordAuthenticationMethod(ftpConfig.username, decodedPassword);
                        var kbdAuth = new KeyboardInteractiveAuthenticationMethod(ftpConfig.username);
                        kbdAuth.AuthenticationPrompt += (sender, e) =>
                        {
                            foreach (var prompt in e.Prompts)
                            {
                                prompt.Response = decodedPassword;
                            }
                        };
                        connectionInfo = new ConnectionInfo(ftpConfig.host, ftpConfig.port, ftpConfig.username, passAuth, kbdAuth);
                    }

                    using (var sftp = new SftpClient(connectionInfo))
                    {
                        sftp.Connect();

                        // Ensure every segment of the remote directory exists before uploading.
                        // SSH.NET throws "Can't open file" when the destination folder is missing.
                        string[] segments = remotePath.TrimStart('/').Split('/');
                        string currentDir = "";
                        foreach (string segment in segments)
                        {
                            if (string.IsNullOrEmpty(segment)) continue;
                            currentDir += "/" + segment;
                            if (!sftp.Exists(currentDir))
                                sftp.CreateDirectory(currentDir);
                        }

                        using (var fileStream = File.OpenRead(absoluteFilePath))
                        {
                            sftp.UploadFile(fileStream, remoteFilePath, true);
                        }

                        sftp.Disconnect();
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] SFTP upload error {fileName}: {ex.Message}");
                if (ex.InnerException != null)
                    Debug.LogError($"[AddressablesManagement] Inner exception: {ex.InnerException.Message}");
                return false;
            }
        }

        public static async Task<string> GetTenantUploadLink(string applicationApiUrl)
        {
            string apiUrl = $"{applicationApiUrl}/tenants/environments/upload-link?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, apiUrl), httpClient);

                if (response == null)
                {
                    Debug.LogError("[AddressablesManagement] Could not get the tenant upload link: no valid session.");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[AddressablesManagement] GetTenantUploadLink failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return null;
                }

                string uploadLink = await response.Content.ReadAsStringAsync();
                uploadLink = uploadLink.Trim('"');
                Debug.Log("[AddressablesManagement] Tenant upload link received.");
                return uploadLink;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error getting tenant upload link: {ex.Message}");
                return null;
            }
        }


        public static async Task<bool> ImportEnvironmentDll(string applicationApiUrl, int worldId, string zipName)
        {
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environment-dll/import?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent($"\"{zipName}\"", Encoding.UTF8, "application/json")
                }, httpClient);

                if (response == null)
                {
                    Debug.LogError("[AddressablesManagement] Could not import the environment DLL: no valid session.");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Read only on the way out: a 422 carries the whitelist violations, one per
                    // offending reference, each tagged with the platform DLL it came from — worth
                    // surfacing verbatim. The success body is the stored record, which is the
                    // platform's business and not something a creator's console should carry.
                    string body = await response.Content.ReadAsStringAsync();

                    Debug.LogError($"[AddressablesManagement] Environment DLL import failed: {response.StatusCode} - {body}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error importing the environment DLL: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> LinkEnvironmentDll(string applicationApiUrl, int worldId,
                                                          string catalogName, string assemblyName)
        {
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environments/catalog/{catalogName}/environment-dll?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Put, apiUrl)
                {
                    Content = new StringContent($"\"{assemblyName}\"", Encoding.UTF8, "application/json")
                }, httpClient);

                if (response == null)
                {
                    Debug.LogError($"[AddressablesManagement] Could not link {catalogName}: no valid session.");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[AddressablesManagement] Linking {catalogName} to {assemblyName} failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error linking {catalogName}: {ex.Message}");
                return false;
            }
        }
        public static async Task<bool> ImportSceneToTenant(string applicationApiUrl, string zipName)
        {
            string apiUrl = $"{applicationApiUrl}/tenants/environments/archives/import?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent($"\"{zipName + ".zip"}\"", Encoding.UTF8, "application/json")
                }, httpClient);

                if (response == null)
                {
                    Debug.LogError($"[AddressablesManagement] Could not import {zipName} to the tenant: no valid session.");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[AddressablesManagement] Tenant import failed for {zipName}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return false;
                }

                Debug.Log($"[AddressablesManagement] Tenant import {zipName} started successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error importing {zipName} to tenant: {ex.Message}");
                return false;
            }
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

            List<SceneConfiguration> built = sceneConfigurations.SceneConfigurations
                .Where(c => c.Scene != null && c.IncludeInBuild).ToList();

            if (built.Count == 0)
            {
                target.Add(PreviewLine("Nothing is included in the build.", preview_note_color));
                return;
            }
            if (!PublishedEnvironmentsIndex.HasData)
            {
                target.Add(PreviewLine(PublishedEnvironmentsIndex.IsLoading
                    ? "Checking what is already published…"
                    : $"{built.Count} scene(s) in the build; what is already published could not be read.", preview_note_color));
                return;
            }

            List<string> updated = new(), added = new();
            foreach (SceneConfiguration cfg in built)
            {
                (PublishedEnvironmentsIndex.IsPublishedIn(cfg.SceneNameFiltered, worldId) ? updated : added).Add(cfg.Scene.name);
            }

            if (updated.Count > 0) target.Add(PreviewLine($"Updated ({updated.Count}): {string.Join(", ", updated)}", preview_update_color));
            if (added.Count > 0) target.Add(PreviewLine($"New ({added.Count}): {string.Join(", ", added)}", preview_new_color));
        }

        private static Label PreviewLine(string text, Color color)
        {
            return new Label(text) { style = { color = color, fontSize = 11, whiteSpace = WhiteSpace.Normal, marginBottom = 1 } };
        }

        #endregion

        #region Settings Loading

        private void LoadSettings()
        {
            string addressablesBundleScriptableObjectsStr = AssetDatabase.FindAssets("t:" + typeof(SceneListScriptableObject).Name).ToList().FirstOrDefault();
            sceneConfigurations = AssetDatabase.LoadAssetAtPath<SceneListScriptableObject>(AssetDatabase.GUIDToAssetPath(addressablesBundleScriptableObjectsStr));

            if (sceneConfigurations == null)
            {
                EnsureFolderExists(settings_folder_path);

                sceneConfigurations = CreateInstance<SceneListScriptableObject>();
                string settingsAssetPath = $"{settings_folder_path}/{addressables_configuration_file}";
                AssetDatabase.CreateAsset(sceneConfigurations, settingsAssetPath);
                AssetDatabase.SaveAssets();
            }

            // The list is a registry of the project's scenes (see SceneListScriptableObject.SyncWithProject).
            if (sceneConfigurations.SyncWithProject())
                SaveAsset(sceneConfigurations);

            settings = AddressablesBuildScript.GetSettingsObject(AddressablesBuildScript.settings_asset);

            if (!settings)
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                               AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
                settings = AddressablesBuildScript.GetSettingsObject(AddressablesBuildScript.settings_asset);
            }

            remoteBuildPath = string.Join('/',
                addressables_output_folder,
                BuildtimeVariable(player_version_override_variable_name),
                BuildtimeVariable(build_target_variable_name));

            // The load path names its two runtime variables by CatalogVariables' constants, which are
            // deliberately not C# types: a catalog must not depend on an identifier that can move.
            remoteLoadPath = string.Join('/',
                RuntimeVariable(CatalogVariables.BaseUrlName),
                RuntimeVariable(CatalogVariables.WorldIdName),
                BuildtimeVariable(player_version_override_variable_name),
                BuildtimeVariable(build_target_variable_name));
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
                    ConfigureAddressablesSettings();
                    ConfigureProfile();
                    ConfigureAddressablesGroups();
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
        public bool IsAddressablesSettingsConfigured =>
                 settings.RemoteCatalogLoadPath.GetName(settings) == remote_load_path_variable_name &&
                 settings.RemoteCatalogBuildPath.GetName(settings) == remote_build_path_variable_name &&
                 settings.BuildRemoteCatalog &&
                 settings.EnableJsonCatalog &&
                 settings.CheckForContentUpdateRestrictionsOption
                         == CheckForContentUpdateRestrictionsOptions.ListUpdatedAssetsWithRestrictions &&
                 settings.MaxConcurrentWebRequests == 3 &&
                 settings.CatalogRequestsTimeout == 0 &&
                 !settings.IgnoreUnsupportedFilesInBuild &&
                 !settings.UniqueBundleIds &&
                 settings.ContiguousBundles &&
                 settings.NonRecursiveBuilding &&
                 settings.BuiltInBundleNaming == BuiltInBundleNaming.Custom &&
                 settings.MonoScriptBundleNaming == MonoScriptBundleNaming.Custom &&
                 !settings.DisableVisibleSubAssetRepresentations;

        private void ConfigureAddressablesSettings()
        {
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, remote_load_path_variable_name);
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, remote_build_path_variable_name);
            settings.BuildRemoteCatalog = true;
            settings.EnableJsonCatalog = true;
            settings.CheckForContentUpdateRestrictionsOption = CheckForContentUpdateRestrictionsOptions.ListUpdatedAssetsWithRestrictions;
            settings.ContentStateBuildPath = string.Empty;
            settings.MaxConcurrentWebRequests = 3;
            settings.CatalogRequestsTimeout = 0;
            settings.IgnoreUnsupportedFilesInBuild = false;
            settings.UniqueBundleIds = false;
            settings.ContiguousBundles = true;
            settings.NonRecursiveBuilding = true;
            settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            settings.DisableVisibleSubAssetRepresentations = false;
            settings.BuildRemoteCatalog = true;
            SaveAsset(settings);
        }

        private void ConfigureAddressablesSettingsForBuild()
        {
            settings.BuiltInBundleCustomNaming = settings.OverridePlayerVersion;
            settings.MonoScriptBundleCustomNaming = settings.OverridePlayerVersion;
        }

        #endregion

        #region Profiles configuration

        [CreateProperty]
        private bool IsProfileConfigured =>
            settings.profileSettings.GetValueByName(settings.activeProfileId, remote_build_path_variable_name) == remoteBuildPath
                && settings.profileSettings.GetValueByName(settings.activeProfileId, remote_load_path_variable_name) == remoteLoadPath
                && settings.profileSettings.GetValueByName(settings.activeProfileId, build_target_variable_name) == build_target_variable_value
                && settings.profileSettings.GetValueByName(settings.activeProfileId, player_version_override_variable_name) == player_version_override_variable_value;

        private void ConfigureProfile()
        {
            settings.profileSettings.SetValue(settings.activeProfileId, remote_build_path_variable_name, remoteBuildPath);
            settings.profileSettings.SetValue(settings.activeProfileId, remote_load_path_variable_name, remoteLoadPath);

            if (settings.profileSettings.GetValueByName(settings.activeProfileId, build_target_variable_name) == null)
                settings.profileSettings.CreateValue(build_target_variable_name, build_target_variable_value);
            else
                settings.profileSettings.SetValue(settings.activeProfileId, build_target_variable_name, build_target_variable_value);

            if (settings.profileSettings.GetValueByName(settings.activeProfileId, player_version_override_variable_name) == null)
                settings.profileSettings.CreateValue(player_version_override_variable_name, player_version_override_variable_value);
            else
                settings.profileSettings.SetValue(settings.activeProfileId, player_version_override_variable_name, player_version_override_variable_value);

            SaveAsset(settings);
        }

        #endregion

        #region Groups configuration

        [CreateProperty]
        private bool AreAddressablesGroupsConfigured
        {
            get
            {
                bool configured = true;
                var defaultGroup = settings.DefaultGroup;

                configured &= !settings.groups.Where(group => group != defaultGroup).Any();
                configured &= !defaultGroup.entries.Any();

                defaultGroup.Schemas.Where(schema => schema is BundledAssetGroupSchema).ToList().ForEach(schema =>
                {
                    BundledAssetGroupSchema b = schema as BundledAssetGroupSchema;
                    configured &=
                        b.LoadPath.GetName(settings) == remote_load_path_variable_name &&
                        b.BuildPath.GetName(settings) == remote_build_path_variable_name &&
                        b.Compression == BundledAssetGroupSchema.BundleCompressionMode.LZ4 &&
                        b.IncludeInBuild &&
                        !b.ForceUniqueProvider &&
                        b.UseAssetBundleCache &&
                        !b.UseAssetBundleCrc &&
                        !b.UseAssetBundleCrcForCachedBundles &&
                        !b.UseUnityWebRequestForLocalBundles &&
                        b.Timeout == 0 &&
                        !b.ChunkedTransfer &&
                        b.RedirectLimit == -1 &&
                        b.RetryCount == 0 &&
                        b.IncludeAddressInCatalog &&
                        b.IncludeGUIDInCatalog &&
                        b.IncludeLabelsInCatalog &&
                        b.InternalIdNamingMode == BundledAssetGroupSchema.AssetNamingMode.FullPath &&
                        b.InternalBundleIdMode == BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash &&
                        b.AssetBundledCacheClearBehavior == BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded &&
                        b.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately &&
                        b.BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                });

                return configured;
            }
        }

        private void ConfigureAddressablesGroups()
        {
            var defaultGroup = settings.DefaultGroup;
            var groupsToDelete = settings.groups.Where(group => group != defaultGroup).ToList();
            foreach (var group in groupsToDelete)
                settings.RemoveGroup(group);

            defaultGroup.Schemas.Where(schema => schema is BundledAssetGroupSchema).ToList().ForEach(schema =>
            {
                BundledAssetGroupSchema b = schema as BundledAssetGroupSchema;
                b.LoadPath.SetVariableByName(settings, remote_load_path_variable_name);
                b.BuildPath.SetVariableByName(settings, remote_build_path_variable_name);
                b.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                b.IncludeInBuild = true;
                b.ForceUniqueProvider = false;
                b.UseAssetBundleCache = true;
                b.UseAssetBundleCrc = false;
                b.UseAssetBundleCrcForCachedBundles = false;
                b.UseUnityWebRequestForLocalBundles = false;
                b.Timeout = 0;
                b.ChunkedTransfer = false;
                b.RedirectLimit = -1;
                b.RetryCount = 0;
                b.IncludeAddressInCatalog = true;
                b.IncludeGUIDInCatalog = true;
                b.IncludeLabelsInCatalog = true;
                b.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.FullPath;
                b.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;
                b.AssetBundledCacheClearBehavior = BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded;
                b.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                b.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                SaveAsset(b);
            });

            var entriesToRemove = defaultGroup.entries.ToList();
            foreach (var entry in entriesToRemove)
                settings.RemoveAssetEntry(entry.guid);

            SaveAsset(settings);
            SaveAsset(defaultGroup);
            SaveAsset(settings);
        }

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

            if (deployErrorsScrollView == null) return;

            var entry = new HelpBox(message, HelpBoxMessageType.Error);
            entry.style.marginBottom = 2;
            deployErrorsScrollView.Add(entry);
            deployErrorsContainer.style.display = DisplayStyle.Flex;
        }

        private void ClearDeployErrors()
        {
            deployErrorsScrollView?.Clear();
            if (deployErrorsContainer != null)
                deployErrorsContainer.style.display = DisplayStyle.None;
        }

        private string BuildtimeVariable(string variable) => "[" + variable + "]";
        private string RuntimeVariable(string variable) => "{" + variable + "}";

        private void SaveAsset(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private void EnsureFolderExists(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string currentPath = "";
            foreach (string folder in folders)
            {
                currentPath = Path.Combine(currentPath, folder);
                if (!AssetDatabase.IsValidFolder(currentPath))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
            }
        }

        #endregion

        #region Build

        /// <summary>
        /// Builds the interpreted hot-update DLL and verifies it (local whitelist + authoritative
        /// server check). Returns true if the build may proceed. Called via reflection so this
        /// window stays decoupled from the HYBRIDCLR_INSTALLED-gated assembly; when HybridCLR is
        /// not installed the setupper is absent and we simply proceed (nothing to gate).
        /// </summary>
        // A deploy interrupted by the hot-update assembly's rename, waiting for the recompilation
        // that follows it. SessionState and not EditorPrefs: this must survive a domain reload and
        // nothing else — an editor that restarts should not silently start deploying.
        private const string ResumeKey = "Virtuademy_AddressablesDeploy_ResumeAfterRecompile";
        private const string ResumeWorlds = "worlds:";
        private const string ResumeTenant = "tenant";
        private const string ResumeBuild = "build";

        /// <summary>
        /// Builds the interpreted DLL, verifies it, and builds the addressables only if it passed.
        ///
        /// Extracted from the button's own handler so it can be resumed: this path used to report
        /// EVERY refusal as "did not pass the security checks", including the one case that is not
        /// a refusal at all — the gate renaming the hot-update assembly because the scripts changed.
        /// A creator got told their code had failed a check it had not yet been put through, and had
        /// to press the button again to find out. The two Build &amp; Deploy paths already parked and
        /// resumed; this one now does the same.
        /// </summary>
        private async Task RunBuildAddressablesAsync()
        {
            if (!await BuildAndVerifyInterpretedDLLAsync())
            {
                if (InterpretedDllAwaitingRecompile())
                {
                    ParkDeployForResume(ResumeBuild);
                    return;
                }

                EditorUtility.DisplayDialog("Build blocked",
                    "The interpreted script DLL did not pass the security checks. " +
                    "See the Console for the offending lines. Addressables were NOT built.",
                    "OK");
                return;
            }

            BuildAndZipScenes();
        }

        private static void ParkDeployForResume(string payload)
        {
            SessionState.SetString(ResumeKey, payload);

            Debug.Log("[HotUpdate] The deploy is waiting for that recompilation and will carry on by " +
                      "itself when it finishes.");
        }

        /// <summary>
        /// Picks a parked deploy back up on the first reload after the recompilation that
        /// interrupted it. The marker is cleared before anything is re-run, so a deploy that fails
        /// again cannot turn into a loop, and it is only ever written by the path that renamed the
        /// assembly — a build that stopped for any other reason stays stopped.
        /// </summary>
        [DidReloadScripts]
        private static void ResumeParkedDeploy()
        {
            string parked = SessionState.GetString(ResumeKey, string.Empty);
            if (string.IsNullOrEmpty(parked))
                return;

            SessionState.EraseString(ResumeKey);

            AddressablesManagementWindow window =
                Resources.FindObjectsOfTypeAll<AddressablesManagementWindow>().FirstOrDefault();

            if (window == null)
            {
                Debug.LogWarning("[HotUpdate] The deploy was waiting for a recompilation, but its window " +
                                 "has been closed. Nothing was resumed.");
                return;
            }

            // Next editor tick, not this callback: the domain has just come back and the window's
            // UI is still being rebuilt around us.
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[HotUpdate] Recompilation finished — resuming the deploy.");

                // Fire and forget by design, exactly as the button click is: both paths log their
                // own failures, and there is nobody left to await them.
                if (parked == ResumeBuild)
                {
                    _ = window.RunBuildAddressablesAsync();
                    return;
                }

                if (parked == ResumeTenant)
                {
                    _ = window.RunTenantBuildAndDeployAsync();
                    return;
                }

                List<int> worldIds = parked.Substring(ResumeWorlds.Length)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x, out int id) ? id : -1)
                    .Where(x => x > 0)
                    .ToList();

                if (worldIds.Count == 0)
                {
                    Debug.LogWarning("[HotUpdate] The parked deploy named no world it could still reach. " +
                                     "Nothing was resumed.");
                    return;
                }

                _ = window.RunBuildAndDeployAsync(worldIds);
            };
        }

        /// <summary>
        /// Whether the verify step stopped to let Unity recompile a renamed assembly, rather than
        /// because anything was refused. It already told the creator what to do — through a dialog
        /// that survives the reload the rename triggers — so the caller must not follow it with a
        /// second, wrong explanation.
        /// </summary>
        private bool InterpretedDllAwaitingRecompile()
        {
            return FindHotUpdateSetupperType()?
                .GetProperty("AwaitingRecompile", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null) as bool? ?? false;
        }

        private async Task<bool> BuildAndVerifyInterpretedDLLAsync()
        {
            Type setupperType = FindHotUpdateSetupperType();

            var method = setupperType?.GetMethod("CompileVerifyAsync", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                // HybridCLR not installed → no interpreted-scripts pipeline to gate.
                Debug.LogWarning("[AddressablesManagement] HotUpdate verifier not found (HybridCLR not installed?). " +
                                 "Skipping interpreted DLL build/verify.");
                return true;
            }

            try
            {
                // CompileVerifyAsync returns Task<bool>, a BCL type this assembly can await directly.
                var task = (Task<bool>)method.Invoke(null, null);
                return await task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Interpreted DLL verify failed to run: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds addressables for all platforms and creates zip files.
        /// Does NOT upload — used by both "Build Addressables" button and "Build & Deploy".
        /// </summary>
        private void BuildAndZipScenes()
        {
            // ── Pre-flight: check that all required build modules are installed ──────
            var scenesWithMissingModules = sceneConfigurations.SceneConfigurations
                .Where(s => s.IncludeInBuild && s.GetMissingBuildTargets().Count > 0)
                .ToList();

            if (scenesWithMissingModules.Count > 0)
            {
                var lines = scenesWithMissingModules.Select(s =>
                {
                    var missing = s.GetMissingBuildTargets().Select(t => t switch
                    {
                        BuildTarget.Android => "Android (VR / Mobile)",
                        BuildTarget.iOS => "iOS (Mobile)",
                        BuildTarget.WebGL => "WebGL",
                        _ => t.ToString()
                    });
                    return $"• {s.SceneNameFiltered}: {string.Join(", ", missing)}";
                });

                string message =
                    "The following scenes require build modules that are not installed:\n\n" +
                    string.Join("\n", lines) +
                    "\n\nInstall the missing modules via Unity Hub → Installs → Add modules, " +
                    "then retry.";

                EditorUtility.DisplayDialog("Missing Build Modules", message, "OK");
                buildResult = EBuildError.MissingModule;
                return;
            }
            // ─────────────────────────────────────────────────────────────────────────

            // ── Clean up stale platform folders ──────────────────────────────────────
            // Delete any platform subfolder that is no longer required for a scene so
            // it does not end up in the zip and get imported on the server.
            foreach (var scene in sceneConfigurations.SceneConfigurations)
            {
                if (!scene.IncludeInBuild) continue;

                string sceneFolderPath = Path.Combine(addressables_output_folder, scene.SceneNameFiltered);
                if (!Directory.Exists(sceneFolderPath)) continue;

                var requiredFolderNames = new HashSet<string>(
                    scene.GetRequiredBuildTargets().Select(t => t.ToString()),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (string subfolder in Directory.GetDirectories(sceneFolderPath))
                {
                    string folderName = Path.GetFileName(subfolder);
                    if (!requiredFolderNames.Contains(folderName))
                    {
                        Directory.Delete(subfolder, true);
                        Debug.Log($"[AddressablesManagement] Removed stale platform folder: {subfolder}");
                    }
                }
            }
            // ─────────────────────────────────────────────────────────────────────────

            // Collect all required BuildTargets from selected scenes
            var allTargets = new HashSet<BuildTarget>();
            foreach (var scene in sceneConfigurations.SceneConfigurations)
            {
                if (scene.IncludeInBuild)
                {
                    foreach (var target in scene.GetRequiredBuildTargets())
                        allTargets.Add(target);
                }
            }

            // Build for each required platform
            foreach (BuildTarget target in allTargets)
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                BuildAddressablesForSelectedPlatform();
            }

            buildResult = CheckBuildResult();

            if (buildResult == EBuildError.None)
            {
                Debug.Log("[AddressablesManagement] Build successful for all platforms.");
                ZipBuiltScenes(PrepareInterpretedAssemblyBundle());
            }
            else
            {
                Debug.LogError("[AddressablesManagement] Build failed. Check errors above.");
            }
        }

        private void BuildAddressablesForSelectedPlatform()
        {
            AddressableAssetGroup assetGroup = settings.DefaultGroup;
            foreach (AddressableAssetEntry entry in assetGroup.entries.Where(x => x.IsScene))
                settings.RemoveAssetEntry(entry.guid);

            foreach (var scene in sceneConfigurations.SceneConfigurations)
            {
                if (scene.IncludeInBuild)
                    BuildAddressablesForTargetGroup(scene);
            }
        }

        private void BuildAddressablesForTargetGroup(SceneConfiguration configuration)
        {
            AddressableAssetGroup assetGroup = settings.DefaultGroup;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(configuration.Scene, out string guid, out long _);
            AddressableAssetEntry addressableEntry = settings.CreateOrMoveEntry(guid, assetGroup);

            string sceneNameFiltered = configuration.SceneNameFiltered;
            addressableEntry.SetAddress(sceneNameFiltered);

            settings.OverridePlayerVersion = sceneNameFiltered;
            ConfigureAddressablesSettingsForBuild();

            AddressablesBuildScript.BuildAddressables();

            settings.RemoveAssetEntry(guid);
            settings.OverridePlayerVersion = string.Empty;
        }

        private EBuildError CheckBuildResult()
        {
            foreach (var scene in sceneConfigurations.SceneConfigurations)
            {
                if (!scene.IncludeInBuild) continue;

                string sceneFolderPath = Path.Combine(addressables_output_folder, scene.SceneNameFiltered);
                var requiredTargets = scene.GetRequiredBuildTargets();

                foreach (BuildTarget target in requiredTargets)
                {
                    string subfolder = target.ToString();
                    string subfolderPath = Path.Combine(sceneFolderPath, subfolder);
                    if (!Directory.Exists(subfolderPath))
                    {
                        Debug.LogError($"Subfolder {subfolder} not found in {sceneFolderPath}");
                        return EBuildError.FolderMissing;
                    }

                    bool catalogFileExists = Directory.GetFiles(subfolderPath, "*catalog*.json").Any();
                    if (!catalogFileExists)
                    {
                        Debug.LogError($"Catalog file not found in {subfolderPath}");
                        return EBuildError.BinaryCatalog;
                    }
                }
            }

            return EBuildError.None;
        }

        /// <summary>
        /// Builds this project's interpreted-assembly bundle beside the scene zips, as
        /// <c>ServerData/&lt;AssemblyName&gt;.zip</c>, and returns the name the scenes are compiled
        /// against — null when the project has no interpreted scripts (HybridCLR not installed).
        ///
        /// Built on every addressables build, not only on a deploy from this window: the scene zips
        /// and the bundle are what someone uploads by hand and imports from the Backoffice, and
        /// the import needs the bundle beside the scenes to register their scripts.
        /// </summary>
        private static string PrepareInterpretedAssemblyBundle()
        {
            Type setupperType = FindHotUpdateSetupperType();
            Type bundleType = FindEditorType("EnvironmentDllBundle");

            if (setupperType == null || bundleType == null)
                return null;

            // Bundles of earlier builds go: a stale one beside the new scene zips is the wrong file
            // to upload, and nothing else would ever remove it. The project prefix keeps this to
            // assemblies this project produced.
            string prefix = setupperType.GetProperty("ProjectAssemblyPrefix", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null) as string;

            if (!string.IsNullOrEmpty(prefix) && Directory.Exists(addressables_output_folder))
            {
                foreach (string stale in Directory.GetFiles(addressables_output_folder, prefix + "*.zip"))
                    File.Delete(stale);
            }

            string zipPath = bundleType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)?
                .Invoke(null, null) as string;

            // The name comes from the file Build wrote, not from a second read of the assembly-name
            // property: that rescans the source on every access, and a script saved in between would
            // let the declaration name one build and the bundle carry another.
            if (!string.IsNullOrEmpty(zipPath))
                return Path.GetFileNameWithoutExtension(zipPath);

            string assemblyName = bundleType.GetProperty("AssemblyName", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null) as string;

            // The scenes still need the assembly, so they still declare it; the import will accept
            // them only where it is already registered.
            Debug.LogWarning($"[AddressablesManagement] The interpreted-assembly bundle could not be built (see above). " +
                             $"The scene zips declare '{assemblyName}', and an import accepts them only where that " +
                             "assembly is already registered.");

            return string.IsNullOrEmpty(assemblyName) ? null : assemblyName;
        }

        /// <summary>
        /// Written into every scene folder before it is zipped, so the archive itself says which
        /// interpreted assembly its scenes were compiled against. The platform's archive import
        /// reads it: a name links the catalog to that assembly, null clears the link.
        /// </summary>
        private const string EnvironmentDllDeclarationFile = "environment-dll.json";

        [Serializable]
        private class EnvironmentDllDeclaration
        {
            public string assemblyName;
        }

        /// <summary>
        /// The assembly a built scene declares. <paramref name="declares"/> is false when the
        /// folder carries no declaration — a build made before this window wrote one.
        /// </summary>
        private static string ReadDeclaredAssembly(string scene, out bool declares)
        {
            declares = false;

            string path = Path.Combine(addressables_output_folder, scene, EnvironmentDllDeclarationFile);
            if (!File.Exists(path))
                return null;

            try
            {
                EnvironmentDllDeclaration declaration = JsonConvert.DeserializeObject<EnvironmentDllDeclaration>(File.ReadAllText(path));
                declares = declaration != null;
                return string.IsNullOrEmpty(declaration?.assemblyName) ? null : declaration.assemblyName;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesManagement] Unreadable {path}: {ex.Message}. Build the scenes again.");
                return null;
            }
        }

        private void ZipBuiltScenes(string assemblyName)
        {
            List<string> builtScenes = GetBuiltSceneNames();
            string declaration = JsonConvert.SerializeObject(new EnvironmentDllDeclaration { assemblyName = assemblyName });

            foreach (string scene in builtScenes)
            {
                string fullBuildPath = Path.Combine(addressables_output_folder, scene);
                string fullZipPath = fullBuildPath + ".zip";

                if (!Directory.Exists(fullBuildPath))
                    throw new DirectoryNotFoundException($"Build folder not found: {fullBuildPath}");

                File.WriteAllText(Path.Combine(fullBuildPath, EnvironmentDllDeclarationFile), declaration);

                // Remove existing zip with the same name
                if (File.Exists(fullZipPath))
                {
                    File.Delete(fullZipPath);
                    Debug.Log($"[AddressablesManagement] Removed existing zip: {fullZipPath}");
                }

                ZipFile.CreateFromDirectory(fullBuildPath, fullZipPath, System.IO.Compression.CompressionLevel.Optimal, true);
                Debug.Log($"[AddressablesManagement] Created zip: {fullZipPath}");
            }
        }

        #endregion
    }
}
