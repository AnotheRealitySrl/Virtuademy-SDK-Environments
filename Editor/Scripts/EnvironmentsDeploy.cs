using Newtonsoft.Json;

using Renci.SshNet;

using Virtuademy.SDK.TenantConfiguration;
using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The two fields the editor reads off the platform's world listing.
    /// </summary>
    /// <remarks>
    /// Declared here rather than taken from the platform's own <c>WorldDTO</c>, because that type
    /// belongs to the HTTP client and lives in the package an application installs and a creator
    /// does not. Naming it here would put this package — the one every creator installs — a
    /// reference away from the platform client, which is the coupling the whole package split
    /// exists to prevent. The code already does its own request and its own deserialisation;
    /// this is the shape it deserialises into.
    /// </remarks>
    public class PublishableWorld
    {
        public int Id { get; set; }

        public string Label { get; set; }
    }

    /// <summary>How <see cref="EnvironmentsDeploy.LoadWorldsAsync"/> ended.</summary>
    public enum WorldListStatus
    {
        Ok,

        /// <summary>Neither the generated platform config nor the login says where the Application API is.</summary>
        NoApiUrl,

        /// <summary>The session is gone or expired and could not be renewed silently.</summary>
        SessionRequired,

        /// <summary>The platform answered with an error, or the request failed.</summary>
        Failed,
    }

    /// <summary>The worlds the login can see, and the ones it can deploy to.</summary>
    public sealed class WorldListResult
    {
        public WorldListStatus Status;

        /// <summary>The line the window shows in place of the list when <see cref="Status"/> is not <see cref="WorldListStatus.Ok"/>.</summary>
        public string Message;

        /// <summary>Every world the account can see.</summary>
        public List<PublishableWorld> Visible = new();

        /// <summary>The visible worlds where the account is a TenantManager, an EnvironmentManager or an Owner: the ones a deploy may target.</summary>
        public List<PublishableWorld> Deployable = new();
    }

    /// <summary>
    /// Where a deploy reports to. The window shows errors in its panel and progress in a bar; the
    /// command line logs both. Without one, errors go to the Console and progress is not shown.
    /// </summary>
    public sealed class DeployObserver
    {
        /// <summary>A problem, in one line. Called once per problem.</summary>
        public Action<string> Error;

        /// <summary>The step in progress: a title, what is being done, and how far along (0 to 1).</summary>
        public Action<string, string, float> Progress;

        /// <summary>The deploy is over: take the progress down.</summary>
        public Action ProgressDone;

        /// <summary>
        /// Called once, as soon as the deploy has its <see cref="DeployReport"/>, with that same object: the
        /// deploy fills it in step by step, so a caller that has to end the run early (a timeout, an
        /// exception) can still say what was uploaded, started and linked.
        /// </summary>
        public Action<DeployReport> Started;

        /// <summary>
        /// Asked before every world, assembly, scene and link step (never in the middle of one): a non-null
        /// answer is the reason to stop. The step in flight finishes, the next ones are not started, and
        /// <see cref="DeployReport.Stopped"/> and the per-scene outcomes say what was left undone.
        /// </summary>
        public Func<string> StopReason;
    }

    /// <summary>What happened to one scene at one target.</summary>
    public sealed class SceneDeployOutcome
    {
        public string Scene;
        public bool Uploaded;

        /// <summary>
        /// The platform accepted the import (it answered "started"). The import itself runs on the platform
        /// afterwards: this says it was started, not that the environment is live.
        /// </summary>
        public bool ImportStarted;

        /// <summary>For a world: whether the scene's catalog was linked to its interpreted assembly (null when nothing was linked or unlinked).</summary>
        public bool? Linked;

        /// <summary>Why the scene's import was not started, or null when it was.</summary>
        public string NotPublishedBecause;

        /// <summary>Set when the deploy was stopped before the scene's link step, which was left undone.</summary>
        public string NotLinkedBecause;
    }

    /// <summary>What happened to one interpreted assembly at one world.</summary>
    public sealed class AssemblyDeployOutcome
    {
        public string Name;
        public bool Uploaded;
        public bool Accepted;

        /// <summary>True when there was no bundle in the output folder, so nothing was sent.</summary>
        public bool NotSent;

        /// <summary>The platform's reason when it refused the assembly, or why it was not sent.</summary>
        public string Reason;
    }

    /// <summary>What a deploy did at one world, or at tenant level (<see cref="WorldId"/> null).</summary>
    public sealed class DeployTargetOutcome
    {
        public int? WorldId;
        public string Label;

        /// <summary>False when the platform gave no upload link and the target was skipped.</summary>
        public bool UploadLinkObtained;

        /// <summary>The interpreted assemblies the scene zips declare. Empty at tenant level: scripts do not run there.</summary>
        public List<AssemblyDeployOutcome> Assemblies = new();

        public List<SceneDeployOutcome> Scenes = new();

        /// <summary>Why the whole target was skipped, or null.</summary>
        public string Failure;
    }

    /// <summary>What <see cref="EnvironmentsDeploy"/> did.</summary>
    public sealed class DeployReport
    {
        public List<DeployTargetOutcome> Targets = new();

        /// <summary>
        /// Scene imports the platform accepted ("started"), over every target. Not a count of live
        /// environments: the import runs on the platform after it answers.
        /// </summary>
        public int ImportsStarted;

        /// <summary>Scene imports there would be had nothing gone wrong: targets times scenes.</summary>
        public int Expected;

        /// <summary>How many problems were reported to <see cref="DeployObserver.Error"/>.</summary>
        public int Problems;

        /// <summary>Set when the deploy could not even start (no API URL, no session): nothing was sent.</summary>
        public string Fatal;

        /// <summary>
        /// Set when <see cref="DeployObserver.StopReason"/> ended the deploy early: the reason, and the step it
        /// stopped before. Everything not started is marked in <see cref="Targets"/>.
        /// </summary>
        public string Stopped;

        public bool Succeeded => Fatal == null && Problems == 0 && Stopped == null;
    }

    /// <summary>What a deploy to a place would do, as far as the platform's list of published environments says.</summary>
    public enum DeployPreviewState
    {
        /// <summary>No scene is included in the build.</summary>
        NothingIncluded,

        /// <summary>The list of published environments is still being read.</summary>
        Checking,

        /// <summary>It could not be read.</summary>
        Unreadable,

        /// <summary>The lists are valid.</summary>
        Ready,
    }

    /// <summary>The scenes that would arrive as new environments and the ones that would overwrite one with the same name.</summary>
    public sealed class DeployPreview
    {
        public DeployPreviewState State;

        /// <summary>How many scenes are included in the build.</summary>
        public int Included;

        /// <summary>Scenes whose name is already published there.</summary>
        public List<string> Updated = new();

        /// <summary>Scenes not published there yet.</summary>
        public List<string> Added = new();
    }

    /// <summary>
    /// What the Addressables management window's world list, deploy preview and deploy buttons do,
    /// without the window: list the worlds the login can deploy to, say what a deploy would add and
    /// update, and upload and import the built scene zips (and the interpreted assembly) to worlds
    /// or to the tenant. The window and <see cref="EnvironmentsCli"/> both call it.
    /// </summary>
    /// <remarks>
    /// Every request goes through <see cref="EditorSessionManager"/>, which renews the session before
    /// each call and, in batch mode, never falls back to an interactive login.
    /// </remarks>
    public static class EnvironmentsDeploy
    {
        private static readonly HttpClient httpClient = new();

        /// <summary>How long one zip upload may take before it is given up. The default 100 s ends an upload of a large zip on a slow line.</summary>
        private static readonly TimeSpan upload_timeout = TimeSpan.FromMinutes(30);

        /// <summary>Apart from <see cref="httpClient"/> so that the long timeout does not apply to the API calls.</summary>
        private static readonly HttpClient uploadClient = new() { Timeout = upload_timeout };

        private static readonly string[] deploy_roles = { "TenantManager", "EnvironmentManager", "Owner" };

        #region Worlds

        /// <summary>
        /// The worlds the login can see, and the ones among them it can deploy to. Silent renewal
        /// only: the window calls it just from being opened, and popping a browser login at someone
        /// who only clicked a menu item would be startling.
        /// </summary>
        /// <param name="progress">Told what the call is doing ("Checking permissions...").</param>
        /// <param name="onListed">Called with every visible world as soon as the listing arrives, before the roles are
        /// read: the window starts its "where is it published" lookup there.</param>
        /// <param name="connectionKey">The connection to list for; null is the project's default one, read once here.</param>
        public static async Task<WorldListResult> LoadWorldsAsync(Action<string> progress = null,
                                                                  Action<IReadOnlyList<PublishableWorld>> onListed = null,
                                                                  string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            WorldListResult result = new();

            try
            {
                string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
                if (string.IsNullOrEmpty(applicationApiUrl))
                {
                    result.Status = WorldListStatus.NoApiUrl;
                    result.Message = "Error: no application API URL in tenant config";
                    return result;
                }

                string apiUrl = $"{applicationApiUrl}/worlds?api-version=2";

                var response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, 
                    () => new HttpRequestMessage(HttpMethod.Get, apiUrl), httpClient, allowInteractive: false);

                if (response == null)
                {
                    result.Status = WorldListStatus.SessionRequired;
                    result.Message = "Session expired. Log in again to load the worlds.";
                    return result;
                }

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[AddressablesManagement] Failed to get worlds: {response.StatusCode} - {body}");
                    result.Status = WorldListStatus.Failed;
                    result.Message = $"Error loading worlds: {response.StatusCode}";
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                result.Visible = JsonConvert.DeserializeObject<List<PublishableWorld>>(json) ?? new();
                onListed?.Invoke(result.Visible);

                // Filter worlds by user roles
                progress?.Invoke("Checking permissions...");

                foreach (var world in result.Visible)
                {
                    try
                    {
                        string rolesUrl = $"{applicationApiUrl}/worlds/{world.Id}/users/my/roles?api-version=2";
                        var rolesResponse = await EditorSessionManager.SendAuthorizedAsync(connectionKey, 
                            () => new HttpRequestMessage(HttpMethod.Get, rolesUrl), httpClient, allowInteractive: false);

                        if (rolesResponse == null)
                        {
                            result.Status = WorldListStatus.SessionRequired;
                            result.Message = "Session expired. Log in again to load the worlds.";
                            result.Deployable.Clear();
                            return result;
                        }

                        if (rolesResponse.IsSuccessStatusCode)
                        {
                            string rolesJson = await rolesResponse.Content.ReadAsStringAsync();
                            List<string> roles = JsonConvert.DeserializeObject<List<string>>(rolesJson) ?? new();

                            if (roles.Any(r => deploy_roles.Contains(r)))
                                result.Deployable.Add(world);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AddressablesManagement] Could not check roles for world {world.Id}: {ex.Message}");
                    }
                }

                result.Status = WorldListStatus.Ok;
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error loading worlds: {ex.Message}");
                result.Status = WorldListStatus.Failed;
                result.Message = $"Error: {ex.Message}";
                return result;
            }
        }

        #endregion

        #region Preview

        /// <summary>
        /// What deploying the <paramref name="included"/> scenes to a world (or to tenant level, when
        /// <paramref name="worldId"/> is null) does: the scenes that arrive as new environments and
        /// the ones that overwrite an environment already published with that name. Reads the list
        /// <see cref="PublishedEnvironmentsIndex"/> holds; the caller refreshes it first.
        /// </summary>
        public static DeployPreview Preview(IReadOnlyList<SceneConfiguration> included, int? worldId)
        {
            DeployPreview preview = new() { Included = included?.Count ?? 0 };

            if (preview.Included == 0)
            {
                preview.State = DeployPreviewState.NothingIncluded;
                return preview;
            }

            if (!PublishedEnvironmentsIndex.HasData)
            {
                preview.State = PublishedEnvironmentsIndex.IsLoading ? DeployPreviewState.Checking : DeployPreviewState.Unreadable;
                return preview;
            }

            preview.State = DeployPreviewState.Ready;
            foreach (SceneConfiguration cfg in included)
            {
                (PublishedEnvironmentsIndex.IsPublishedIn(cfg.SceneNameFiltered, worldId) ? preview.Updated : preview.Added).Add(cfg.Scene.name);
            }

            return preview;
        }

        #endregion

        #region Deploy

        /// <summary>
        /// Uploads and imports the built zips of <paramref name="scenes"/> to each world, the
        /// interpreted assembly first. Renews the session before the first upload rather than
        /// discovering it is dead on the first call. A problem at one scene or world is reported
        /// and the deploy goes on with the next; <see cref="DeployReport.Problems"/> counts them.
        /// </summary>
        /// <remarks>
        /// <see cref="DeployObserver.StopReason"/> ends it early, between steps: a scene's upload and
        /// import are one step and are never cut in half.
        /// </remarks>
        /// <param name="worldIds">The worlds to deploy to.</param>
        /// <param name="worlds">Where their labels are read from; an id not in it is labelled by the id.</param>
        /// <param name="scenes">The scenes' <see cref="SceneConfiguration.SceneNameFiltered"/> names, whose zips are in the output folder.</param>
        public static async Task<DeployReport> DeployToWorldsAsync(IReadOnlyList<int> worldIds, IReadOnlyList<PublishableWorld> worlds,
                                                                   IReadOnlyList<string> scenes, DeployObserver observer = null,
                                                                   string connectionKey = null)
        {
            // One connection for the whole deploy, taken once here: a token must not follow another connection's choice
            // (the default one can change while it runs) to another tenant's host.
            connectionKey ??= ProjectConnections.DefaultKey;
            DeployReport report = new();
            observer?.Started?.Invoke(report);
            void Fail(string message) { report.Problems++; (observer?.Error ?? (m => Debug.LogError(m)))(message); }
            bool StopBefore(string step) => ShouldStop(report, observer, step);

            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(applicationApiUrl))
            {
                report.Fatal = "No application API URL available.";
                Fail(report.Fatal);
                return report;
            }

            // Renew the session before the uploads start rather than discovering it is dead on
            // the first call: the renewal can need an interactive login, and that is far less
            // disruptive here than halfway through a multi-world deploy. Every call below still
            // re-checks on its own — this deploy can outlive the token it starts with.
            if (!await EditorSessionManager.EnsureValidTokenAsync(connectionKey))
            {
                report.Fatal = "Could not establish a valid session. Nothing was deployed.";
                Fail(report.Fatal);
                return report;
            }

            int totalWorlds = worldIds.Count;
            report.Expected = totalWorlds * scenes.Count;

            try
            {
                for (int w = 0; w < totalWorlds; w++)
                {
                    int worldId = worldIds[w];
                    PublishableWorld world = worlds?.FirstOrDefault(wd => wd.Id == worldId);
                    string worldLabel = world?.Label ?? worldId.ToString();

                    if (StopBefore($"world \"{worldLabel}\""))
                    {
                        for (int rest = w; rest < totalWorlds; rest++)
                            report.Targets.Add(NotStartedTarget(worldIds[rest], worlds, scenes));
                        break;
                    }

                    DeployTargetOutcome target = new() { WorldId = worldId, Label = worldLabel };
                    report.Targets.Add(target);

                    string progressPrefix = $"Deploy ({w + 1}/{totalWorlds}) - World \"{worldLabel}\"";

                    observer?.Progress?.Invoke(progressPrefix, "Getting upload link...", (float)w / totalWorlds);

                    string uploadLink = await GetUploadLinkForWorld(applicationApiUrl, worldId, connectionKey);
                    if (string.IsNullOrEmpty(uploadLink))
                    {
                        target.Failure = "failed to get upload link";
                        Fail($"World \"{worldLabel}\": failed to get upload link. Skipping.");
                        continue;
                    }

                    target.UploadLinkObtained = true;

                    bool useFtp = ConnectionStore.Get(connectionKey)?.Tenant?.Env == Env.Sandbox;
                    FtpUploadConfig ftpConfig = null;

                    if (useFtp)
                    {
                        try
                        {
                            ftpConfig = JsonConvert.DeserializeObject<FtpUploadConfig>(uploadLink);
                            if (ftpConfig == null || string.IsNullOrEmpty(ftpConfig.host))
                            {
                                target.Failure = "invalid SFTP config";
                                Fail($"World \"{worldLabel}\": invalid SFTP config. Skipping.");
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            target.Failure = "invalid SFTP config";
                            Fail($"World \"{worldLabel}\": failed to parse SFTP config — {ex.Message}. Skipping.");
                            continue;
                        }
                    }

                    // Before the scenes: the archive import resolves the assembly each scene zip
                    // declares, so the assembly has to be registered when the first scene arrives.
                    target.Assemblies = await ImportDeclaredAssembliesAsync(
                        connectionKey, applicationApiUrl, worldId, worldLabel, scenes, uploadLink, useFtp, ftpConfig, progressPrefix, observer, Fail, StopBefore);

                    // The ones the platform did not take: not the ones with no bundle to send, which the
                    // scenes may still import against if the assembly is already registered.
                    HashSet<string> rejectedAssemblies = new(
                        target.Assemblies.Where(a => !a.Accepted && !a.NotSent).Select(a => a.Name), StringComparer.Ordinal);

                    List<string> importedScenes = new();

                    for (int s = 0; s < scenes.Count; s++)
                    {
                        string scene = scenes[s];

                        if (StopBefore($"scene \"{scene}\" at world \"{worldLabel}\""))
                        {
                            for (int rest = s; rest < scenes.Count; rest++)
                                target.Scenes.Add(new SceneDeployOutcome { Scene = scenes[rest], NotPublishedBecause = NotStartedBecause });
                            break;
                        }

                        SceneDeployOutcome outcome = new() { Scene = scene };
                        target.Scenes.Add(outcome);

                        string zipPath = Path.Combine(EnvironmentsBuild.OutputFolder, scene + ".zip");
                        if (!File.Exists(zipPath))
                        {
                            outcome.NotPublishedBecause = "zip not found";
                            Fail($"World \"{worldLabel}\": zip not found for \"{scene}\". Skipping scene.");
                            continue;
                        }

                        string declared = EnvironmentsBuild.ReadDeclaredAssembly(scene, out _);
                        if (declared != null && rejectedAssemblies.Contains(declared))
                        {
                            // Publishing it would hand the world a scene whose scripts were turned down.
                            outcome.NotPublishedBecause = "its interpreted assembly was not accepted";
                            Fail($"World \"{worldLabel}\": \"{scene}\" was not published, because its interpreted " +
                                 "assembly was not accepted (see above).");
                            continue;
                        }

                        float progress = ((float)w + (float)s / scenes.Count) / totalWorlds;

                        observer?.Progress?.Invoke(progressPrefix, $"Uploading {scene}.zip ({s + 1}/{scenes.Count})...", progress);

                        bool uploaded = useFtp
                            ? await UploadZipViaFtp(zipPath, ftpConfig)
                            : await UploadZip(zipPath, uploadLink);

                        outcome.Uploaded = uploaded;
                        if (!uploaded)
                        {
                            outcome.NotPublishedBecause = "upload failed";
                            Fail($"World \"{worldLabel}\": upload failed for \"{scene}.zip\". Skipping import.");
                            continue;
                        }

                        observer?.Progress?.Invoke(progressPrefix, $"Importing {scene} ({s + 1}/{scenes.Count})...", progress);

                        bool imported = await ImportSceneToWorld(applicationApiUrl, worldId, scene, connectionKey);
                        outcome.ImportStarted = imported;
                        if (imported)
                        {
                            importedScenes.Add(scene);
                            report.ImportsStarted++;
                        }
                        else
                        {
                            outcome.NotPublishedBecause = "import failed";
                            Fail($"World \"{worldLabel}\": import failed for \"{scene}\". Check the console for details.");
                        }
                    }

                    await LinkDeclaredAssembliesAsync(connectionKey, applicationApiUrl, worldLabel, worldId, importedScenes, target, Fail, StopBefore);

                    if (report.Stopped != null)
                    {
                        for (int rest = w + 1; rest < totalWorlds; rest++)
                            report.Targets.Add(NotStartedTarget(worldIds[rest], worlds, scenes));
                        break;
                    }
                }
            }
            finally
            {
                observer?.ProgressDone?.Invoke();
            }

            _ = PublishedEnvironmentsIndex.RefreshAsync();
            return report;
        }

        /// <summary>
        /// Uploads and imports the built zips of <paramref name="scenes"/> at tenant level. Scripts
        /// are not part of it: an environment published at tenant level runs without its interpreted
        /// scripts, and the assembly is neither uploaded nor linked. Stops between scenes on
        /// <see cref="DeployObserver.StopReason"/>, like <see cref="DeployToWorldsAsync"/>.
        /// </summary>
        public static async Task<DeployReport> DeployToTenantAsync(IReadOnlyList<string> scenes, DeployObserver observer = null,
                                                                   string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            DeployReport report = new();
            observer?.Started?.Invoke(report);
            void Fail(string message) { report.Problems++; (observer?.Error ?? (m => Debug.LogError(m)))(message); }
            bool StopBefore(string step) => ShouldStop(report, observer, step);

            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(applicationApiUrl))
            {
                report.Fatal = "Tenant: no application API URL available.";
                Fail(report.Fatal);
                return report;
            }

            // See DeployToWorldsAsync: renew before the uploads, not on the first failure.
            if (!await EditorSessionManager.EnsureValidTokenAsync(connectionKey))
            {
                report.Fatal = "Tenant: could not establish a valid session. Nothing was deployed.";
                Fail(report.Fatal);
                return report;
            }

            report.Expected = scenes.Count;
            DeployTargetOutcome target = new() { WorldId = null, Label = "Tenant level" };
            report.Targets.Add(target);

            try
            {
                if (StopBefore("the tenant's upload link"))
                {
                    target.Scenes.AddRange(scenes.Select(scene => new SceneDeployOutcome { Scene = scene, NotPublishedBecause = NotStartedBecause }));
                    return report;
                }

                observer?.Progress?.Invoke("Deploy to Tenant", "Getting upload link...", 0f);

                string uploadLink = await GetTenantUploadLink(applicationApiUrl, connectionKey);
                if (string.IsNullOrEmpty(uploadLink))
                {
                    target.Failure = "failed to get upload link";
                    Fail("Tenant: failed to get upload link.");
                    return report;
                }

                target.UploadLinkObtained = true;

                bool useFtp = ConnectionStore.Get(connectionKey)?.Tenant?.Env == Env.Sandbox;
                FtpUploadConfig ftpConfig = null;

                if (useFtp)
                {
                    try
                    {
                        ftpConfig = JsonConvert.DeserializeObject<FtpUploadConfig>(uploadLink);
                        if (ftpConfig == null || string.IsNullOrEmpty(ftpConfig.host))
                        {
                            target.Failure = "invalid SFTP config";
                            Fail("Tenant: invalid SFTP config. Aborting.");
                            return report;
                        }
                    }
                    catch (Exception ex)
                    {
                        target.Failure = "invalid SFTP config";
                        Fail($"Tenant: failed to parse SFTP config — {ex.Message}. Aborting.");
                        return report;
                    }
                }

                for (int s = 0; s < scenes.Count; s++)
                {
                    string scene = scenes[s];

                    if (StopBefore($"scene \"{scene}\" at tenant level"))
                    {
                        for (int rest = s; rest < scenes.Count; rest++)
                            target.Scenes.Add(new SceneDeployOutcome { Scene = scenes[rest], NotPublishedBecause = NotStartedBecause });
                        break;
                    }

                    SceneDeployOutcome outcome = new() { Scene = scene };
                    target.Scenes.Add(outcome);

                    string zipPath = Path.Combine(EnvironmentsBuild.OutputFolder, scene + ".zip");
                    if (!File.Exists(zipPath))
                    {
                        outcome.NotPublishedBecause = "zip not found";
                        Fail($"Tenant: zip not found for \"{scene}\". Skipping scene.");
                        continue;
                    }

                    float progress = (float)s / scenes.Count;

                    observer?.Progress?.Invoke("Deploy to Tenant", $"Uploading {scene}.zip ({s + 1}/{scenes.Count})...", progress);

                    bool uploaded = useFtp
                        ? await UploadZipViaFtp(zipPath, ftpConfig)
                        : await UploadZip(zipPath, uploadLink);

                    outcome.Uploaded = uploaded;
                    if (!uploaded)
                    {
                        outcome.NotPublishedBecause = "upload failed";
                        Fail($"Tenant: upload failed for \"{scene}.zip\". Skipping import.");
                        continue;
                    }

                    observer?.Progress?.Invoke("Deploy to Tenant", $"Importing {scene} ({s + 1}/{scenes.Count})...", progress);

                    bool imported = await ImportSceneToTenant(applicationApiUrl, scene, connectionKey);
                    outcome.ImportStarted = imported;
                    if (imported)
                        report.ImportsStarted++;
                    else
                    {
                        outcome.NotPublishedBecause = "import failed";
                        Fail($"Tenant: import failed for \"{scene}\". Check the console for details.");
                    }
                }
            }
            finally
            {
                observer?.ProgressDone?.Invoke();
            }

            _ = PublishedEnvironmentsIndex.RefreshAsync();
            return report;
        }

        private const string NotStartedBecause = "not started: the deploy was stopped before it";

        /// <summary>
        /// True once the observer asked to stop (and from then on): records the reason and the step it
        /// stopped before in <see cref="DeployReport.Stopped"/>, the first time only.
        /// </summary>
        private static bool ShouldStop(DeployReport report, DeployObserver observer, string step)
        {
            if (report.Stopped != null)
                return true;

            string reason = observer?.StopReason?.Invoke();
            if (reason == null)
                return false;

            report.Stopped = $"{reason} Stopped before {step}.";
            Debug.LogWarning($"[AddressablesManagement] Deploy stopped: {report.Stopped}");
            return true;
        }

        /// <summary>A world the deploy never reached: every scene is marked as not started.</summary>
        private static DeployTargetOutcome NotStartedTarget(int worldId, IReadOnlyList<PublishableWorld> worlds, IReadOnlyList<string> scenes)
        {
            DeployTargetOutcome target = new()
            {
                WorldId = worldId,
                Label = worlds?.FirstOrDefault(wd => wd.Id == worldId)?.Label ?? worldId.ToString(),
                Failure = NotStartedBecause,
            };
            target.Scenes.AddRange(scenes.Select(scene => new SceneDeployOutcome { Scene = scene, NotPublishedBecause = NotStartedBecause }));
            return target;
        }

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
        /// Returns one outcome per assembly; those with <see cref="AssemblyDeployOutcome.Accepted"/>
        /// false and a bundle that was sent are the ones the platform did not accept, so the caller
        /// can hold back the scenes that need them.
        /// </summary>
        private static async Task<List<AssemblyDeployOutcome>> ImportDeclaredAssembliesAsync(
            string connectionKey, string applicationApiUrl, int worldId, string worldLabel, IReadOnlyList<string> scenes, string uploadLink,
            bool useFtp, FtpUploadConfig ftpConfig, string progressPrefix, DeployObserver observer, Action<string> fail,
            Func<string, bool> stopBefore)
        {
            List<AssemblyDeployOutcome> outcomes = new();

            IEnumerable<string> declared = scenes
                .Select(scene => EnvironmentsBuild.ReadDeclaredAssembly(scene, out _))
                .Where(name => name != null)
                .Distinct(StringComparer.Ordinal);

            foreach (string assemblyName in declared)
            {
                AssemblyDeployOutcome outcome = new() { Name = assemblyName };
                outcomes.Add(outcome);

                if (stopBefore($"the interpreted assembly '{assemblyName}' at world \"{worldLabel}\""))
                {
                    outcome.NotSent = true;
                    outcome.Reason = NotStartedBecause;
                    continue;
                }

                string zipPath = Path.Combine(EnvironmentsBuild.OutputFolder, assemblyName + ".zip");
                if (!File.Exists(zipPath))
                {
                    // The build could not produce the bundle and said why. The scenes still go: the
                    // platform accepts them where this assembly is already registered.
                    outcome.NotSent = true;
                    outcome.Reason = $"no bundle in {EnvironmentsBuild.OutputFolder}: the scenes that need it import only if it is already registered";
                    Debug.LogWarning($"[AddressablesManagement] World \"{worldLabel}\": no bundle for '{assemblyName}' in " +
                                     $"{EnvironmentsBuild.OutputFolder}. The scenes that need it import only if it is already registered.");
                    continue;
                }

                observer?.Progress?.Invoke(progressPrefix, "Uploading interpreted assembly...", 0f);

                bool uploaded = useFtp
                    ? await UploadZipViaFtp(zipPath, ftpConfig)
                    : await UploadZip(zipPath, uploadLink);

                outcome.Uploaded = uploaded;
                if (!uploaded)
                {
                    outcome.Reason = "upload failed";
                    fail($"World \"{worldLabel}\": upload failed for \"{Path.GetFileName(zipPath)}\".");
                    continue;
                }

                (bool imported, string reason) = await ImportEnvironmentDll(applicationApiUrl, worldId, Path.GetFileName(zipPath), connectionKey);
                if (!imported)
                {
                    outcome.Reason = reason;
                    fail($"World \"{worldLabel}\": the platform rejected the interpreted assembly '{assemblyName}': {reason}");
                    continue;
                }

                outcome.Accepted = true;

                // Only now: a marker written before the import would remember a failed publish as
                // done, and the next build would skip its compile and verification.
                EnvironmentsBuild.FindHotUpdateSetupperType()?
                    .GetMethod("MarkBundlePublished", BindingFlags.Public | BindingFlags.Static)?
                    .Invoke(null, null);
            }

            return outcomes;
        }

        /// <summary>
        /// Points each imported scene's catalog at the assembly it declares, or clears the link for
        /// a scene built without scripts. A platform that reads the declaration has already done
        /// this during the archive import, and repeating it changes nothing; one that predates it
        /// does not, and this is the only call that links its scenes.
        /// </summary>
        private static async Task LinkDeclaredAssembliesAsync(string connectionKey, string applicationApiUrl, string worldLabel, int worldId,
                                                              List<string> scenes, DeployTargetOutcome target, Action<string> fail,
                                                              Func<string, bool> stopBefore)
        {
            foreach (string scene in scenes)
            {
                if (stopBefore($"linking \"{scene}\" at world \"{worldLabel}\""))
                {
                    SceneDeployOutcome notLinked = target.Scenes.FirstOrDefault(o => o.Scene == scene);
                    if (notLinked != null)
                        notLinked.NotLinkedBecause = NotStartedBecause;
                    continue;
                }

                string assemblyName = EnvironmentsBuild.ReadDeclaredAssembly(scene, out bool declares);
                if (!declares)
                {
                    Debug.LogWarning($"[AddressablesManagement] World \"{worldLabel}\": \"{scene}\" was built before the " +
                                     "scene zips declared their interpreted assembly, so its script link was left as it was. " +
                                     "Build the scenes again.");
                    continue;
                }

                SceneDeployOutcome outcome = target.Scenes.FirstOrDefault(o => o.Scene == scene);

                // An empty body clears the link.
                bool linked = await LinkEnvironmentDll(applicationApiUrl, worldId, scene, assemblyName ?? string.Empty, connectionKey);
                if (outcome != null)
                    outcome.Linked = linked;
                if (!linked)
                    fail($"World \"{worldLabel}\": could not link the interpreted assembly to \"{scene}\".");
            }
        }

        #endregion

        #region API calls

        public static async Task<string> GetUploadLinkForWorld(string applicationApiUrl, int worldId, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/upload-link?api-version=2";

            HttpResponseMessage response;
            try
            {
                response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, 
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

        public static async Task<bool> ImportSceneToWorld(string applicationApiUrl, int worldId, string zipName, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environments/archives/import?api-version=2";

            HttpResponseMessage response;
            try
            {
                response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, () => new HttpRequestMessage(HttpMethod.Post, apiUrl)
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

        public static async Task<bool> UploadZip(string filePath, string sasUrl)
        {
            string fileName = Path.GetFileName(filePath);

            var uriBuilder = new UriBuilder(sasUrl);
            uriBuilder.Path = uriBuilder.Path.TrimEnd('/') + "/" + fileName;
            Uri uploadUri = uriBuilder.Uri;

            try
            {
                // Streamed from disk: a scene zip can be hundreds of megabytes, and none of it is held in memory.
                using FileStream file = File.OpenRead(filePath);
                using var content = new StreamContent(file);
                content.Headers.Add("x-ms-blob-type", "BlockBlob");

                HttpResponseMessage response = await uploadClient.PutAsync(uploadUri, content);
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

        public static async Task<bool> UploadZipViaFtp(string filePath, FtpUploadConfig ftpConfig)
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

        public static async Task<string> GetTenantUploadLink(string applicationApiUrl, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/tenants/environments/upload-link?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, 
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

        /// <summary>
        /// Imports the uploaded assembly bundle. On failure, <c>Reason</c> says why in one line, for
        /// the deploy panel; the full response still goes to the Console.
        /// </summary>
        public static async Task<(bool Ok, string Reason)> ImportEnvironmentDll(string applicationApiUrl, int worldId, string zipName, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environment-dll/import?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, () => new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent($"\"{zipName}\"", Encoding.UTF8, "application/json")
                }, httpClient);

                if (response == null)
                {
                    Debug.LogError("[AddressablesManagement] Could not import the environment DLL: no valid session.");
                    return (false, "no valid session.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Read only on the way out: a 422 carries the whitelist violations, one per
                    // offending reference, each tagged with the platform DLL it came from — worth
                    // surfacing verbatim. The success body is the stored record, which is the
                    // platform's business and not something a creator's console should carry.
                    string body = await response.Content.ReadAsStringAsync();

                    Debug.LogError($"[AddressablesManagement] Environment DLL import failed: {response.StatusCode} - {body}");
                    return (false, DescribeDllImportFailure((int)response.StatusCode, body));
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Error importing the environment DLL: {ex.Message}");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// One line for the deploy panel out of the import's error body: the violations of a 422,
        /// each distinct one once with the platforms that raised it, or the detail and code of a
        /// ProblemDetails. Falls back to the status code when the body is neither.
        /// </summary>
        private static string DescribeDllImportFailure(int statusCode, string body)
        {
            try
            {
                Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.Parse(body);

                if (json["violations"] is Newtonsoft.Json.Linq.JArray violations && violations.Count > 0)
                {
                    IEnumerable<string> distinct = violations
                        .GroupBy(v => $"{(string)v["kind"]}: {(string)v["detail"]}")
                        .Select(g =>
                        {
                            string[] platforms = g.Select(v => (string)v["platform"]).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
                            return platforms.Length > 0 ? $"{g.Key} ({string.Join(", ", platforms)})" : g.Key;
                        });

                    return string.Join("; ", distinct);
                }

                string detail = (string)json["detail"] ?? (string)json["title"];
                string code = (string)json["code"];
                if (!string.IsNullOrEmpty(detail))
                    return string.IsNullOrEmpty(code) ? detail : $"{detail} [{code}]";
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Not JSON: the status code is all there is to say.
            }

            return $"HTTP {statusCode} (details in the Console).";
        }

        public static async Task<bool> LinkEnvironmentDll(string applicationApiUrl, int worldId,
                                                          string catalogName, string assemblyName, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/worlds/{worldId}/environments/catalog/{catalogName}/environment-dll?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, () => new HttpRequestMessage(HttpMethod.Put, apiUrl)
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

        public static async Task<bool> ImportSceneToTenant(string applicationApiUrl, string zipName, string connectionKey = null)
        {
            connectionKey ??= ProjectConnections.DefaultKey;
            string apiUrl = $"{applicationApiUrl}/tenants/environments/archives/import?api-version=2";

            try
            {
                var response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, () => new HttpRequestMessage(HttpMethod.Post, apiUrl)
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
    }
}
