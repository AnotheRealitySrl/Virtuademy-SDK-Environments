using Newtonsoft.Json;
using Virtuademy.SDK.TenantConfiguration.Editor;
using Virtuademy.SDK.TenantConfiguration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            BuildFailed,
            InvalidSceneNames,
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

        // The deploy targets: one group per connection the project uses (meta-repo ADR 0037), each with its worlds and its
        // tenant level. A group's key is fixed when it is built, and a deploy to its targets goes with that connection's
        // token and to that connection's hosts only, whatever the default connection becomes meanwhile.
        private readonly Dictionary<string, TargetGroup> groups = new(StringComparer.OrdinalIgnoreCase);

        // Which groups were opened or closed in this window; a group not in it starts open, a Production one closed.
        private readonly Dictionary<string, bool> groupExpanded = new(StringComparer.OrdinalIgnoreCase);

        // What the groups were built from: the connections the project uses, whether each has a session, and the default one.
        private string groupsSignature;

        // The registry entry the project was set up from, read when the groups are built.
        private string projectVersion;

        private bool deployRunning;
        private bool rebuildPending;
        private bool missingModules;

        // UI references
        private Label loginStatusLabel;
        private IVisualElementScheduledItem sessionExpiryRefresh;
        private VisualElement deploySection;
        private VisualElement targetsContainer;
        private VisualElement versionNotice;
        private Button deployButton;
        private Button buildAndDeployButton;
        private Button openTenantSelectionButton;
        private Button buildAddressablesButton;

        // Platform module warnings
        private VisualElement platformWarningsContainer;
        private SerializedObject sceneSerializedObject;
        private VisualElement sceneListContainer;
        private TextField sceneSearchField;
        private Toggle onlyInBuildToggle;
        private DropdownField publicationFilter;
        private Label sceneCountLabel;

        // Deploy error log
        private VisualElement deployErrorsContainer;
        private ScrollView deployErrorsScrollView;

        /// <summary>One connection the project uses, as the window shows it: a foldout with its worlds and its tenant level.</summary>
        private sealed class TargetGroup
        {
            public string Key;
            public Foldout Foldout;
            public Label Status;
            public Button LoginButton;
            public Button LogoutButton;
            public Button BackofficeButton;
            public Label WorldsStatus;
            public VisualElement WorldsList;
            public List<PublishableWorld> Deployable = new();
            public readonly Dictionary<int, Toggle> WorldToggles = new();
            public readonly Dictionary<int, VisualElement> WorldPreviews = new();
            public Toggle TenantToggle;
            public VisualElement TenantPreview;
            public bool Production;
            public PlatformVersionRule.Verdict Version;

            /// <summary>What the account may open in this tenant's Back Office; null shows no Back Office button.</summary>
            public BackofficeAccess Backoffice;

            /// <summary>The worlds were listed for this group, so its ticks can be shown and deployed to.</summary>
            public bool Listed;
            public bool Listing;

            /// <summary>Bumped when the group is dropped: a listing that started before drops its result.</summary>
            public int Generation;

            public bool Allowed => PlatformVersionRule.Allows(Version);
        }

        /// <summary>A group's ticked targets, as one deploy takes them.</summary>
        private sealed class GroupTargets
        {
            public TargetGroup Group;
            public List<int> Worlds;
            public bool Tenant;
            public int Count => Worlds.Count + (Tenant ? 1 : 0);
        }

        /// <summary>The answer of <c>GET /tenants/backoffice-access/my</c> (meta-repo ADR 0038, decision 7).</summary>
        private sealed class BackofficeAccess
        {
            [JsonProperty("canAccess")] public bool CanAccess;
            [JsonProperty("tenantLevel")] public bool TenantLevel;
            [JsonProperty("worldIds")] public List<int> WorldIds;

            public bool Covers(int worldId) => TenantLevel || (WorldIds?.Contains(worldId) ?? false);
        }

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

            // A connection logged in or out by another process (the command line, another editor) has its group built
            // again. Otherwise only the labels, which age on their own (a token expires while the window sits idle):
            // reloading the worlds is a network round trip per focus change, and nothing about them changed.
            if (loginStatusLabel == null)
                return;

            if (GroupsSignature() != groupsSignature)
                RebuildTargets();
            else
                RefreshLabels();
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
            SetupDeployTargetsUI();

            EditorLoginState.OnLoginStateChanged += OnLoginStateChanged;
            EditorLoginState.OnTokenChanged += OnTokenChanged;
        }

        #region Deploy targets

        private void SetupDeployTargetsUI()
        {
            loginStatusLabel = root.Q<Label>("login-status-label");
            deploySection = root.Q<VisualElement>("deploy-section");
            targetsContainer = root.Q<VisualElement>("deploy-targets");
            deployButton = root.Q<Button>("deploy-button");
            buildAndDeployButton = root.Q<Button>("build-and-deploy-button");

            // The window's own login is gone: connections are logged in per group below, and imported, chosen for play
            // mode and logged out in Available Tenants.
            openTenantSelectionButton = root.Q<Button>("open-tenant-selection-button");
            openTenantSelectionButton.text = "Available Tenants";
            openTenantSelectionButton.tooltip = "Import a configuration file, log in, and choose the project's default connection.";
            openTenantSelectionButton.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Virtuademy/Available Tenants");
            };

            versionNotice = new VisualElement();
            deploySection.Insert(deploySection.IndexOf(targetsContainer), versionNotice);

            deployButton.clicked += OnDeployClicked;
            buildAndDeployButton.clicked += OnBuildAndDeployClicked;

            InitDeployErrorsContainer();

            deploySection.style.display = DisplayStyle.Flex;
            RebuildTargets();
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

        /// <summary>
        /// A login or a logout on any connection, or another default connection. The errors in the panel belong to the
        /// connections as they were, so they go; the groups are built again from the project's connections.
        /// </summary>
        private void OnLoginStateChanged()
        {
            if (!deployRunning)
                ClearDeployErrors();

            // Where the scenes are published belongs to the session that read it: a connection logged out forgets it,
            // and with it the filter choices it fed.
            foreach (string key in ProjectConnections.Used.Where(k => ConnectionStore.Get(k)?.HasSession != true))
                PublishedEnvironmentsIndex.Clear(key);

            RebuildTargets();
        }

        // A renewal or a server rejection changes only a token: re-render the labels, never the worlds — this runs
        // inside the API call that renewed it.
        private void OnTokenChanged() => RefreshLabels();

        /// <summary>The connections the project uses that are in the store, in the order the project first used them.</summary>
        private static List<ConnectionRecord> UsedConnections() =>
            ProjectConnections.Used.Select(ConnectionStore.Get).Where(r => r != null).ToList();

        private static string GroupsSignature() =>
            string.Join("|", UsedConnections().Select(r => $"{r.Key}#{r.HasSession}")) + "|play:" + ProjectConnections.DefaultKey;

        /// <summary>
        /// Builds one group per connection the project uses. A group remembers nothing of its own: the ticks are read back
        /// from the project's settings (<see cref="ProjectConnections.TargetsOf"/>) once its worlds are listed. Deferred
        /// while a deploy runs, which holds on to the groups it was started with.
        /// </summary>
        private void RebuildTargets()
        {
            if (targetsContainer == null)
                return;

            if (deployRunning)
            {
                rebuildPending = true;
                return;
            }

            foreach (TargetGroup old in groups.Values)
                old.Generation++;
            groups.Clear();
            targetsContainer.Clear();
            groupsSignature = GroupsSignature();

            projectVersion = PlatformVersionRule.ProjectVersion;
            RefreshVersionNotice(projectVersion);

            List<ConnectionRecord> connections = UsedConnections();
            if (connections.Count == 0)
            {
                targetsContainer.Add(new Label("This project uses no connection yet. Import the configuration file of a tenant and log in from " +
                                               "Virtuademy > Available Tenants: each connection then gets its group here.")
                {
                    style = { whiteSpace = WhiteSpace.Normal, marginLeft = 4, marginRight = 4, marginBottom = 4 }
                });
            }

            foreach (ConnectionRecord record in connections)
            {
                TargetGroup group = BuildGroup(record, projectVersion);
                groups[record.Key] = group;
                targetsContainer.Add(group.Foldout);

                if (group.Foldout.value)
                    LoadGroupWorlds(group);
            }

            RefreshLabels();
            RefreshDeployPreviews();
            RefreshButtons();
        }

        /// <summary>Says once, above the groups, when the project has no platform version the tenants can be compared with.</summary>
        private void RefreshVersionNotice(string projectVersion)
        {
            versionNotice.Clear();
            if (PlatformVersionRule.Check(projectVersion, null) != PlatformVersionRule.Verdict.ProjectUnknown)
                return;

            string text = projectVersion == null
                ? "This project has no setup configuration, so the platform version of the tenants is not checked before a deploy."
                : $"This project was set up from '{projectVersion}', which is not a platform version, so the platform version of the " +
                  "tenants is not checked before a deploy.";
            versionNotice.Add(new HelpBox(text, HelpBoxMessageType.Info) { style = { marginBottom = 4 } });
        }

        private static bool IsProduction(ConnectionRecord record) =>
            string.Equals(record.Environment, nameof(Env.Production), StringComparison.OrdinalIgnoreCase)
            || record.Tenant?.Env == Env.Production;

        private TargetGroup BuildGroup(ConnectionRecord record, string projectVersion)
        {
            TargetGroup group = new()
            {
                Key = record.Key,
                Production = IsProduction(record),
                Version = PlatformVersionRule.Check(projectVersion, record.PlatformVersion),
            };

            bool expanded = groupExpanded.TryGetValue(record.Key, out bool open) ? open : !group.Production;
            group.Foldout = new Foldout { value = expanded, style = { marginBottom = 4 } };

            // The ticks inside raise change events too, and they bubble up here: only the foldout's own counts.
            group.Foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != group.Foldout)
                    return;

                groupExpanded[group.Key] = evt.newValue;
                if (evt.newValue && !group.Listed && !group.Listing)
                    LoadGroupWorlds(group);
            });

            VisualElement statusRow = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            group.Status = new Label { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1 } };
            statusRow.Add(group.Status);

            group.LoginButton = new Button(() => LoginGroup(group)) { text = "Log in", style = { flexShrink = 0 } };
            group.LogoutButton = new Button(async () => await EditorSessionManager.LogoutAsync(group.Key)) { text = "Log out", style = { flexShrink = 0 } };
            TintRed(group.LogoutButton);

            // Shown once the group's listing has asked whether the account may enter (LoadGroupBackofficeAccessAsync).
            group.BackofficeButton = new Button(() => OpenBackoffice(group))
            {
                text = "Back Office ↗",
                style = { flexShrink = 0, display = DisplayStyle.None },
            };

            statusRow.Add(group.BackofficeButton);
            statusRow.Add(group.LoginButton);
            statusRow.Add(group.LogoutButton);
            group.Foldout.Add(statusRow);

            string refusal = PlatformVersionRule.Refusal(group.Version, projectVersion, record.PlatformVersion);
            if (refusal != null)
                group.Foldout.Add(new HelpBox(refusal, HelpBoxMessageType.Error) { style = { marginBottom = 4 } });

            if (group.Production)
                group.Foldout.Add(new HelpBox("Production. A deploy that includes this tenant asks for a confirmation of its own.",
                                              HelpBoxMessageType.Warning) { style = { marginBottom = 4 } });

            group.WorldsStatus = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 2, opacity = 0.85f } };
            group.Foldout.Add(group.WorldsStatus);
            group.WorldsList = new VisualElement();
            group.Foldout.Add(group.WorldsList);

            if (!expanded)
                group.WorldsStatus.text = "Open the group to list its worlds.";

            return group;
        }

        /// <summary>
        /// A red button for an action that ends something (a logout). An inline background replaces the theme's hover
        /// colour, so the hover is lightened here.
        /// </summary>
        private static void TintRed(Button button)
        {
            Color normal = new(0.62f, 0.2f, 0.2f);
            Color hover = new(0.74f, 0.27f, 0.27f);
            button.style.backgroundColor = normal;
            button.style.color = Color.white;
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = hover);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = normal);
        }

        /// <summary>The browser login of one group's connection. It does not change the default connection.</summary>
        private async void LoginGroup(TargetGroup group)
        {
            group.LoginButton.SetEnabled(false);
            try
            {
                LoginOutcome outcome = await ConnectionLoginService.LoginAsync(group.Key, new LoginOptions
                {
                    OnStatus = status => { if (group.Status != null) group.Status.text = status; },
                });

                if (!outcome.Success)
                    LogDeployError($"{PublishedEnvironmentsIndex.LabelOf(group.Key)}: the login failed. {outcome.Message}");
            }
            finally
            {
                group.LoginButton?.SetEnabled(true);
            }
        }

        /// <summary>
        /// Lists the worlds of one group with silent renewal only: this runs from opening the window or a group, and
        /// popping a browser login at someone who only looked would be startling. Deploy actions do allow it.
        /// </summary>
        private async void LoadGroupWorlds(TargetGroup group)
        {
            group.WorldsList.Clear();
            group.WorldToggles.Clear();
            group.WorldPreviews.Clear();
            group.TenantToggle = null;
            group.TenantPreview = null;
            group.Listed = false;
            group.WorldsStatus.style.display = DisplayStyle.Flex;

            ConnectionRecord record = ConnectionStore.Get(group.Key);
            if (record?.HasSession != true)
            {
                group.WorldsStatus.text = "Log in to list the worlds of this tenant.";
                RefreshButtons();
                return;
            }

            int generation = group.Generation;
            group.Listing = true;
            group.WorldsStatus.text = "Loading worlds...";

            try
            {
                // The platform version can change at any time (an upgrade of the tenant): read it again before the ticks
                // are offered, and rebuild the group when the version rule's verdict moved.
                if (await RefreshVersionAsync(group.Key) != group.Version)
                {
                    if (generation == group.Generation)
                    {
                        group.Listing = false;
                        RebuildTargets();
                    }

                    return;
                }

                if (generation != group.Generation)
                    return;

                // Beside the listing, not in its way: the access decides only the Back Office button.
                _ = LoadGroupBackofficeAccessAsync(group, generation);

                WorldListResult listing = await EnvironmentsDeploy.LoadWorldsAsync(
                    connectionKey: group.Key,
                    progress: text => { if (generation == group.Generation) group.WorldsStatus.text = text; },
                    // Where every scene is already published, shown under each scene by SceneConfigurationDrawer:
                    // computed on ALL the worlds the account can see, before the deploy-role filter.
                    onListed: visible => _ = PublishedEnvironmentsIndex.RefreshAsync(visible.Select(w => (w.Id, w.Label)), group.Key));

                if (generation != group.Generation)
                    return;

                if (listing.Status != WorldListStatus.Ok)
                {
                    group.WorldsStatus.text = listing.Message;
                    return;
                }

                group.Deployable = listing.Deployable;
                group.Listed = true;
                FillGroupTargets(group, ConnectionStore.Get(group.Key) ?? record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error loading the worlds of {PublishedEnvironmentsIndex.LabelOf(group.Key)}: {ex.Message}");
                if (generation == group.Generation)
                    group.WorldsStatus.text = $"Error: {ex.Message}";
            }
            finally
            {
                if (generation == group.Generation)
                {
                    group.Listing = false;
                    RefreshButtons();
                }
            }
        }

        /// <summary>One tick per world the account can deploy to, and the tenant level for a TenantManager, as the project last left them.</summary>
        private void FillGroupTargets(TargetGroup group, ConnectionRecord record)
        {
            IReadOnlyList<ProjectConnections.DeployTarget> saved = ProjectConnections.TargetsOf(group.Key);
            HashSet<int> savedWorlds = new(saved.Where(t => !t.TenantLevel).Select(t => t.WorldId.Value));
            bool savedTenant = saved.Any(t => t.TenantLevel);

            group.WorldsStatus.style.display = group.Deployable.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (group.Deployable.Count == 0)
                group.WorldsStatus.text = "Your account cannot deploy to any world of this tenant.";

            foreach (PublishableWorld world in group.Deployable)
            {
                // A group the version rule refuses shows its worlds unticked and locked; the ticks the project kept for it
                // are left alone and come back if the versions agree again.
                Toggle toggle = new() { text = $"{world.Label} (ID: {world.Id})", value = group.Allowed && savedWorlds.Contains(world.Id) };
                toggle.SetEnabled(group.Allowed);
                toggle.RegisterValueChangedCallback(evt => OnTargetTicked(group, evt));

                VisualElement preview = NewDeployPreview();
                group.WorldToggles[world.Id] = toggle;
                group.WorldPreviews[world.Id] = preview;
                group.WorldsList.Add(toggle);
                group.WorldsList.Add(preview);
            }

            if (record.IsTenantManager)
            {
                group.TenantToggle = new Toggle
                {
                    text = "Tenant level",
                    tooltip = "Every world of the tenant. The interpreted scripts are not uploaded: an environment deployed at tenant level runs without them.",
                    value = group.Allowed && savedTenant,
                    style = { marginTop = 2 },
                };
                group.TenantToggle.SetEnabled(group.Allowed);
                group.TenantToggle.RegisterValueChangedCallback(evt => OnTargetTicked(group, evt));
                group.TenantPreview = NewDeployPreview();
                group.WorldsList.Add(group.TenantToggle);
                group.WorldsList.Add(group.TenantPreview);
            }

            RefreshDeployPreviews();

            // The access can arrive before the worlds: what the button opens depends on the ticks just made.
            RefreshBackofficeButton(group);
        }

        /// <summary>
        /// The connection's tenant read again over HMAC (<see cref="ConnectionLoginService.RefreshConnectionAsync"/>), and the
        /// version rule's verdict on it. When the platform cannot be read, the verdict on what the store last had.
        /// </summary>
        private async Task<PlatformVersionRule.Verdict> RefreshVersionAsync(string connectionKey)
        {
            await ConnectionLoginService.RefreshConnectionAsync(connectionKey);
            return PlatformVersionRule.Check(projectVersion, ConnectionStore.Get(connectionKey)?.PlatformVersion);
        }

        /// <summary>
        /// Right before a deploy: every target connection's platform version read again. A target the rule no longer
        /// allows stops the deploy (nothing is built or sent), with a dialog that names it, and the groups are built again.
        /// </summary>
        private async Task<bool> RecheckVersionsAsync(List<GroupTargets> targets)
        {
            List<string> refused = new();
            foreach (GroupTargets target in targets)
            {
                if (!PlatformVersionRule.Allows(await RefreshVersionAsync(target.Group.Key)))
                    refused.Add(PublishedEnvironmentsIndex.LabelOf(target.Group.Key));
            }

            if (refused.Count == 0)
                return true;

            string message = $"The platform version of {string.Join(", ", refused)} changed and no longer matches the project's. Nothing was built or sent.";
            LogDeployError(message);
            EditorUtility.DisplayDialog("Deploy blocked", message + " The groups were read again: check the ticks.", "OK");
            RebuildTargets();
            return false;
        }

        private void OnTargetTicked(TargetGroup group, ChangeEvent<bool> evt)
        {
            evt.StopPropagation();
            ProjectConnections.SetTargets(group.Key,
                group.WorldToggles.Where(t => t.Value.value).Select(t => t.Key),
                group.TenantToggle?.value ?? false);
            RefreshDeployPreviews();
            RefreshButtons();
            RefreshBackofficeButton(group);
        }

        #region Back Office

        /// <summary>
        /// Asks the group's Application API whether the account may open the tenant's Back Office (meta-repo ADR 0038,
        /// decision 7), with silent renewal only, and shows or hides the group's button from the answer.
        /// </summary>
        private async Task LoadGroupBackofficeAccessAsync(TargetGroup group, int generation)
        {
            BackofficeAccess access = await LoadBackofficeAccessAsync(group.Key);
            if (generation != group.Generation)
                return;

            group.Backoffice = access;
            RefreshBackofficeButton(group);
        }

        /// <summary>
        /// The access when the account may enter, else null. The token goes to this connection's Application API only.
        /// A tenant with no Back Office address is not asked. An API older than the route answers 404 or 405, and is
        /// skipped without a word (the version rule of meta-repo ADR 0037); any other failure is logged and shows no
        /// button either.
        /// </summary>
        private static async Task<BackofficeAccess> LoadBackofficeAccessAsync(string connectionKey)
        {
            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(applicationApiUrl) || BackofficeUriOf(connectionKey) == null)
                return null;

            string url = $"{applicationApiUrl}/tenants/backoffice-access/my?api-version=2";
            try
            {
                using HttpResponseMessage response = await EditorSessionManager.SendAuthorizedAsync(connectionKey,
                    () => new HttpRequestMessage(HttpMethod.Get, url), allowInteractive: false);

                if (response == null || response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
                    return null;

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogWarning($"[AddressablesManagement] Could not read the Back Office access of {PublishedEnvironmentsIndex.LabelOf(connectionKey)}: {response.StatusCode}");
                    return null;
                }

                BackofficeAccess access = JsonConvert.DeserializeObject<BackofficeAccess>(await response.Content.ReadAsStringAsync());
                return access?.CanAccess == true ? access : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesManagement] Could not read the Back Office access of {PublishedEnvironmentsIndex.LabelOf(connectionKey)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>The connection's Back Office address, when it is an absolute http(s) URL.</summary>
        private static Uri BackofficeUriOf(string connectionKey)
        {
            string value = ConnectionStore.Get(connectionKey)?.BackofficeUrl;
            return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri
                : null;
        }

        /// <summary>
        /// The world the button opens the Back Office on: the first ticked world, in the group's order, that the access
        /// covers. Null opens the home page: no world is ticked (the tenant level is not a world), or none is covered.
        /// </summary>
        private static PublishableWorld BackofficeWorld(TargetGroup group) => group.Deployable
            .FirstOrDefault(w => group.WorldToggles.TryGetValue(w.Id, out Toggle toggle) && toggle.value && group.Backoffice.Covers(w.Id));

        private static string BackofficeLink(Uri home, int? worldId)
        {
            if (worldId == null)
                return home.AbsoluteUri;

            UriBuilder builder = new(home);
            string query = builder.Query.TrimStart('?');
            builder.Query = (query.Length > 0 ? query + "&" : "") + "world=" + worldId.Value.ToString(CultureInfo.InvariantCulture);
            return builder.Uri.AbsoluteUri;
        }

        /// <summary>Shown while the group's connection has a session, a Back Office address and an access that says yes.</summary>
        private void RefreshBackofficeButton(TargetGroup group)
        {
            if (group.BackofficeButton == null)
                return;

            Uri home = BackofficeUriOf(group.Key);
            bool show = home != null && group.Backoffice != null && ConnectionStore.Get(group.Key)?.HasSession == true;
            group.BackofficeButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            PublishableWorld world = BackofficeWorld(group);
            group.BackofficeButton.tooltip = world == null
                ? "Opens the Back Office of this tenant in the browser, on its home page. Tick a world to open it on that world."
                : $"Opens the Back Office of this tenant in the browser, on the first ticked world: {world.Label} (ID: {world.Id}).";
        }

        private void OpenBackoffice(TargetGroup group)
        {
            Uri home = BackofficeUriOf(group.Key);
            if (home != null && group.Backoffice != null)
                Application.OpenURL(BackofficeLink(home, BackofficeWorld(group)?.Id));
        }

        #endregion

        /// <summary>The ticked targets of every group that can be deployed to: listed, and allowed by the version rule.</summary>
        private List<GroupTargets> TickedTargets() => groups.Values
            .Where(g => g.Allowed && g.Listed)
            .Select(g => new GroupTargets
            {
                Group = g,
                Worlds = g.WorldToggles.Where(t => t.Value.value).Select(t => t.Key).ToList(),
                Tenant = g.TenantToggle?.value ?? false,
            })
            .Where(t => t.Count > 0)
            .ToList();

        /// <summary>The banner (the default connection), each group's title and status, and the session-expiry refresh.</summary>
        private void RefreshLabels()
        {
            if (loginStatusLabel == null)
                return;

            ConnectionRecord defaultConnection = ConnectionStore.Get(ProjectConnections.DefaultKey);
            if (defaultConnection == null)
            {
                loginStatusLabel.text = "No default connection: choose one in Available Tenants.";
                loginStatusLabel.style.color = new Color(0.8f, 0.4f, 0.2f);
            }
            else
            {
                string session = defaultConnection.HasSession
                    ? $"logged in{(string.IsNullOrEmpty(defaultConnection.Username) ? "" : $" as {defaultConnection.Username}")}{EditorSessionManager.DescribeSession(defaultConnection, out _)}"
                    : "not logged in";
                loginStatusLabel.text = $"Default connection: {PublishedEnvironmentsIndex.LabelOf(defaultConnection.Key)}, {session}";
                loginStatusLabel.style.color = !defaultConnection.HasSession
                    ? new Color(0.8f, 0.4f, 0.2f)
                    : defaultConnection.IsTokenValid ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.7f, 0.2f);
            }

            foreach (TargetGroup group in groups.Values)
            {
                ConnectionRecord record = ConnectionStore.Get(group.Key);
                if (record == null)
                    continue;

                string state = !record.HasSession ? "not logged in" : record.IsTokenValid ? "logged in" : "session expired";
                bool isDefault = string.Equals(record.Key, defaultConnection?.Key, StringComparison.OrdinalIgnoreCase);
                group.Foldout.text = $"{record.TenantLabel} · {record.Environment ?? "?"} · platform {record.PlatformVersion ?? "unknown"} · {state}" +
                                     (isDefault ? " · default" : "");

                if (record.HasSession)
                {
                    string user = string.IsNullOrEmpty(record.Username) ? "" : $" as {record.Username}";
                    string role = record.IsTenantManager ? " [TenantManager]" : "";
                    group.Status.text = $"Logged in{user}{role}{EditorSessionManager.DescribeSession(record, out bool tokenValid)}";
                    group.Status.style.color = tokenValid ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.7f, 0.2f);
                }
                else
                {
                    group.Status.text = "Not logged in.";
                    group.Status.style.color = new Color(0.8f, 0.4f, 0.2f);
                }

                group.LoginButton.style.display = record.HasSession ? DisplayStyle.None : DisplayStyle.Flex;
                group.LogoutButton.style.display = record.HasSession ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshBackofficeButton(group);
            }

            ScheduleSessionExpiryRefresh();
        }

        /// <summary>
        /// The labels go stale on their own when a token expires while the window sits idle: re-render them once, right
        /// after the first such moment among the project's connections. Re-armed on every render.
        /// </summary>
        private void ScheduleSessionExpiryRefresh()
        {
            sessionExpiryRefresh?.Pause();
            sessionExpiryRefresh = null;

            DateTime? next = UsedConnections()
                .Where(r => r.IsTokenValid && r.UsableUntilUtc != null)
                .Select(r => r.UsableUntilUtc)
                .OrderBy(t => t)
                .FirstOrDefault();
            if (next == null)
                return;

            // One second past the moment, so IsTokenValid has already flipped when it runs.
            long delayMs = (long)Math.Max(0, (next.Value - DateTime.UtcNow).TotalMilliseconds) + 1000;
            sessionExpiryRefresh = loginStatusLabel.schedule
                .Execute(RefreshLabels)
                .StartingIn(delayMs);
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

            List<GroupTargets> targets = TickedTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Deploy", "Tick at least one world, or the tenant level, in the group of a connection.", "OK");
                return;
            }

            List<string> scenes = GetBuiltSceneNames();
            if (scenes.Count == 0)
            {
                Debug.LogWarning("[AddressablesManagement] No scenes marked for build.");
                return;
            }

            // Check that zip files exist
            List<string> missingZips = scenes.Where(scene => !File.Exists(Path.Combine(addressables_output_folder, scene + ".zip"))).ToList();
            if (missingZips.Count > 0)
            {
                string missing = string.Join(", ", missingZips);
                LogDeployError($"Missing zip files for: {missing}. Build first.");
                EditorUtility.DisplayDialog("Deploy", $"Missing zip files for:\n{missing}\n\nPlease build first.", "OK");
                return;
            }

            if (!ConfirmTargets("Deploy", targets) || !await RecheckVersionsAsync(targets))
                return;

            SetDeployRunning(true);
            try
            {
                await DeployToTargetsAsync("Deploy", targets, scenes);
            }
            finally
            {
                SetDeployRunning(false);
            }
        }

        private async void OnBuildAndDeployClicked()
        {
            ClearDeployErrors();

            List<GroupTargets> targets = TickedTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Build & Deploy", "Tick at least one world, or the tenant level, in the group of a connection.", "OK");
                return;
            }

            // Asked before the build, which takes minutes: nobody should come back to a question.
            if (!ConfirmTargets("Build & Deploy", targets) || !await RecheckVersionsAsync(targets))
                return;

            await LockedReloadAsync(() => BuildAndDeployAsync(targets));
        }

        /// <summary>
        /// Runs <paramref name="flow"/> with assembly reloads locked, and always unlocks afterwards: on every
        /// early return and on an exception too, because a lock left behind stops the editor from ever
        /// compiling a script again until it is restarted.
        /// </summary>
        /// <remarks>
        /// The build switches the build target, which queues a recompilation and a domain reload, and a reload while the
        /// code awaits the network would end it silently.
        /// </remarks>
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

        /// <summary>
        /// The interpreted scripts are verified on every target connection's platform before anything is built (the
        /// script policy lives per Platform DB), the scenes are built once, and each target is then deployed with its own
        /// connection's token.
        /// </summary>
        private async Task BuildAndDeployAsync(List<GroupTargets> targets)
        {
            SetDeployRunning(true);

            // The buttons come back on every way out, an exception included.
            try
            {
                if (!RequireAddressablesConfigured("Build & Deploy"))
                    return;

                foreach (string key in targets.Select(t => t.Group.Key).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!await EnvironmentsBuild.VerifyInterpretedScriptsAsync(key))
                    {
                        string label = PublishedEnvironmentsIndex.LabelOf(key);
                        LogDeployError($"{label}: the interpreted script DLL failed the security checks. Build & deploy aborted (see Console).");
                        EditorUtility.DisplayDialog("Build & Deploy blocked",
                            $"The interpreted script DLL did not pass the security checks of {label}. See the Console. " +
                            "Nothing was built or deployed.", "OK");
                        return;
                    }
                }

                BuildAndZipScenes();

                if (buildResult != EBuildError.None)
                {
                    LogDeployError($"Build failed ({buildResult}). Aborting deploy.");
                    return;
                }

                await DeployToTargetsAsync("Build & Deploy", targets, GetBuiltSceneNames());
            }
            finally
            {
                SetDeployRunning(false);
            }
        }

        /// <summary>
        /// Before a deploy to more than one target: the targets per tenant and environment, and a confirmation. Then, when
        /// any of them is in a Production environment, a confirmation for those alone. False when either is declined.
        /// </summary>
        private static bool ConfirmTargets(string what, List<GroupTargets> targets)
        {
            string Describe(GroupTargets t)
            {
                List<string> places = t.Worlds
                    .Select(id => $"{t.Group.Deployable.FirstOrDefault(w => w.Id == id)?.Label ?? "world"} ({id})")
                    .ToList();
                if (t.Tenant)
                    places.Add("tenant level");
                return $"{PublishedEnvironmentsIndex.LabelOf(t.Group.Key)}: {string.Join(", ", places)}";
            }

            int count = targets.Sum(t => t.Count);
            if (count > 1 && !EditorUtility.DisplayDialog($"{what} to {count} targets",
                    "The scenes included in the build go to:\n\n" + string.Join("\n", targets.Select(Describe)) +
                    "\n\nEach tenant is deployed with its own connection's login.", what, "Cancel"))
                return false;

            List<GroupTargets> production = targets.Where(t => t.Group.Production).ToList();
            if (production.Count > 0 && !EditorUtility.DisplayDialog("Deploy to production",
                    "These targets are in a Production environment:\n\n" + string.Join("\n", production.Select(Describe)) +
                    "\n\nDeploy there?", "Deploy to production", "Cancel"))
                return false;

            return true;
        }

        /// <summary>Each group's worlds, then its tenant level, with that group's connection. Problems go to the panel prefixed with the tenant.</summary>
        private async Task DeployToTargetsAsync(string what, List<GroupTargets> targets, List<string> scenes)
        {
            int errorsBefore = deployErrorCount;
            int importsStarted = 0;
            int expected = 0;

            foreach (GroupTargets target in targets)
            {
                DeployObserver observer = NewDeployObserver(PublishedEnvironmentsIndex.LabelOf(target.Group.Key));

                if (target.Worlds.Count > 0)
                {
                    DeployReport report = await EnvironmentsDeploy.DeployToWorldsAsync(target.Worlds, target.Group.Deployable, scenes, observer, target.Group.Key);
                    importsStarted += report.ImportsStarted;
                    expected += Math.Max(report.Expected, target.Worlds.Count * scenes.Count);
                }

                if (target.Tenant)
                {
                    DeployReport report = await EnvironmentsDeploy.DeployToTenantAsync(scenes, observer, target.Group.Key);
                    importsStarted += report.ImportsStarted;
                    expected += Math.Max(report.Expected, scenes.Count);
                }
            }

            ReportDeployOutcome(what, errorsBefore, importsStarted, expected);
        }

        /// <summary>Errors go to the deploy panel and the Console, prefixed with the tenant; progress to the editor's progress bar.</summary>
        private DeployObserver NewDeployObserver(string tenant) => new()
        {
            Error = message => LogDeployError($"{tenant}: {message}"),
            Progress = (title, info, fraction) => EditorUtility.DisplayProgressBar($"{title} ({tenant})", info, fraction),
            ProgressDone = EditorUtility.ClearProgressBar,
        };

        /// <summary>While a deploy runs the groups are frozen: no ticking, and a rebuild waits for the end.</summary>
        private void SetDeployRunning(bool running)
        {
            deployRunning = running;
            targetsContainer?.SetEnabled(!running);
            RefreshButtons();

            if (!running && rebuildPending)
            {
                rebuildPending = false;
                RebuildTargets();
            }
        }

        /// <summary>
        /// The build buttons wait for the build modules the included scenes need; the deploy buttons also for a ticked
        /// target; none works while a deploy runs.
        /// </summary>
        private void RefreshButtons()
        {
            bool hasTargets = TickedTargets().Count > 0;
            const string missingModulesTooltip =
                "Disabled: one or more selected scenes require build modules that are not installed. " +
                "See the warnings above.";
            const string noTargetTooltip = "Tick a world, or the tenant level, in the group of a connection first.";

            if (buildAddressablesButton != null)
            {
                buildAddressablesButton.SetEnabled(!missingModules && !deployRunning);
                buildAddressablesButton.tooltip = missingModules ? missingModulesTooltip : string.Empty;
            }

            if (deployButton != null)
            {
                deployButton.SetEnabled(hasTargets && !deployRunning);
                deployButton.tooltip = hasTargets ? "Upload the built zips of the included scenes to every ticked target." : noTargetTooltip;
            }

            if (buildAndDeployButton != null)
            {
                buildAndDeployButton.SetEnabled(hasTargets && !missingModules && !deployRunning);
                buildAndDeployButton.tooltip = missingModules ? missingModulesTooltip
                    : hasTargets ? "Build the included scenes once, then verify and upload them to every ticked target with its own connection's login."
                    : noTargetTooltip;
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
        /// All / Published / Not published, then, per connection whose index has data, Tenant level when readable and one
        /// entry per world the index knows about (every world the account can see, not only the deployable ones). With
        /// more than one connection each place carries its tenant. Each place carries, in brackets, how many of the
        /// project's scenes are published there — the number of rows the filter would show. Keeps the current choice when
        /// it still exists (by place, since its text changes with the count), falls back to All otherwise.
        /// </summary>
        private void RefreshPublicationFilterChoices()
        {
            if (publicationFilter == null) return;

            string current = publicationFilter.value;
            bool currentIsPlace = publicationPlaceChoices.TryGetValue(current ?? string.Empty, out (string Connection, int? WorldId) currentPlace);

            publicationPlaceChoices.Clear();
            List<string> choices = new() { publication_all, publication_published, publication_not_published };

            List<string> sceneKeys = (sceneConfigurations?.SceneConfigurations ?? new List<SceneConfiguration>())
                .Where(c => c.Scene != null)
                .Select(c => PublishedEnvironmentsIndex.Key(c.Scene.name))
                .ToList();

            // Without data (logged out, first load in progress) there is nothing to filter a place on.
            IReadOnlyList<string> connections = PublishedEnvironmentsIndex.ConnectionsWithData;
            foreach (string connection in connections)
            {
                int CountIn(int? place) => sceneKeys.Count(k => PublishedEnvironmentsIndex.IsPublishedIn(connection, k, place));
                string prefix = connections.Count > 1 ? $"{PublishedEnvironmentsIndex.LabelOf(connection)} · " : string.Empty;

                if (!PublishedEnvironmentsIndex.TenantUnknownFor(connection))
                {
                    string tenantChoice = $"{prefix}{publication_tenant} ({CountIn(null)})";
                    choices.Add(tenantChoice);
                    publicationPlaceChoices[tenantChoice] = (connection, null);
                }

                IReadOnlyList<(int Id, string Label)> worlds = PublishedEnvironmentsIndex.WorldsOf(connection);
                HashSet<string> duplicateLabels = new(worlds.GroupBy(w => w.Label).Where(g => g.Count() > 1).Select(g => g.Key));
                foreach ((int id, string label) in worlds.OrderBy(w => w.Label, StringComparer.OrdinalIgnoreCase))
                {
                    // The dropdown shows its value as text, so two worlds with the same name need the id to tell them apart.
                    string worldName = duplicateLabels.Contains(label) ? $"{label} [ID: {id}]" : label;
                    string choice = $"{prefix}{publication_world_prefix}{worldName} ({CountIn(id)})";
                    if (publicationPlaceChoices.ContainsKey(choice)) continue;
                    choices.Add(choice);
                    publicationPlaceChoices[choice] = (connection, id);
                }
            }

            string next = currentIsPlace
                ? publicationPlaceChoices.FirstOrDefault(p => string.Equals(p.Value.Connection, currentPlace.Connection, StringComparison.OrdinalIgnoreCase)
                                                              && p.Value.WorldId == currentPlace.WorldId).Key ?? publication_all
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
                if (publication != publication_all && PublishedEnvironmentsIndex.AnyData)
                {
                    string key = PublishedEnvironmentsIndex.Key(cfg.Scene.name);
                    if (publicationPlaceChoices.TryGetValue(publication, out (string Connection, int? WorldId) place))
                    {
                        if (!PublishedEnvironmentsIndex.IsPublishedIn(place.Connection, key, place.WorldId)) continue;
                    }
                    else
                    {
                        bool published = PublishedEnvironmentsIndex.IsPublishedAnywhere(key);
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

        /// <summary>Filter choices that point at one place: a connection and a world id, or null for its tenant level.</summary>
        private readonly Dictionary<string, (string Connection, int? WorldId)> publicationPlaceChoices = new();

        private static readonly Color preview_new_color = new(0.42f, 0.78f, 0.45f);
        private static readonly Color preview_update_color = new(0.95f, 0.66f, 0.28f);
        private static readonly Color preview_note_color = new(0.62f, 0.62f, 0.62f);

        private static VisualElement NewDeployPreview()
        {
            return new VisualElement { style = { marginLeft = 22, marginBottom = 4 } };
        }

        /// <summary>
        /// Under every ticked world and tenant level, in its connection's group: what the current "Include in build"
        /// selection does there — the scenes that arrive as new environments (green) and the ones that overwrite an
        /// environment already published with that name (orange).
        /// </summary>
        private void RefreshDeployPreviews()
        {
            foreach (TargetGroup group in groups.Values)
            {
                foreach (KeyValuePair<int, VisualElement> pair in group.WorldPreviews)
                {
                    if (group.WorldToggles.TryGetValue(pair.Key, out Toggle toggle) && toggle.value)
                        FillDeployPreview(pair.Value, pair.Key, group.Key);
                    else
                        pair.Value.Clear();
                }

                if (group.TenantPreview == null)
                    continue;

                if (group.TenantToggle?.value == true)
                    FillDeployPreview(group.TenantPreview, null, group.Key);
                else
                    group.TenantPreview.Clear();
            }
        }

        private void FillDeployPreview(VisualElement target, int? worldId, string connectionKey)
        {
            target.Clear();
            if (sceneConfigurations?.SceneConfigurations == null) return;

            DeployPreview preview = EnvironmentsDeploy.Preview(EnvironmentsBuild.IncludedScenes(sceneConfigurations), worldId, connectionKey);

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

            // Build buttons wait while any selected scene targets a non-installed module.
            // The plain deploy button (which only re-uploads already-built zips) does not.
            missingModules = anyWarning;
            RefreshButtons();
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

            // The build alone has no target: the scripts are verified on the default connection's platform.
            if (!await BuildAndVerifyInterpretedDLLAsync(ProjectConnections.DefaultKey))
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
        private static Task<bool> BuildAndVerifyInterpretedDLLAsync(string connectionKey) => EnvironmentsBuild.VerifyInterpretedScriptsAsync(connectionKey);

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
            else if (result.Failure == EnvironmentsBuildFailure.InvalidSceneNames)
                EditorUtility.DisplayDialog("Scene names", result.Message, "OK");
            else if (result.Failure == EnvironmentsBuildFailure.BuildFailed)
                EditorUtility.DisplayDialog("Build failed", result.Message, "OK");

            buildResult = result.Failure switch
            {
                EnvironmentsBuildFailure.MissingModule => EBuildError.MissingModule,
                EnvironmentsBuildFailure.FirstpassScripts => EBuildError.FirstpassScripts,
                EnvironmentsBuildFailure.FolderMissing => EBuildError.FolderMissing,
                EnvironmentsBuildFailure.BinaryCatalog => EBuildError.BinaryCatalog,
                EnvironmentsBuildFailure.BuildFailed => EBuildError.BuildFailed,
                EnvironmentsBuildFailure.InvalidSceneNames => EBuildError.InvalidSceneNames,
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
