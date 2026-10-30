using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The commands about an environment's thumbnail and localization file, on top of <see cref="EnvironmentFilesService"/>:
    /// <c>published</c>, <c>save-files</c>, <c>set-files</c>, <c>restore</c> and <c>scene-files</c>. A <i>place</i> is a world, or a tenant's
    /// tenant level, where a scene's environment exists online. The command line's counterpart of the Environment Details window.
    /// <list type="bullet">
    /// <item><description><c>published [-vdScenes a,b] [-vdConnection &lt;connection&gt;]</c>: every scene of the scene list (included in the build or not):
    /// the places it is published at, each environment's id, <c>canWrite</c>, the address, SHA-256 and project status of its thumbnail and
    /// localization file (<c>inProject</c>: the project's publish folder holds the same file; <c>onlineOnly</c>; <c>none</c>), and whether the tenant can
    /// receive files from the SDK (<c>writeSupport</c>). Read-only, works on every tenant. A file whose hash the platform does not say is downloaded and hashed.</description></item>
    /// <item><description><c>save-files [-vdScenes a,b] [-vdWorlds &lt;ids&gt; | -vdTenant] [-vdBackupFolder &lt;path&gt;] [-vdDryRun] [-vdConnection &lt;connection&gt;]</c>:
    /// saves the files the places hold into the project's publish folder (<c>Assets/Virtuademy/Editor/PublishFiles/</c>), never writing a file the project already holds
    /// (same SHA-256), and writes a manifest (<c>Backups/&lt;connection&gt;-&lt;time&gt;.json</c>) that <c>restore</c> reads. With <c>-vdBackupFolder</c> a full copy and its
    /// manifest go to that folder instead, and nothing to the project.</description></item>
    /// <item><description><c>set-files -vdExpectTenant &lt;label&gt; -vdScenes &lt;one&gt; [-vdThumbnail &lt;asset&gt;] [-vdLocalization &lt;asset&gt;]
    /// (-vdWorlds &lt;ids&gt; | -vdAllWorlds | -vdTenant) [-vdExcludeWorlds &lt;ids&gt;] [-vdDryRun] [-vdConnection &lt;connection&gt;]</c>: replaces the files of the scene's
    /// environment at those places with project assets (under <c>Assets/</c>). An image is prepared first (a JPEG of at most 2560 x 1440, never upscaled,
    /// transparency flattened on white; one already fit is sent as it is; any other is converted into a JPEG in the publish folder). A place that already holds the
    /// same file (same SHA-256) is reported <c>unchanged</c> and skipped. The file a place holds now is saved to the project first, unless the project already
    /// holds it; if that fails, nothing is replaced there. A file that fails its check stops everything with <see cref="ExitFileNotValid"/>.</description></item>
    /// <item><description><c>restore -vdExpectTenant &lt;label&gt; -vdManifest &lt;path&gt; [-vdScenes a,b] [-vdWorlds &lt;ids&gt; | -vdTenant] [-vdDryRun] [-vdConnection &lt;connection&gt;]</c>:
    /// puts back what a <c>save-files</c> manifest recorded, for the same connection, at the same environments (the hero image first); a file the manifest has as absent
    /// is left alone, as there is no way to remove one.</description></item>
    /// <item><description><c>scene-files -vdScenes &lt;one&gt; [-vdThumbnail &lt;asset&gt;] [-vdLocalization &lt;asset&gt;] [-vdClear]</c>: sets (or, with <c>-vdClear</c>, removes) the scene's
    /// default files in the scene list. Needs no network. A deploy sends them when it makes the scene's environment in a place (a new one) and never again:
    /// <c>deploy</c> waits for the environment to appear in the place's list (up to <see cref="EnvironmentFilesService.DefaultsWaitSeconds"/> seconds), then sends them, and says what it did in
    /// <c>deploy.targets[].scenes[].defaultFiles</c>; <c>-vdDryRun</c> reports what it would send in <c>defaultFiles</c>. Without options it shows the scene's defaults.</description></item>
    /// </list>
    /// <para>
    /// <b>Default files in <c>preview</c> and <c>deploy</c></b> (<c>-vdDryRun</c> included): before anything is built or sent they check each new scene's default
    /// files and report them in <c>defaultFiles</c>; a file that is not valid is a warning there, is not uploaded afterwards, and does not stop the deploy. The deploy
    /// then sends the defaults of the scenes that were new at a place once their environment shows up in the place's list, and says what it did in
    /// <c>deploy.targets[].scenes[].defaultFiles</c>.
    /// </para>
    /// Writes need a tenant on a platform version that has the routes (<c>writeSupport</c>), known before any request; the messages never name a version.
    /// <see cref="ExitFileNotValid"/> means a file would be refused by the platform: nothing was sent. <see cref="ExitTargetsNotChanged"/> means one or more places
    /// were left as they were: refused (<c>canWrite</c> false), unsupported, or failed.
    /// </summary>
    public static partial class EnvironmentsCli
    {
        private static readonly string[] files_commands = { "published", "save-files", "set-files", "restore", "scene-files" };

        #region Arguments

        private static bool TryValidateFilesArguments(Options options, Dictionary<string, string> values, out string error)
        {
            error = null;
            if (!files_commands.Contains(command))
                return true;

            values.TryGetValue("-vdExcludeWorlds", out string excluded);
            if (!TryParseIds(excluded, "-vdExcludeWorlds", out options.ExcludeWorlds, out error))
                return false;

            if (options.Worlds != null && options.Tenant)
            {
                error = $"{command} takes -vdWorlds or -vdTenant, not both: run it once for each.";
                return false;
            }

            if (command == "set-files")
            {
                if (options.Scenes == null || options.Scenes.Split(',').Count(s => s.Trim().Length > 0) != 1)
                {
                    error = "set-files needs -vdScenes <one scene>.";
                    return false;
                }

                if (options.Thumbnail == null && options.Localization == null)
                {
                    error = "set-files needs -vdThumbnail <asset>, -vdLocalization <asset> or both.";
                    return false;
                }

                int choices = (options.Worlds != null ? 1 : 0) + (options.Tenant ? 1 : 0) + (options.AllWorlds ? 1 : 0);
                if (choices != 1)
                {
                    error = "set-files needs exactly one of -vdWorlds <ids>, -vdAllWorlds or -vdTenant: the target is always explicit.";
                    return false;
                }

                if (options.ExcludeWorlds != null && !options.AllWorlds)
                {
                    error = "-vdExcludeWorlds goes with -vdAllWorlds.";
                    return false;
                }
            }

            if (command == "restore" && string.IsNullOrEmpty(options.Manifest))
            {
                error = "restore needs -vdManifest <path>: the manifest save-files wrote.";
                return false;
            }

            if (command == "scene-files")
            {
                if (options.Scenes == null || options.Scenes.Split(',').Count(s => s.Trim().Length > 0) != 1)
                {
                    error = "scene-files needs -vdScenes <one scene>.";
                    return false;
                }

                if (options.Clear && (options.Thumbnail != null || options.Localization != null))
                {
                    error = "scene-files takes -vdClear or the files to set, not both.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseIds(string text, string option, out List<int> ids, out string error)
        {
            ids = null;
            error = null;
            if (text == null)
                return true;

            ids = new List<int>();
            foreach (string part in text.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                if (!int.TryParse(part, out int id))
                {
                    error = $"{option} takes world ids, separated by commas: '{part}' is not one.";
                    return false;
                }

                if (!ids.Contains(id))
                    ids.Add(id);
            }

            if (ids.Count == 0)
            {
                error = $"{option} names no world.";
                return false;
            }

            return true;
        }

        #endregion

        #region Shared steps

        /// <summary>Every scene of the scene list, included in the build or not, or the ones <c>-vdScenes</c> names (by scene name or environment name).</summary>
        private static int SelectFilesScenes(Options options, out List<SceneRef> scenes)
        {
            scenes = new List<SceneRef>();
            SceneListScriptableObject list = SceneListScriptableObject.Find();
            if (list == null)
            {
                string message = "The project has no scene list yet. Open Virtuademy > Publish Environments once to create it.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitNothingToBuild;
            }

            List<SceneRef> all = EnvironmentFilesService.ScenesOf(list);
            if (options.Scenes == null)
            {
                scenes = all;
            }
            else
            {
                List<string> unknown = new();
                foreach (string name in options.Scenes.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                {
                    SceneRef match = all.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        unknown.Add(name);
                    else if (!scenes.Contains(match))
                        scenes.Add(match);
                }

                if (unknown.Count > 0 || scenes.Count == 0)
                {
                    string message = $"-vdScenes names scenes that are not in the scene list: {string.Join(", ", unknown)}. In the list: {string.Join(", ", all.Select(s => s.Name))}.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    scenes = new List<SceneRef>();
                    return ExitNothingToBuild;
                }
            }

            report["scenes"] = scenes.Select(s => new Dictionary<string, object> { { "name", s.Name }, { "addressName", s.Key }, { "includedInBuild", s.IncludedInBuild } }).ToList();
            return ExitDone;
        }

        /// <summary>The connection, the creator-kit app, the target, the platform version and the session, in the order the other commands check them.</summary>
        private static async Task<int> FilesPreludeAsync(Options options, bool write)
        {
            int code = UseNamedConnection(options.Connection);
            if (code == ExitDone)
                code = RequireConnection();
            if (code == ExitDone)
                code = await RequireCreatorKitAppAsync();
            if (code != ExitDone)
                return code;

            code = ConfirmTarget(options.Connection, options.ExpectTenant, required: write);
            if (code != ExitDone)
                return code;

            if (write)
            {
                // The version rule of writes is the platform's: read the connection again, so the support is the tenant's today.
                await ConnectionLoginService.RefreshConnectionAsync(targetKey);
                code = CheckCreatorKitApp();
                if (code != ExitDone)
                    return code;
            }

            return await RequireSessionAsync();
        }

        private static void ReportSupport()
        {
            string version = ConnectionStore.Get(targetKey)?.PlatformVersion;
            WriteSupport support = EnvironmentFilesService.SupportOfVersion(version);
            report["platformVersion"] = new Dictionary<string, object> { { "tenant", version } };
            report["writeSupport"] = support.ToString().ToLowerInvariant();
            if (support == WriteSupport.NotSupported)
                report["writeSupportMessage"] = EnvironmentFilesService.UnsupportedMessage;
        }

        private static async Task<(int Code, FilesListing Listing)> ListFilesAsync(IReadOnlyList<SceneRef> scenes)
        {
            FilesListing listing = await EnvironmentFilesService.ListAsync(targetKey, scenes, readFiles: true, progress: message => Debug.Log(log_prefix + message));
            if (listing.Status == ListingStatus.Ok)
            {
                report["places"] = new Dictionary<string, object>
                {
                    { "worlds", listing.Worlds.Count },
                    { "tenantLevelVisible", !listing.TenantListUnreadable },
                    { "environments", listing.AllTargets.Count() },
                };
                return (ExitDone, listing);
            }

            Debug.LogError(log_prefix + listing.Message);
            report["error"] = listing.Message;
            return (listing.Status == ListingStatus.SessionRequired ? ExitLoginRequired : ExitPlatformUnreachable, listing);
        }

        private static PlaceSelection SelectionOf(Options options, IEnumerable<SceneRef> scenes)
        {
            PlaceSelection selection = new() { SceneKeys = options.Scenes == null ? null : new HashSet<string>(scenes.Select(s => s.Key), StringComparer.OrdinalIgnoreCase) };
            if (options.Worlds != null)
                selection.Worlds = new HashSet<int>(options.Worlds);
            else if (options.Tenant)
                selection.Tenant = true;
            else if (options.AllWorlds)
                selection.AllWorlds = true;
            else
                selection.AllPlaces = true;

            if (options.ExcludeWorlds != null)
                selection.ExcludedWorlds = new HashSet<int>(options.ExcludeWorlds);

            return selection;
        }

        /// <summary>A world id the account cannot see is <see cref="ExitWorldNotDeployable"/>, as in a deploy; a tenant target needs a TenantManager (<see cref="ExitNotTenantManager"/>) when it is to be written.</summary>
        private static int CheckFilesTargets(Options options, FilesListing listing, bool write)
        {
            if (options.Worlds != null)
            {
                List<int> unknown = options.Worlds.Where(id => listing.Worlds.All(w => w.Id != id)).ToList();
                if (unknown.Count > 0)
                {
                    string message = $"This login cannot see world(s) {string.Join(", ", unknown)}. Visible: {(listing.Worlds.Count == 0 ? "none" : string.Join(", ", listing.Worlds.Select(w => $"{w.Id} ({w.Label})")))}. Nothing was sent.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    return ExitWorldNotDeployable;
                }
            }

            if (write && options.Tenant && ConnectionStore.Get(targetKey)?.IsTenantManager != true)
            {
                string message = "Changing a tenant-level environment needs a TenantManager login; this one is not.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitNotTenantManager;
            }

            return ExitDone;
        }

        /// <summary>The one line <c>preview</c> and <c>deploy</c> run before they build or send: checks the scenes' default files and puts them in the report.</summary>
        private static async Task ReportDefaultFilesAsync(Options options, IEnumerable<SceneListScriptableObject.SceneConfiguration> scenes, WorldListResult listing) =>
            report["defaultFiles"] = await DefaultFilesDryRunAsync(options, scenes.Select(s => s.SceneNameFiltered).ToList(), listing);

        private static string ResultName(OutcomeKind kind) => char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString().Substring(1);

        private static Dictionary<string, object> PlaceReport(FilesTarget t) => new()
        {
            { "scene", t.Scene.Name },
            { "addressName", t.Scene.Key },
            { "target", t.IsTenant ? "tenant" : "world" },
            { "worldId", t.WorldId },
            { "worldLabel", t.WorldLabel },
            { "envId", t.EnvId },
            { "envLabel", t.EnvLabel },
        };

        private static Dictionary<string, object> StatusReport(FileStatus s) => new()
        {
            { "present", s.Present },
            { "uri", s.Present ? s.Uri : null },
            { "sha256", s.Sha256 },
            { "hashFromPlatform", s.Present && s.Sha256 != null ? s.HashFromPlatform : (bool?)null },
            { "presence", s.Presence.ToString().Substring(0, 1).ToLowerInvariant() + s.Presence.ToString().Substring(1) },
            { "projectAsset", s.ProjectAsset },
            { "width", s.Width > 0 ? s.Width : (int?)null },
            { "height", s.Height > 0 ? s.Height : (int?)null },
            { "format", s.Format },
            { "error", s.Error },
        };

        private static Dictionary<string, object> OutcomeReport(FileOutcome o)
        {
            Dictionary<string, object> entry = PlaceReport(o.Target);
            entry["file"] = o.Kind == FileKind.Thumbnail ? "thumbnail" : "localization";
            entry["result"] = ResultName(o.Result);
            entry["message"] = o.Message;
            entry["savedPrevious"] = o.SavedPrevious;
            entry["wouldSavePrevious"] = o.WouldSavePrevious ? true : (bool?)null;
            entry["warning"] = o.Warning;
            return entry;
        }

        private static Dictionary<string, object> PreparedReport(PreparedFile f) => new()
        {
            { "file", f.Kind == FileKind.Thumbnail ? "thumbnail" : "localization" },
            { "source", f.SourceAsset },
            { "uploadAsset", f.Bytes != null ? f.UploadAsset : null },
            { "converted", f.Converted },
            { "conversionPending", f.ConversionPending ? true : (bool?)null },
            { "conversion", f.ConversionNote },
            { "width", f.Width > 0 ? f.Width : (int?)null },
            { "height", f.Height > 0 ? f.Height : (int?)null },
            { "bytes", f.Bytes?.Length },
            { "sha256", f.Sha256 },
            { "errors", f.Check.Errors.Select(e => e.ToString()).ToList() },
            { "errorsTruncated", f.Check.ErrorsTruncated ? true : (bool?)null },
            { "warnings", f.Check.Warnings.Select(w => w.ToString()).ToList() },
        };

        /// <summary>The report and the exit code of a <see cref="ReplaceResult"/>: <see cref="ExitFileNotValid"/>, <see cref="ExitTargetsNotChanged"/> or done.</summary>
        private static int FinishReplace(ReplaceResult result, bool dryRun, List<string> extraNotChanged = null)
        {
            if (dryRun)
                report["dryRun"] = true;

            report["files"] = result.Files.Select(PreparedReport).ToList();
            if (result.Notes.Count > 0 || (extraNotChanged?.Count ?? 0) > 0)
                report["notes"] = result.Notes.Concat(extraNotChanged ?? new List<string>()).ToList();

            foreach (string note in result.Notes)
                Debug.LogWarning(log_prefix + note);

            foreach (PreparedFile file in result.Files)
            {
                string label = file.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file";
                foreach (FileIssue warning in file.Check.Warnings)
                    Debug.LogWarning($"{log_prefix}{file.SourceAsset} ({label}): {warning}");
                if (file.ConversionNote != null)
                    Debug.Log($"{log_prefix}{file.SourceAsset}: {file.ConversionNote}");
            }

            if (result.Invalid)
            {
                report["invalidFiles"] = result.Files.Where(f => !f.Check.Valid).Select(PreparedReport).ToList();
                string message = $"{result.Files.Count(f => !f.Check.Valid)} file(s) would be refused by the platform: nothing was sent. " +
                                 string.Join(" ", result.Files.Where(f => !f.Check.Valid).Select(f => $"{f.SourceAsset}: {string.Join("; ", f.Check.Errors.Take(5).Select(e => e.ToString()))}" +
                                                                                                    (f.Check.Errors.Count > 5 ? $" (and {f.Check.Errors.Count - 5} more)" : string.Empty)));
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitFileNotValid;
            }

            report["outcomes"] = result.Outcomes.Select(OutcomeReport).ToList();
            report["summary"] = result.Outcomes.GroupBy(o => ResultName(o.Result)).ToDictionary(g => g.Key, g => (object)g.Count());

            foreach (FileOutcome outcome in result.Outcomes)
                Debug.Log($"{log_prefix}{outcome.Target.Scene.Name}, {outcome.Target.Place}, {(outcome.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file")}: {ResultName(outcome.Result)}. {outcome.Message}");

            bool problems = result.AnyNotChanged || (extraNotChanged?.Count ?? 0) > 0;
            if (problems)
            {
                string message = "One or more places were left as they were (see outcomes): the others may have changed, and nothing is rolled back.";
                report["error"] = message;
                Debug.LogError(log_prefix + message);
                return ExitTargetsNotChanged;
            }

            return ExitDone;
        }

        #endregion

        #region published

        private static async Task<int> PublishedAsync(Options options)
        {
            int code = SelectFilesScenes(options, out List<SceneRef> scenes);
            if (code == ExitDone)
                code = await FilesPreludeAsync(options, write: false);
            if (code != ExitDone)
                return code;

            ReportSupport();
            (int Code, FilesListing Listing) read = await ListFilesAsync(scenes);
            if (read.Code != ExitDone)
                return read.Code;

            report["published"] = read.Listing.Scenes.Select(s => new Dictionary<string, object>
            {
                { "scene", s.Scene.Name },
                { "addressName", s.Scene.Key },
                { "includedInBuild", s.Scene.IncludedInBuild },
                { "places", s.Targets.OrderBy(t => t.IsTenant ? -1 : t.WorldId).Select(t =>
                    {
                        Dictionary<string, object> place = PlaceReport(t);
                        place["canWrite"] = t.CanWrite;
                        place["seenThroughWorld"] = t.SeenThroughWorld ? true : (bool?)null;
                        place["thumbnail"] = StatusReport(t.Thumbnail);
                        place["localization"] = StatusReport(t.Localization);
                        return place;
                    }).ToList() },
            }).ToList();

            return ExitDone;
        }

        #endregion

        #region save-files

        private static async Task<int> SaveFilesAsync(Options options)
        {
            int code = SelectFilesScenes(options, out List<SceneRef> scenes);
            if (code == ExitDone)
                code = await FilesPreludeAsync(options, write: false);
            if (code != ExitDone)
                return code;

            string destFolder = null;
            if (!string.IsNullOrEmpty(options.BackupFolder))
            {
                destFolder = Path.GetFullPath(options.BackupFolder);
                if (File.Exists(Path.Combine(destFolder, "manifest.json")))
                {
                    string message = $"'{options.BackupFolder}' already holds a save-files manifest: name another folder.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    return ExitBadArguments;
                }
            }

            ReportSupport();
            (int Code, FilesListing Listing) read = await ListFilesAsync(scenes);
            if (read.Code != ExitDone)
                return read.Code;

            code = CheckFilesTargets(options, read.Listing, write: false);
            if (code != ExitDone)
                return code;

            PlaceSelection selection = SelectionOf(options, scenes);
            List<FilesTarget> targets = read.Listing.AllTargets.Where(selection.Matches).ToList();

            SaveResult saved = await EnvironmentFilesService.SaveToProjectAsync(targetKey, targets, destFolder, options.DryRun, message => Debug.Log(log_prefix + message));

            if (options.DryRun)
                report["dryRun"] = true;

            report["saveFiles"] = new Dictionary<string, object>
            {
                { "manifest", saved.ManifestPath },
                { "written", saved.Written },
                { "alreadyInProject", saved.AlreadyInProject },
                { "absent", saved.Absent },
                { "failed", saved.Failed },
                { "folder", destFolder ?? EnvironmentFilesService.PublishFolder },
                { "files", saved.Files.Select(f =>
                    {
                        Dictionary<string, object> entry = PlaceReport(f.Target);
                        entry["file"] = f.Kind == FileKind.Thumbnail ? "thumbnail" : "localization";
                        entry["status"] = char.ToLowerInvariant(f.Status.ToString()[0]) + f.Status.ToString().Substring(1);
                        entry["asset"] = f.Asset;
                        entry["sha256"] = f.Sha256;
                        entry["error"] = f.Error;
                        return entry;
                    }).ToList() },
            };

            foreach (SavedFile failed in saved.Files.Where(f => f.Status == SaveStatus.Failed))
                Debug.LogError($"{log_prefix}{failed.Target.Scene.Name}, {failed.Target.Place}, {failed.Kind}: {failed.Error}");

            Debug.Log($"{log_prefix}save-files: {saved.Written} written{(options.DryRun ? " (dry run)" : string.Empty)}, {saved.AlreadyInProject} already in the project, {saved.Absent} absent, {saved.Failed} failed.");

            if (saved.Failed > 0)
            {
                report["error"] = $"{saved.Failed} file(s) could not be saved: see saveFiles.files.";
                return ExitPlatformUnreachable;
            }

            return ExitDone;
        }

        #endregion

        #region set-files

        private static async Task<int> SetFilesAsync(Options options)
        {
            int code = SelectFilesScenes(options, out List<SceneRef> scenes);
            if (code == ExitDone)
                code = await FilesPreludeAsync(options, write: true);
            if (code != ExitDone)
                return code;

            ReportSupport();
            SceneRef scene = scenes[0];

            // The files first: nothing is sent when one is not valid, and what a conversion is going to be is known before the network.
            List<string> languages = null;
            if (options.Localization != null)
            {
                (List<string> tenantLanguages, string languageError) = await EnvironmentFilesService.GetTenantLanguagesAsync(targetKey);
                languages = tenantLanguages;
                if (languageError != null)
                {
                    Debug.LogWarning(log_prefix + languageError + " The file is checked against the platform's rules only.");
                    report["tenantLanguagesError"] = languageError;
                }
                else
                {
                    report["tenantLanguages"] = languages;
                }
            }

            PreparedFile thumbnail = options.Thumbnail != null ? EnvironmentFilesService.PrepareThumbnail(options.Thumbnail, write: false) : null;
            PreparedFile localization = options.Localization != null ? EnvironmentFilesService.PrepareLocalization(options.Localization, languages) : null;

            (int Code, FilesListing Listing) read = await ListFilesAsync(scenes);
            if (read.Code != ExitDone)
                return read.Code;

            code = CheckFilesTargets(options, read.Listing, write: true);
            if (code != ExitDone)
                return code;

            PlaceSelection selection = SelectionOf(options, scenes);
            List<FilesTarget> targets = read.Listing.AllTargets.Where(selection.Matches).ToList();

            // A place that was asked for by name and has no such environment is a place that did not change.
            List<string> missing = new();
            if (options.Worlds != null)
                missing.AddRange(options.Worlds.Where(id => targets.All(t => t.WorldId != id)).Select(id => $"{scene.Name} is not published in world {id}."));
            if (options.Tenant && targets.All(t => !t.IsTenant))
                missing.Add($"{scene.Name} is not published at the tenant level.");
            if (options.AllWorlds && targets.Count == 0)
                missing.Add($"{scene.Name} is not published in any world.");

            ReplaceResult result = await EnvironmentFilesService.ReplaceAsync(targets, thumbnail, localization, options.DryRun, message => Debug.Log(log_prefix + message));
            return FinishReplace(result, options.DryRun, missing);
        }

        #endregion

        #region restore

        private static async Task<int> RestoreAsync(Options options)
        {
            SaveManifest manifest = EnvironmentFilesService.ReadManifest(options.Manifest, out string manifestError);
            if (manifest == null)
            {
                Debug.LogError(log_prefix + manifestError);
                report["error"] = manifestError;
                return ExitBadArguments;
            }

            int code = await FilesPreludeAsync(options, write: true);
            if (code != ExitDone)
                return code;

            if (!string.Equals(manifest.Connection, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                string message = $"The manifest belongs to the connection '{manifest.Connection}', and this command acts on '{targetKey}': environment ids mean something only within their connection. Nothing was sent.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitTargetMismatch;
            }

            ReportSupport();
            string manifestPath = options.Manifest.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) ? EnvironmentFilesService.FullPathOf(options.Manifest) : Path.GetFullPath(options.Manifest);
            string manifestFolder = Path.GetDirectoryName(manifestPath);

            List<SceneRef> scenes = EnvironmentFilesService.ScenesOf(manifest);
            if (options.Scenes != null)
            {
                List<string> wanted = options.Scenes.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                List<string> unknown = wanted.Where(w => scenes.All(s => !string.Equals(s.Name, w, StringComparison.OrdinalIgnoreCase) && !string.Equals(s.Key, w, StringComparison.OrdinalIgnoreCase))).ToList();
                if (unknown.Count > 0)
                {
                    string message = $"-vdScenes names scenes the manifest does not have: {string.Join(", ", unknown)}. It has: {string.Join(", ", scenes.Select(s => s.Name))}.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    return ExitNothingToBuild;
                }

                scenes = scenes.Where(s => wanted.Any(w => string.Equals(s.Name, w, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Key, w, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            (int Code, FilesListing Listing) read = await ListFilesAsync(scenes);
            if (read.Code != ExitDone)
                return read.Code;

            code = CheckFilesTargets(options, read.Listing, write: true);
            if (code != ExitDone)
                return code;

            List<string> languages = null;
            if (manifest.Entries.Any(e => e.Localization?.Status == "saved"))
            {
                (List<string> tenantLanguages, string languageError) = await EnvironmentFilesService.GetTenantLanguagesAsync(targetKey);
                languages = tenantLanguages;
                if (languageError != null)
                    Debug.LogWarning(log_prefix + languageError + " The files are checked against the platform's rules only.");
            }

            PlaceSelection selection = SelectionOf(options, scenes);
            ReplaceResult result = await EnvironmentFilesService.RestoreAsync(manifest, manifestFolder, read.Listing, selection, languages, options.DryRun, message => Debug.Log(log_prefix + message));
            return FinishReplace(result, options.DryRun);
        }

        #endregion

        #region scene-files

        private static int SceneFilesCommand(Options options)
        {
            int code = SelectFilesScenes(options, out List<SceneRef> scenes);
            if (code != ExitDone)
                return code;

            SceneListScriptableObject list = SceneListScriptableObject.Find();
            SceneListScriptableObject.SceneConfiguration config = list.SceneConfigurations.First(c => c.Scene != null && c.SceneNameFiltered == scenes[0].Key);

            Texture2D thumbnail = null;
            TextAsset localization = null;

            if (options.Thumbnail != null)
            {
                PreparedFile file = EnvironmentFilesService.PrepareThumbnail(options.Thumbnail, write: false);
                thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(options.Thumbnail);
                if (!file.Check.Valid || thumbnail == null)
                    return SceneFilesInvalid(file, thumbnail == null ? $"'{options.Thumbnail}' is not an image asset of the project." : null);

                foreach (FileIssue warning in file.Check.Warnings)
                    Debug.LogWarning($"{log_prefix}{options.Thumbnail}: {warning}");
                report["thumbnailCheck"] = PreparedReport(file);
            }

            if (options.Localization != null)
            {
                PreparedFile file = EnvironmentFilesService.PrepareLocalization(options.Localization, null);
                localization = AssetDatabase.LoadAssetAtPath<TextAsset>(options.Localization);
                if (!file.Check.Valid || localization == null)
                    return SceneFilesInvalid(file, localization == null ? $"'{options.Localization}' is not a text asset of the project." : null);

                foreach (FileIssue warning in file.Check.Warnings)
                    Debug.LogWarning($"{log_prefix}{options.Localization}: {warning}");
                report["localizationCheck"] = PreparedReport(file);
            }

            bool changed = false;
            if (options.Clear)
            {
                changed = config.DefaultThumbnail != null || config.DefaultLocalization != null;
                config.DefaultThumbnail = null;
                config.DefaultLocalization = null;
            }

            if (thumbnail != null)
            {
                changed |= config.DefaultThumbnail != thumbnail;
                config.DefaultThumbnail = thumbnail;
            }

            if (localization != null)
            {
                changed |= config.DefaultLocalization != localization;
                config.DefaultLocalization = localization;
            }

            if (changed)
            {
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssets();
            }

            report["sceneFiles"] = new Dictionary<string, object>
            {
                { "scene", scenes[0].Name },
                { "defaultThumbnail", config.DefaultThumbnail != null ? AssetDatabase.GetAssetPath(config.DefaultThumbnail) : null },
                { "defaultLocalization", config.DefaultLocalization != null ? AssetDatabase.GetAssetPath(config.DefaultLocalization) : null },
                { "changed", changed },
                { "sceneList", AssetDatabase.GetAssetPath(list) },
            };

            Debug.Log($"{log_prefix}scene-files: {scenes[0].Name}: thumbnail {(config.DefaultThumbnail != null ? AssetDatabase.GetAssetPath(config.DefaultThumbnail) : "none")}, " +
                      $"localization file {(config.DefaultLocalization != null ? AssetDatabase.GetAssetPath(config.DefaultLocalization) : "none")}{(changed ? " (saved)" : string.Empty)}.");
            return ExitDone;
        }

        private static int SceneFilesInvalid(PreparedFile file, string extra)
        {
            report["invalidFiles"] = new List<Dictionary<string, object>> { PreparedReport(file) };
            string message = $"{file.SourceAsset} cannot be a default: {string.Join(" ", new[] { extra }.Concat(file.Check.Errors.Take(5).Select(e => e.ToString())).Where(x => x != null))}";
            Debug.LogError(log_prefix + message);
            report["error"] = message;
            return ExitFileNotValid;
        }

        #endregion

        #region deploy

        /// <summary>
        /// What <c>deploy -vdDryRun</c> says about the default files: for each place and each scene that is new there, the files it would
        /// send and what their checks say; a scene already published there is not touched. Reads each place's list; sends and writes nothing.
        /// </summary>
        private static async Task<List<Dictionary<string, object>>> DefaultFilesDryRunAsync(Options options, IReadOnlyList<string> sceneKeys, WorldListResult listing)
        {
            List<Dictionary<string, object>> places = new();
            List<(int? WorldId, string Label)> targets = options.Tenant
                ? new List<(int?, string)> { (null, "the tenant level") }
                : options.Worlds.Select(id => ((int?)id, $"world \"{listing.Deployable.First(w => w.Id == id).Label}\"")).ToList();

            foreach ((int? worldId, string label) in targets)
            {
                List<DefaultFilesPlan> plans = await EnvironmentFilesService.PlanDefaultsAsync(targetKey, worldId, label, sceneKeys);
                foreach (DefaultFilesPlan plan in plans.Where(p => p.IsNew || p.Note != null))
                {
                    foreach (string line in plan.Errors.Concat(plan.Warnings).Concat(plan.Note != null ? new[] { plan.Note } : Array.Empty<string>()))
                        Debug.LogWarning(log_prefix + line);

                    places.Add(new Dictionary<string, object>
                    {
                        { "place", label },
                        { "worldId", worldId },
                        { "scene", plan.Scene },
                        { "new", plan.IsNew },
                        { "thumbnail", plan.Thumbnail },
                        { "thumbnailUpload", plan.ThumbnailUpload },
                        { "localization", plan.Localization },
                        { "conversion", plan.ConversionNote },
                        { "errors", plan.Errors },
                        { "warnings", plan.Warnings },
                        { "note", plan.Note },
                    });
                }
            }

            return places;
        }

        #endregion
    }
}
