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
using System.Threading;
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
    /// The Publish Environments window (menu Virtuademy > Publish Environments; the class kept its old name): build the
    /// project's scenes, and deploy them to the worlds and tenant levels of the connections the project uses (meta-repo
    /// ADR 0037). Connections come in here: the file the Back Office downloads goes into the encrypted connection store,
    /// and each connection gets a group where it is logged in and ticked.
    /// </summary>
    /// <remarks>
    /// The window is an environment project's. The Virtuademy application, which has this package installed too, defines
    /// <c>VIRTUADEMY_APPLICATION</c>, and its menu is not compiled there: an application switches tenant in Available
    /// Tenants, with no login.
    /// </remarks>
    public class AddressablesManagementWindow : EditorWindow
    {
        private const string window_title = "Publish Environments";

        // Set once the project's old configuration files were offered to the connection store in this editor session.
        private const string credentials_imported_key = "Virtuademy_PublishEnvironments_CredentialsImported";

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

        // Logins started from a group, by connection key: they outlive a rebuild of the groups.
        private readonly Dictionary<string, PendingLogin> pendingLogins = new(StringComparer.OrdinalIgnoreCase);

        // UI references
        private Label loginStatusLabel;
        private Label connectionsSummaryLabel;
        private Label deploySummaryLabel;
        private Label buildStatusLabel;
        private Label scenesInBuildLabel;
        private VisualElement blockingBanner;
        private VisualElement advancedFixRow;
        private Foldout advancedFoldout;
        private IVisualElementScheduledItem sessionExpiryRefresh;
        private VisualElement deploySection;
        private VisualElement targetsContainer;
        private VisualElement versionNotice;
        private Button deployButton;
        private Button buildAndDeployButton;
        private Button importConfigurationButton;
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

        /// <summary>
        /// One connection the project uses, as the window shows it: a header row (state, tenant, environment, platform
        /// version, default star, the main action and a menu) and a body with the account and the targets.
        /// </summary>
        private sealed class TargetGroup
        {
            public string Key;
            public VisualElement Root;
            public Foldout Arrow;
            public VisualElement Body;
            public bool Expanded;
            public VisualElement Dot;
            public Label EnvironmentChip;
            public Label VersionChip;
            public Label Summary;
            public Button LoginButton;
            public Button BackofficeButton;
            public Button MenuButton;
            public Label Status;
            public VisualElement LoginActions;
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

        /// <summary>A browser login started from a group: it can be cancelled, and its authorization URL copied.</summary>
        private sealed class PendingLogin
        {
            public readonly CancellationTokenSource Cancellation = new();
            public string AuthUrl;
            public string Status;
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

#if !VIRTUADEMY_APPLICATION
        [MenuItem("Virtuademy/Publish Environments")]
#endif
        public static void ShowExample()
        {
            AddressablesManagementWindow wnd = GetWindow<AddressablesManagementWindow>();
            wnd.titleContent = new GUIContent(window_title);
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

            RefreshSettingsChecks();
            RefreshButtons();
        }


        public void CreateGUI()
        {
            root = rootVisualElement;

            // A window restored from a layout saved before the rename keeps the title it was saved with.
            titleContent = new GUIContent(window_title);

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            ShowLogo();
            InitializeWindow();
            ImportProjectCredentials();
        }

        /// <summary>The Virtuademy header of the SDK's windows (SDK-Core), with this window's name.</summary>
        private void ShowLogo()
        {
            root.Q<VisualElement>("window-header-slot").Add(VirtuademyWindowHeader.Create("Publish Environments"));
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
            connectionsSummaryLabel = root.Q<Label>("connections-summary-label");
            deploySummaryLabel = root.Q<Label>("deploy-summary-label");
            deploySection = root.Q<VisualElement>("deploy-section");
            targetsContainer = root.Q<VisualElement>("deploy-targets");
            deployButton = root.Q<Button>("deploy-button");
            buildAndDeployButton = root.Q<Button>("build-and-deploy-button");

            // Connections come in through the section's import; each then has its group below, where it is logged in and
            // logged out.
            importConfigurationButton = root.Q<Button>("import-configuration-button");
            importConfigurationButton.clicked += OnImportConfigurationClicked;

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

            root.Q<VisualElement>("deploy-errors-slot").Add(deployErrorsContainer);
        }

        /// <summary>
        /// A login or a logout on any connection, or a connection added or removed. The errors in the panel belong to the
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

        /// <summary>
        /// The file the Back Office downloads, into the encrypted connection store (<see cref="ConfigurationFileImport"/>): one
        /// connection per environment of the file, each one the project uses from now on. No file is written to the project.
        /// It needs the network, because a connection is named by the tenant key the platform gives; it makes no login.
        /// </summary>
        private async void OnImportConfigurationClicked()
        {
            string file = ConfigurationFileImport.PickFile();
            if (file == null)
                return;

            importConfigurationButton.SetEnabled(false);
            try
            {
                ConfigurationFileImport.StoreImportResult result = await ConfigurationFileImport.ImportIntoStoreAsync(file,
                    onStatus: status => { if (connectionsSummaryLabel != null) connectionsSummaryLabel.text = status; });

                if (result.Failures.Count > 0)
                {
                    EditorUtility.DisplayDialog("Import of the configuration file",
                        (result.Connections.Count > 0 ? "Some environments of the file were imported, not these:\n\n" : "Nothing was imported:\n\n") +
                        string.Join("\n", result.Failures), "OK");
                }
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[PublishEnvironments] The configuration file was not imported: {ex.Message}");
                EditorUtility.DisplayDialog("Import failed", ex.Message, "OK");
            }
            finally
            {
                importConfigurationButton.SetEnabled(true);
                RebuildTargets();
            }
        }

        /// <summary>
        /// The configuration files an environment project imported before the connection store (meta-repo ADR 0037,
        /// "Migration") go into the store, once per editor session: it asks the platform about each credential the store
        /// does not hold yet. The files are left where they are, and say so in the Console: an environment project does not
        /// need them any more.
        /// </summary>
        private async void ImportProjectCredentials()
        {
            if (SessionState.GetBool(credentials_imported_key, false))
                return;

            SessionState.SetBool(credentials_imported_key, true);

            AppConfigurationSettings settings = AppConfigurationSettings.FindSelected();
            if (settings == null || settings.AppAssets.Count == 0)
                return;

            var (added, known, failures) = await ConnectionLoginService.ImportProjectCredentialsAsync(settings);
            if (added > 0 || failures.Count > 0)
            {
                Debug.Log($"[PublishEnvironments] The project's configuration files: {added} connection(s) added to the store, {known} already there" +
                          (failures.Count > 0 ? $", {failures.Count} not resolved: {string.Join("; ", failures)}." : ".") +
                          " Each needs one login, in its group of the Publish Environments window. The files themselves, listed in " +
                          $"{AssetDatabase.GetAssetPath(settings)}, are not needed by an environment project any more.");
            }

            RebuildTargets();
        }

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
                targetsContainer.Add(new Label("No connection yet. Download the configuration file of a tenant from its Back Office " +
                                               "(Download SDK Environments Config) and import it with the button above: each connection " +
                                               "then gets its group here, where you log in.")
                {
                    style = { whiteSpace = WhiteSpace.Normal, marginLeft = 4, marginRight = 4, marginBottom = 4 }
                });
            }

            foreach (ConnectionRecord record in connections)
            {
                TargetGroup group = BuildGroup(record, projectVersion);
                groups[record.Key] = group;
                targetsContainer.Add(group.Root);

                if (group.Expanded)
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
            group.Expanded = groupExpanded.TryGetValue(record.Key, out bool open) ? open : !group.Production;

            group.Root = new VisualElement
            {
                style = { paddingBottom = 3, marginBottom = 3, borderBottomWidth = 1, borderBottomColor = new Color(0f, 0f, 0f, 0.25f) },
            };

            // The header: the state, who the tenant is, and what can be done with it.
            VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 22 } };

            group.Arrow = new Foldout { text = string.Empty, value = group.Expanded, style = { marginRight = 0, flexShrink = 0 } };
            group.Arrow.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == group.Arrow)
                    SetExpanded(group, evt.newValue);
            });
            header.Add(group.Arrow);

            group.Dot = new VisualElement();
            group.Dot.AddToClassList("dot");
            header.Add(group.Dot);

            header.Add(new Label(record.TenantLabel) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 4 } });

            // Production carries no chip: the chips mark the environments that are not the real one.
            if (!group.Production)
            {
                group.EnvironmentChip = Chip(record.Environment ?? "?", false);
                group.EnvironmentChip.tooltip = "The tenant's environment.";
                if (string.Equals(record.Environment, nameof(Env.Sandbox), StringComparison.OrdinalIgnoreCase))
                    group.EnvironmentChip.AddToClassList("chip-sandbox");
                else if (string.Equals(record.Environment, nameof(Env.PreProduction), StringComparison.OrdinalIgnoreCase))
                    group.EnvironmentChip.AddToClassList("chip-preproduction");
                header.Add(group.EnvironmentChip);
            }

            bool locked = !group.Allowed;
            group.VersionChip = Chip(locked ? $"{record.PlatformVersion ?? "no version"} · locked" : record.PlatformVersion ?? "no version", locked);
            group.VersionChip.tooltip = PlatformVersionRule.Refusal(group.Version, projectVersion, record.PlatformVersion) ?? "The tenant's platform version.";
            header.Add(group.VersionChip);

            group.Summary = new Label { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleRight, marginRight = 4 } };
            group.Summary.AddToClassList("muted");
            header.Add(group.Summary);

            // Shown once the group's listing has asked whether the account may enter (LoadGroupBackofficeAccessAsync).
            group.BackofficeButton = new Button(() => OpenBackoffice(group)) { text = "Back Office ↗", style = { flexShrink = 0, display = DisplayStyle.None } };
            header.Add(group.BackofficeButton);

            group.LoginButton = new Button(() => LoginGroup(group)) { text = "Log in", tooltip = "Logs in to this tenant in the browser.", style = { flexShrink = 0 } };
            header.Add(group.LoginButton);

            group.MenuButton = IconButton(menu_icons, "...", () => ShowGroupMenu(group));
            group.MenuButton.tooltip = "More actions";
            header.Add(group.MenuButton);

            // A click on the header, outside its buttons, opens or closes the group.
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is not VisualElement target || target is Button || target.GetFirstAncestorOfType<Button>() != null
                    || target == group.Arrow || group.Arrow.Contains(target))
                    return;
                group.Arrow.value = !group.Arrow.value;
            });

            group.Root.Add(header);

            // The body: the account, why the group cannot be deployed to when it cannot, and the targets.
            group.Body = new VisualElement { style = { marginLeft = 20 } };

            VisualElement accountRow = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            group.Status = new Label { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1, fontSize = 11 } };
            accountRow.Add(group.Status);
            group.LoginActions = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0, display = DisplayStyle.None } };
            group.LoginActions.Add(new Button(() =>
            {
                if (pendingLogins.TryGetValue(group.Key, out PendingLogin login) && login.AuthUrl != null)
                    EditorGUIUtility.systemCopyBuffer = login.AuthUrl;
            }) { text = "Copy link", tooltip = "Copies the login address, to open it in another browser." });
            group.LoginActions.Add(new Button(() =>
            {
                if (pendingLogins.TryGetValue(group.Key, out PendingLogin login))
                    login.Cancellation.Cancel();
            }) { text = "Cancel" });
            accountRow.Add(group.LoginActions);
            group.Body.Add(accountRow);

            if (locked)
            {
                group.Body.Add(new Label(ShortRefusal(group.Version, projectVersion, record.PlatformVersion))
                {
                    tooltip = PlatformVersionRule.Refusal(group.Version, projectVersion, record.PlatformVersion),
                    style = { color = new Color(0.9f, 0.45f, 0.45f), fontSize = 11, whiteSpace = WhiteSpace.Normal, marginBottom = 2 },
                });
            }

            group.WorldsStatus = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 2, fontSize = 11 } };
            group.WorldsStatus.AddToClassList("muted");
            group.Body.Add(group.WorldsStatus);
            group.WorldsList = new VisualElement();
            group.Body.Add(group.WorldsList);

            group.Root.Add(group.Body);
            group.Body.style.display = group.Expanded ? DisplayStyle.Flex : DisplayStyle.None;

            return group;
        }

        private static readonly string[] menu_icons = { "_Menu", "d__Menu", "pane options" };
        private static readonly string[] check_icons = { "TestPassed", "d_GreenCheckmark", "GreenCheckmark" };
        private static readonly string[] warning_icons = { "console.warnicon.sml", "console.warnicon", "d_console.warnicon.sml" };
        private static readonly string[] error_icons = { "console.erroricon.sml", "console.erroricon" };

        /// <summary>The first of Unity's built-in icons that exists, or null.</summary>
        private static Texture2D BuiltinIcon(string[] names)
        {
            foreach (string name in names)
            {
                Texture2D texture = EditorGUIUtility.FindTexture(name);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static void SetIcon(VisualElement element, string[] names)
        {
            Texture2D texture = BuiltinIcon(names);
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>A flat 18-pixel button showing a built-in icon, or <paramref name="fallback"/> as text where the icon is missing.</summary>
        private static Button IconButton(string[] icons, string fallback, Action clicked)
        {
            Button button = new(clicked);
            Texture2D texture = BuiltinIcon(icons);
            if (texture != null)
            {
                button.style.backgroundImage = new StyleBackground(texture);
                button.style.width = 18;
                button.style.height = 18;
                button.style.paddingLeft = button.style.paddingRight = 0;
                button.style.backgroundColor = Color.clear;
                button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
                button.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                button.text = fallback;
            }

            button.style.flexShrink = 0;
            return button;
        }

        private static Label Chip(string text, bool danger)
        {
            Label chip = new(text);
            chip.AddToClassList("chip");
            if (danger)
                chip.AddToClassList("chip-danger");
            return chip;
        }

        /// <summary>The version rule's refusal in one line; the full text is the tooltip.</summary>
        private static string ShortRefusal(PlatformVersionRule.Verdict verdict, string projectVersion, string tenantVersion) =>
            verdict == PlatformVersionRule.Verdict.TargetUnknown || string.IsNullOrEmpty(tenantVersion)
                ? "The tenant reports no platform version: no deploy here."
                : $"The tenant runs {tenantVersion}, the project is set up for {projectVersion}: no deploy here.";

        private void SetExpanded(TargetGroup group, bool expanded)
        {
            group.Expanded = expanded;
            groupExpanded[group.Key] = expanded;
            group.Body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (expanded && !group.Listed && !group.Listing)
                LoadGroupWorlds(group);
            RefreshLabels();
        }

        /// <summary>The group's less frequent actions.</summary>
        private void ShowGroupMenu(TargetGroup group)
        {
            ConnectionRecord record = ConnectionStore.Get(group.Key);
            bool hasSession = record?.HasSession == true;

            GenericMenu menu = new();
            if (hasSession)
                menu.AddItem(new GUIContent("Refresh worlds"), false, () =>
                {
                    if (!group.Expanded)
                        group.Arrow.value = true;
                    else if (!group.Listing)
                        LoadGroupWorlds(group);
                });
            else
                menu.AddDisabledItem(new GUIContent("Refresh worlds"));

            menu.AddSeparator(string.Empty);

            if (hasSession)
                menu.AddItem(new GUIContent("Log out"), false, async () => await EditorSessionManager.LogoutAsync(group.Key));
            else
                menu.AddDisabledItem(new GUIContent("Log out"));

            menu.AddItem(new GUIContent("Remove from project..."), false, () =>
            {
                string label = PublishedEnvironmentsIndex.LabelOf(group.Key);
                if (EditorUtility.DisplayDialog("Remove from project",
                        $"Stop using {label} in this project? Its ticks go with it. The connection stays in your store, " +
                        "with its login, for your other projects; importing its configuration file brings it back.",
                        "Remove", "Cancel"))
                    ProjectConnections.Remove(group.Key);
            });

            menu.DropDown(group.MenuButton.worldBound);
        }

        /// <summary>
        /// The browser login of one group's connection. While it waits, the
        /// group's account line says so, with the login address to copy and a Cancel.
        /// </summary>
        private async void LoginGroup(TargetGroup group)
        {
            string key = group.Key;
            if (pendingLogins.ContainsKey(key))
                return;

            PendingLogin login = new();
            pendingLogins[key] = login;
            RefreshLabels();

            try
            {
                LoginOptions options = new()
                {
                    Cancellation = login.Cancellation.Token,
                    OnStatus = status => EditorApplication.delayCall += () => { login.Status = status; RefreshLabels(); },
                };
                options.Browser.OnAuthUrl = url => EditorApplication.delayCall += () => { login.AuthUrl = url; RefreshLabels(); };

                LoginOutcome outcome = await ConnectionLoginService.LoginAsync(key, options);

                if (!outcome.Success && outcome.Code != "cancelled" && !login.Cancellation.IsCancellationRequested)
                    LogDeployError($"{PublishedEnvironmentsIndex.LabelOf(key)}: the login failed. {outcome.Message}");
            }
            finally
            {
                pendingLogins.Remove(key);
                login.Cancellation.Dispose();
                RefreshLabels();
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
                group.WorldsStatus.text = "Log in to list the worlds.";
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

                // By name, then by id for two worlds with the same name: the order of the ticks and of the Back Office link.
                group.Deployable = listing.Deployable
                    .OrderBy(w => w.Label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(w => w.Id)
                    .ToList();
                group.Listed = true;
                FillGroupTargets(group, ConnectionStore.Get(group.Key) ?? record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PublishEnvironments] Error loading the worlds of {PublishedEnvironmentsIndex.LabelOf(group.Key)}: {ex.Message}");
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

            // A group the version rule refuses shows its targets unticked and locked; the ticks the project kept for it
            // are left alone and come back if the versions agree again.
            if (record.IsTenantManager)
            {
                group.TenantToggle = new Toggle
                {
                    text = "Whole tenant (tenant level)",
                    tooltip = "Every world of the tenant. The interpreted scripts are not uploaded: an environment deployed at tenant level runs without them.",
                    value = group.Allowed && savedTenant,
                };
                group.TenantToggle.SetEnabled(group.Allowed);
                group.TenantToggle.RegisterValueChangedCallback(evt => OnTargetTicked(group, evt));
                group.TenantPreview = NewDeployPreview();
                group.WorldsList.Add(group.TenantToggle);
                group.WorldsList.Add(group.TenantPreview);

                if (group.Deployable.Count > 0)
                    group.WorldsList.Add(new VisualElement { style = { height = 1, marginTop = 2, marginBottom = 3, backgroundColor = new Color(0f, 0f, 0f, 0.3f) } });
            }

            foreach (PublishableWorld world in group.Deployable)
            {
                VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                Toggle toggle = new() { text = world.Label, value = group.Allowed && savedWorlds.Contains(world.Id), style = { flexGrow = 1, flexShrink = 1 } };
                toggle.SetEnabled(group.Allowed);
                toggle.RegisterValueChangedCallback(evt => OnTargetTicked(group, evt));
                Label id = new($"#{world.Id}") { tooltip = "The world's ID.", style = { flexShrink = 0, marginRight = 4 } };
                id.AddToClassList("muted");
                row.Add(toggle);
                row.Add(id);

                VisualElement preview = NewDeployPreview();
                group.WorldToggles[world.Id] = toggle;
                group.WorldPreviews[world.Id] = preview;
                group.WorldsList.Add(row);
                group.WorldsList.Add(preview);
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
                    Debug.LogWarning($"[PublishEnvironments] Could not read the Back Office access of {PublishedEnvironmentsIndex.LabelOf(connectionKey)}: {response.StatusCode}");
                    return null;
                }

                BackofficeAccess access = JsonConvert.DeserializeObject<BackofficeAccess>(await response.Content.ReadAsStringAsync());
                return access?.CanAccess == true ? access : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PublishEnvironments] Could not read the Back Office access of {PublishedEnvironmentsIndex.LabelOf(connectionKey)}: {ex.Message}");
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

        /// <summary>
        /// The section's summary, and each group's state: its dot, summary, buttons and account line; then the session-expiry
        /// refresh.
        /// </summary>
        private void RefreshLabels()
        {
            if (loginStatusLabel == null)
                return;

            List<GroupTargets> ticked = TickedTargets();
            int targetCount = ticked.Sum(t => t.Count);

            if (connectionsSummaryLabel != null)
            {
                connectionsSummaryLabel.text = groups.Count == 0
                    ? string.Empty
                    : $"{Plural(groups.Count, "connection")} · {Plural(targetCount, "target")}";
            }

            loginStatusLabel.style.display = DisplayStyle.None;

            foreach (TargetGroup group in groups.Values)
            {
                ConnectionRecord record = ConnectionStore.Get(group.Key);
                if (record == null)
                    continue;

                pendingLogins.TryGetValue(group.Key, out PendingLogin login);

                group.Dot.style.backgroundColor = login != null ? new Color(0.35f, 0.6f, 0.95f)
                    : !record.HasSession ? new Color(0.5f, 0.5f, 0.5f)
                    : record.IsTokenValid ? new Color(0.3f, 0.78f, 0.35f)
                    : new Color(0.92f, 0.68f, 0.2f);
                group.Dot.tooltip = login != null ? "Logging in" : !record.HasSession ? "Not logged in"
                    : record.IsTokenValid ? "Logged in" : "Session expired: it is renewed on the next operation";

                int groupTargets = ticked.Where(t => t.Group == group).Sum(t => t.Count);
                group.Summary.text = login != null ? "logging in"
                    : !record.HasSession ? "not logged in"
                    : groupTargets > 0 && !group.Expanded ? Plural(groupTargets, "target")
                    : string.Empty;

                group.LoginButton.style.display = !record.HasSession && login == null ? DisplayStyle.Flex : DisplayStyle.None;
                group.LoginActions.style.display = login != null ? DisplayStyle.Flex : DisplayStyle.None;

                if (login != null)
                {
                    group.Status.text = login.AuthUrl != null ? "Waiting for the browser…" : login.Status ?? "Logging in…";
                    group.Status.style.color = StyleKeyword.Null;
                    group.Status.tooltip = string.Empty;
                }
                else if (record.HasSession)
                {
                    string user = string.IsNullOrEmpty(record.Username) ? "Logged in" : record.Username;
                    string role = record.IsTenantManager ? " · Tenant manager" : string.Empty;
                    group.Status.text = record.IsTokenValid
                        ? $"{user}{role} · until {ShortTime(record.UsableUntilUtc)}"
                        : $"{user}{role} · session expired, renewed on the next operation";
                    group.Status.style.color = record.IsTokenValid ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.92f, 0.68f, 0.2f);
                    group.Status.tooltip = $"Account: {record.Username}{EditorSessionManager.DescribeSession(record, out _)}\nConnection: {record.Key}";
                }
                else
                {
                    group.Status.text = "Not logged in.";
                    group.Status.style.color = new Color(0.6f, 0.6f, 0.6f);
                    group.Status.tooltip = $"Connection: {record.Key}";
                }

                RefreshBackofficeButton(group);
                if (login != null || !record.HasSession)
                    group.BackofficeButton.style.display = DisplayStyle.None;
            }

            ScheduleSessionExpiryRefresh();
        }

        private static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

        /// <summary>A moment in local time: the hour today, the day and the hour otherwise ("3 Nov 09:32").</summary>
        private static string ShortTime(DateTime? utc)
        {
            if (utc == null)
                return "?";

            DateTime local = utc.Value.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
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
                Debug.LogWarning("[PublishEnvironments] No scenes marked for build.");
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
        /// target; none works while a deploy runs. The lines beside them say what a build and a deploy would cover.
        /// </summary>
        private void RefreshButtons()
        {
            List<GroupTargets> ticked = TickedTargets();
            bool hasTargets = ticked.Count > 0;
            const string missingModulesTooltip =
                "Disabled: one or more selected scenes require build modules that are not installed. " +
                "See the warnings above.";
            const string noTargetTooltip = "Tick a world, or the tenant level, in the group of a connection first.";

            if (buildAddressablesButton != null)
            {
                buildAddressablesButton.SetEnabled(!missingModules && !deployRunning);
                buildAddressablesButton.tooltip = missingModules ? missingModulesTooltip
                    : "Builds the included scenes for their platforms and zips them into ServerData. The interpreted scripts are verified first, on the platform of the ticked targets' connections.";
            }

            if (deployButton != null)
            {
                deployButton.SetEnabled(hasTargets && !deployRunning);
                deployButton.tooltip = hasTargets ? "Uploads the built zips of the included scenes to every ticked target." : noTargetTooltip;
            }

            if (buildAndDeployButton != null)
            {
                buildAndDeployButton.SetEnabled(hasTargets && !missingModules && !deployRunning);
                buildAndDeployButton.tooltip = missingModules ? missingModulesTooltip
                    : hasTargets ? "Builds the included scenes once, then verifies and uploads them to every ticked target with its own connection's login."
                    : noTargetTooltip;
            }

            int scenes = sceneConfigurations?.SceneConfigurations?.Count(c => c != null && c.Scene != null && c.IncludeInBuild) ?? 0;

            if (deploySummaryLabel != null)
                deploySummaryLabel.text = !hasTargets ? "Tick a world, or a tenant level, to deploy."
                    : scenes == 0 ? "No scene is included in the build."
                    : $"{Plural(scenes, "scene")} → {DescribeTargets(ticked)}";

            RefreshBuildStatus();

            if (connectionsSummaryLabel != null && groups.Count > 0)
                connectionsSummaryLabel.text = $"{Plural(groups.Count, "connection")} · {Plural(ticked.Sum(t => t.Count), "target")}";
        }

        /// <summary>"2 worlds of Virtuademy (Sandbox)", "the tenant level of …", or "3 targets in 2 tenants".</summary>
        private static string DescribeTargets(List<GroupTargets> ticked)
        {
            if (ticked.Count == 1)
            {
                GroupTargets t = ticked[0];
                string label = PublishedEnvironmentsIndex.LabelOf(t.Group.Key);
                string worlds = t.Worlds.Count > 0 ? Plural(t.Worlds.Count, "world") : null;
                return worlds != null && t.Tenant ? $"{worlds} and the tenant level of {label}"
                    : worlds != null ? $"{worlds} of {label}"
                    : $"the tenant level of {label}";
            }

            return $"{Plural(ticked.Sum(t => t.Count), "target")} in {ticked.Count} tenants";
        }

        /// <summary>
        /// The Scenes section's header (how many scenes are in the build) and the line beside Build: when the zips of the
        /// included scenes were last written to ServerData, and how many of them exist.
        /// </summary>
        private void RefreshBuildStatus()
        {
            List<SceneConfiguration> configs = sceneConfigurations?.SceneConfigurations?.Where(c => c != null && c.Scene != null).ToList();
            if (configs == null)
                return;

            List<string> included = configs.Where(c => c.IncludeInBuild).Select(c => c.SceneNameFiltered).ToList();

            if (scenesInBuildLabel != null)
                scenesInBuildLabel.text = $"{included.Count} of {Plural(configs.Count, "scene")} in the build";

            if (buildStatusLabel == null)
                return;

            List<DateTime> built = included
                .Select(name => Path.Combine(addressables_output_folder, name + ".zip"))
                .Where(File.Exists)
                .Select(File.GetLastWriteTimeUtc)
                .ToList();

            buildStatusLabel.text = included.Count == 0 ? "Tick the scenes to build."
                : built.Count == 0 ? "Not built yet."
                : $"Last build: {ShortTime(built.Max())} · {built.Count} of {Plural(included.Count, "scene")} built";
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
            sceneSearchField.textEdition.placeholder = "Search scenes";
            sceneSearchField.RegisterValueChangedCallback(_ => RebuildSceneList());
            onlyInBuildToggle = new Toggle("In build only") { style = { marginRight = 6 } };
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

            sceneListContainer = new ScrollView { style = { maxHeight = 360 } };
            container.Add(sceneListContainer);

            // The folders whose scenes never make the list (package samples, third-party assets, plugins, and the ones the
            // creator excluded from a folder's header), each with Include.
            excludedFoldersFoldout = new Foldout { value = false, style = { marginTop = 4 } };
            excludedFoldersFoldout.tooltip = "Scenes under these folders are not listed, built or published. Include brings a folder's scenes back.";
            root.Q<VisualElement>("excluded-folders-slot").Add(excludedFoldersFoldout);

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
            RebuildExcludedFolders();
            string currentFolder = null;

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

                // The rows go by folder; each folder's header can exclude it.
                string folder = SceneFolder(cfg);
                if (folder != currentFolder)
                {
                    currentFolder = folder;
                    sceneListContainer.Add(FolderHeader(folder));
                }

                PropertyField row = new(list.GetArrayElementAtIndex(i)) { style = { marginLeft = 8 } };
                row.Bind(sceneSerializedObject);
                // Refresh platform warnings whenever a property changes (e.g. toggling a platform flag)
                row.RegisterCallback<SerializedPropertyChangeEvent>(_ => { RefreshPlatformWarnings(); RefreshDeployPreviews(); RefreshButtons(); });
                sceneListContainer.Add(row);
                shown++;
            }

            sceneCountLabel.text = shown == configs.Count ? string.Empty : $"{shown} shown";
        }

        private Foldout excludedFoldersFoldout;

        private static string SceneFolder(SceneConfiguration configuration) =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(configuration.Scene))?.Replace('\\', '/') ?? "Assets";

        /// <summary>A folder's header in the scene list: its path, how many of its scenes are listed, and Exclude.</summary>
        private VisualElement FolderHeader(string folder)
        {
            VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            Label path = new(folder) { style = { flexGrow = 1, flexShrink = 1, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } };
            path.AddToClassList("muted");
            header.Add(path);

            Button exclude = new(() => ExcludeFolder(folder))
            {
                text = "Exclude",
                tooltip = "Takes this folder's scenes, and its subfolders', out of the list: they are not built or published. " +
                          "The folder goes to the excluded folders below, where Include brings it back.",
                style = { fontSize = 10, height = 16, paddingTop = 0, paddingBottom = 0 },
            };
            header.Add(exclude);
            return header;
        }

        private void ExcludeFolder(string folder)
        {
            int included = (sceneConfigurations.SceneConfigurations ?? new List<SceneConfiguration>())
                .Count(c => c.Scene != null && c.IncludeInBuild && (SceneFolder(c) + "/").StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
            if (included > 0 && !EditorUtility.DisplayDialog("Exclude folder",
                    $"{Plural(included, "scene")} of {folder} {(included == 1 ? "is" : "are")} in the build. Excluding the folder takes " +
                    $"{(included == 1 ? "it" : "them")} out of the build and of the deploy.", "Exclude", "Cancel"))
                return;

            if (sceneConfigurations.ExcludeFolder(folder))
                AfterExclusionChanged();
        }

        private void IncludeFolder(string folder)
        {
            if (sceneConfigurations.IncludeFolder(folder))
                AfterExclusionChanged();
        }

        private void AfterExclusionChanged()
        {
            sceneConfigurations.SyncWithProject();
            SaveAsset(sceneConfigurations);
            RefreshPublicationFilterChoices();
            RebuildSceneList();
            RefreshPlatformWarnings();
            RefreshDeployPreviews();
            RefreshButtons();
        }

        /// <summary>
        /// The folders the creator excluded, each with the number of scenes it holds and Include (Remove for a folder that is
        /// no longer in the project). Hidden while there is none.
        /// </summary>
        private void RebuildExcludedFolders()
        {
            if (excludedFoldersFoldout == null || sceneConfigurations == null) return;
            excludedFoldersFoldout.Clear();

            IReadOnlyList<string> folders = sceneConfigurations.ExcludedFolders;
            excludedFoldersFoldout.style.display = folders.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            excludedFoldersFoldout.text = $"Excluded folders ({folders.Count})";

            foreach (string folder in folders.ToList())
            {
                bool exists = AssetDatabase.IsValidFolder(folder);
                int scenes = exists ? AssetDatabase.FindAssets("t:SceneAsset", new[] { folder }).Length : 0;

                VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                row.Add(new Label(folder) { style = { flexGrow = 1, flexShrink = 1 } });
                Label count = new(!exists ? "not in the project" : scenes == 0 ? "no scenes" : Plural(scenes, "scene")) { style = { marginRight = 4 } };
                count.AddToClassList("muted");
                row.Add(count);
                row.Add(new Button(() => IncludeFolder(folder))
                {
                    text = exists ? "Include" : "Remove",
                    tooltip = exists ? "Lists this folder's scenes again, unticked." : "Removes this folder from the excluded ones.",
                    style = { fontSize = 10, height = 16, paddingTop = 0, paddingBottom = 0 },
                });
                excludedFoldersFoldout.Add(row);
            }
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

            blockingBanner = root.Q<VisualElement>("blocking-banner");
            advancedFixRow = root.Q<VisualElement>("advanced-fix-row");
            advancedFoldout = root.Q<Foldout>("addressables-settings-foldout");
            buildStatusLabel = root.Q<Label>("build-status-label");
            scenesInBuildLabel = root.Q<Label>("scenes-in-build-label");

            SetIcon(root.Q<VisualElement>("blocking-icon"), warning_icons);
            SetIcon(root.Q<VisualElement>("folder-missing-icon"), error_icons);
            SetIcon(root.Q<VisualElement>("binary-catalog-icon"), error_icons);

            root.Q<Button>("blocking-fix-button").clicked += FixBuildSettings;
            root.Q<Button>("advanced-fix-button").clicked += FixBuildSettings;
            root.Q<Button>("blocking-details-button").clicked += () =>
            {
                advancedFoldout.value = true;
                VisualElement target = root.Q<VisualElement>("addressables-settings");
                root.schedule.Execute(() => root.Q<ScrollView>("window-scroll")?.ScrollTo(target));
            };

            // The checks are read again every two seconds: someone can change the Addressables settings in their own windows.
            RefreshSettingsChecks();
            root.schedule.Execute(RefreshSettingsChecks).Every(2000);

            Button topLevelSettingsButton = root.Q<Button>("top-level-settings-button");
            topLevelSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Settings");

            Button profileSettingsButton = root.Q<Button>("profile-settings-button");
            profileSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");

            Button groupsSettingsButton = root.Q<Button>("addressables-groups-button");
            groupsSettingsButton.clicked += () => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

            Button defaultLocalGroupButton = root.Q<Button>("default-local-group-button");
            defaultLocalGroupButton.clicked += () => Selection.activeObject = settings.DefaultGroup;

            // Build only builds: when the settings are not the SDK's, it says so and offers the fix.
            buildAddressablesButton = root.Q<Button>("build-addressables-button");
            buildAddressablesButton.clicked += async () => await RunBuildAddressablesAsync();

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

        /// <summary>
        /// The advanced section's items, each with a check or a warning icon and, in its tooltip, what the SDK expects; the
        /// count of items to fix in the section's title; and the banner at the top while anything differs.
        /// </summary>
        private void RefreshSettingsChecks()
        {
            if (root == null || blockingBanner == null)
                return;

            if (settings == null)
                settings = AddressablesConfiguration.GetOrCreateSettings();

            (string Icon, string Value, Func<string> Current, bool Ok, string Expected)[] items =
            {
                ("addressables-profile-check", "addressables-profile-value", () => ActiveProfileName, IsAddressablesSettingsConfigured,
                    "The top-level settings the SDK sets: the remote catalog on Remote.BuildPath and Remote.LoadPath, a JSON catalog " +
                    $"(with the {AddressablesConfiguration.JsonCatalogDefine} define), custom bundle naming, contiguous and non-recursive bundles."),
                ("remote-buildpath-check", "remote-buildpath-value", () => CurrentRemoteBuildPathVariableValue, IsRemoteBuildPathConfigured,
                    "Expected: " + AddressablesConfiguration.RemoteBuildPath),
                ("remote-loadpath-check", "remote-loadpath-value", () => CurrentRemoteLoadPathVariableValue, IsRemoteLoadPathConfigured,
                    "Expected: " + AddressablesConfiguration.RemoteLoadPath),
                ("build-target-check", "build-target-value", () => CurrentBuildTargetVariableValue, IsBuildTargetConfigured,
                    "Expected: " + AddressablesConfiguration.BuildTargetVariableValue),
                ("player-version-override-check", "player-version-override-value", () => CurrentPlayerVersionOverrideVariableValue, IsPlayerVersionOverrideConfigured,
                    "Expected: " + AddressablesConfiguration.PlayerVersionOverrideVariableValue),
                ("default-local-group-check", null, null, AddressablesConfiguration.AreGroupsConfigured(settings),
                    "Expected: one default group, built and loaded from the remote paths. Fix build settings resets the groups to it."),
            };

            int toFix = 0;
            foreach ((string icon, string value, Func<string> current, bool ok, string expected) in items)
            {
                VisualElement element = root.Q<VisualElement>(icon);
                SetIcon(element, ok ? check_icons : warning_icons);
                element.tooltip = (ok ? "As the SDK expects. " : "Not what the SDK expects. ") + expected;

                if (value != null)
                {
                    Label label = root.Q<Label>(value);
                    label.text = current() ?? "(missing)";
                    label.tooltip = element.tooltip;
                }

                if (!ok)
                    toFix++;
            }

            // The banner follows the same check the build makes, so the two never disagree.
            bool configured = AddressablesConfiguration.IsConfiguredFor(settings);
            if (!configured && toFix == 0)
                toFix = 1;

            DisplayStyle show = configured ? DisplayStyle.None : DisplayStyle.Flex;
            blockingBanner.style.display = show;
            advancedFixRow.style.display = show;
            advancedFoldout.text = configured ? "Advanced settings" : $"Advanced settings ({toFix} to fix)";
        }

        /// <summary>
        /// Applies the build settings the SDK needs (<see cref="AddressablesConfiguration.Apply"/>), which resets the project's
        /// asset groups to a single default group, after a confirmation.
        /// </summary>
        private void FixBuildSettings()
        {
            if (!EditorUtility.DisplayDialog("Fix build settings",
                    "Apply the build settings the SDK needs? It resets the project's asset groups to a single default group " +
                    "(the scenes are added again at the next build).", "Fix", "Cancel"))
                return;

            try
            {
                AddressablesConfiguration.Apply();
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError("[PublishEnvironments] " + ex.Message);
                EditorUtility.DisplayDialog("Fix build settings", ex.Message, "OK");
            }

            settings = AddressablesConfiguration.GetOrCreateSettings();
            RefreshSettingsChecks();
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
                Debug.Log($"[PublishEnvironments] {what} completed: {importsStarted} scene import(s) started on the platform.");
                return;
            }

            string summary = $"{what} finished with {problems} problem(s): {importsStarted} of {expected} scene import(s) " +
                             "started on the platform. The problems are listed in the Publish Environments window and in the Console.";
            Debug.LogWarning("[PublishEnvironments] " + summary);
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

        /// <summary>Builds the interpreted DLL, verifies it, and builds the scenes only if it passed.</summary>
        private async Task RunBuildAddressablesAsync()
        {
            if (!RequireAddressablesConfigured("Build"))
                return;

            // The script policy lives per platform: the interpreted scripts are verified on the connections of the ticked
            // targets, or on the project's only connection when nothing is ticked.
            List<string> keys = TickedTargets().Select(t => t.Group.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0)
            {
                if (ProjectConnections.DefaultKey == null && groups.Count > 1 && EnvironmentsBuild.HasInterpretedScripts)
                {
                    EditorUtility.DisplayDialog("Build",
                        $"The project uses {groups.Count} connections, and its interpreted scripts are verified on the platform they go to. " +
                        "Tick a target in the group of the connection to verify on, then build again. Nothing was built.", "OK");
                    return;
                }

                keys.Add(ProjectConnections.DefaultKey);
            }

            foreach (string key in keys)
            {
                if (await BuildAndVerifyInterpretedDLLAsync(key))
                    continue;

                string label = key == null ? "the platform" : PublishedEnvironmentsIndex.LabelOf(key);
                EditorUtility.DisplayDialog("Build blocked",
                    $"The interpreted script DLL did not pass the security checks of {label}. " +
                    "See the Console for the offending lines. The scenes were NOT built.",
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

            LogDeployError($"{what} aborted: the project's build settings are not the ones the SDK needs. " +
                           "Press Fix in the banner at the top of the window (\"Advanced settings\" lists what is wrong), then try again. Nothing was built.");
            if (EditorUtility.DisplayDialog($"{what} blocked",
                    "The project's build settings are not the ones the SDK needs, so a build would fail at the end. Nothing was built.\n\n" +
                    "Fix them now? It resets the project's asset groups to a single default group. Then start again.",
                    "Fix build settings", "Cancel"))
            {
                try
                {
                    AddressablesConfiguration.Apply();
                }
                catch (InvalidOperationException ex)
                {
                    Debug.LogError("[PublishEnvironments] " + ex.Message);
                }

                settings = AddressablesConfiguration.GetOrCreateSettings();
                RefreshSettingsChecks();
            }

            return false;
        }

        /// <summary>
        /// Builds the interpreted hot-update DLL and verifies it (local whitelist + authoritative
        /// server check). Returns true if the build may proceed; see
        /// <see cref="EnvironmentsBuild.VerifyInterpretedScriptsAsync"/>.
        /// </summary>
        private static Task<bool> BuildAndVerifyInterpretedDLLAsync(string connectionKey) => EnvironmentsBuild.VerifyInterpretedScriptsAsync(connectionKey);

        /// <summary>
        /// Builds the included scenes for all platforms and creates zip files.
        /// Does NOT upload — used by both the "Build" button and "Build & Deploy".
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

            RefreshButtons();

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
