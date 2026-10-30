using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Virtuademy.SDK.Environments.Editor
{
    internal static partial class EnvironmentFilesService
    {
        #region Replace

        /// <summary>
        /// Replaces the thumbnail and/or the localization file at every target. Each file is prepared and checked once
        /// (<see cref="PrepareThumbnail"/>, <see cref="PrepareLocalization"/>); one that the platform would refuse stops everything
        /// before any request (<see cref="ReplaceResult.Invalid"/>). Then target by target, a thumbnail first: see
        /// <see cref="ReplaceOneAsync"/>. A dry run sends nothing and saves nothing, and says what each target would do.
        /// </summary>
        public static async Task<ReplaceResult> ReplaceAsync(IReadOnlyList<FilesTarget> targets, PreparedFile thumbnail, PreparedFile localization,
                                                             bool dryRun, Action<string> progress = null, CancellationToken ct = default)
        {
            ReplaceResult result = new();
            result.Files.AddRange(new[] { thumbnail, localization }.Where(f => f != null));
            if (result.Files.Any(f => f.Check != null && !f.Check.Valid))
            {
                result.Invalid = true;
                return result;
            }

            foreach (FilesTarget target in targets)
            {
                foreach (PreparedFile file in result.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Invoke($"{target.Scene.Name}, {target.Place}: {(file.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file")}...");
                    result.Outcomes.Add(await ReplaceOneAsync(target, file, dryRun, ct));
                }
            }

            return result;
        }

        /// <summary>
        /// One file at one target. In this order, and nothing is sent in any of the first four: the tenant's platform cannot receive files
        /// from the SDK (<see cref="OutcomeKind.Unsupported"/>); the login may not change the environment (<see cref="OutcomeKind.Refused"/>,
        /// as the platform's <c>canWrite</c> says, before any request); the place already holds this file by hash
        /// (<see cref="OutcomeKind.Unchanged"/>); a dry run. Otherwise the file the place holds now is saved into the project (unless it
        /// already is there; if that fails, nothing is replaced) and the new one is sent. The target's status is updated from the platform's answer.
        /// </summary>
        public static async Task<FileOutcome> ReplaceOneAsync(FilesTarget target, PreparedFile file, bool dryRun, CancellationToken ct = default)
        {
            FileOutcome outcome = new() { Target = target, Kind = file.Kind };
            FileStatus status = target.StatusOf(file.Kind);

            WriteSupport support = SupportOf(target.ConnectionKey);
            if (support == WriteSupport.NotSupported)
                return Finish(outcome, OutcomeKind.Unsupported, UnsupportedMessage);

            string refusal = RefusalFor(target);
            if (refusal != null)
                return Finish(outcome, OutcomeKind.Refused, refusal);

            if (status.Present && status.Sha256 != null &&
                (string.Equals(status.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase) ||
                 (file.StoredSha256 != null && string.Equals(status.Sha256, file.StoredSha256, StringComparison.OrdinalIgnoreCase))))
                return Finish(outcome, OutcomeKind.Unchanged, "Unchanged: same file.");

            if (dryRun)
            {
                outcome.WouldSavePrevious = status.Present && status.ProjectAsset == null;
                return Finish(outcome, OutcomeKind.WouldUpdate, "Will change.");
            }

            if (status.Present)
            {
                SavedFile saved = await SaveOnlineFileAsync(target, file.Kind, null, false, ct);
                if (saved.Status == SaveStatus.Failed)
                    return Finish(outcome, OutcomeKind.Failed, $"The file this place holds now could not be saved to the project first, so nothing was replaced: {saved.Error}");

                outcome.SavedPrevious = saved.Asset;
            }

            ct.ThrowIfCancellationRequested();
            string commitError = CommitConversion(file);
            if (commitError != null)
                return Finish(outcome, OutcomeKind.Failed, commitError);

            (OutcomeKind result, string message, EnvironmentDto env) = await PutAsync(target, file, support);
            if (result != OutcomeKind.Updated)
                return Finish(outcome, result, message);

            await ReadBackAsync(target, file, env, ct);
            if (status.Error != null)
                outcome.Warning = $"The file was sent, but the stored file could not be read back: {status.Error}";

            return Finish(outcome, OutcomeKind.Updated, "Updated.");
        }

        private static FileOutcome Finish(FileOutcome outcome, OutcomeKind result, string message)
        {
            outcome.Result = result;
            outcome.Message = message;
            return outcome;
        }

        private static async Task<(OutcomeKind Result, string Message, EnvironmentDto Env)> PutAsync(FilesTarget target, PreparedFile file, WriteSupport support)
        {
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(target.ConnectionKey);
            if (string.IsNullOrEmpty(apiUrl))
                return (OutcomeKind.Failed, "No application API URL in the tenant's configuration.", null);

            string route = file.Kind == FileKind.Thumbnail ? "thumbnail" : "localization";
            string url = target.IsTenant
                ? $"{apiUrl}/tenants/environments/{target.EnvId}/{route}?api-version={ApiVersion}"
                : $"{apiUrl}/worlds/{target.WorldId}/environments/{target.EnvId}/{route}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await EditorSessionManager.SendAuthorizedAsync(target.ConnectionKey, () =>
                {
                    HttpRequestMessage request = new(HttpMethod.Put, url) { Content = new ByteArrayContent(file.Bytes) };
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    return request;
                }, uploadClient, allowInteractive: false);

                if (response == null)
                    return (OutcomeKind.Failed, "The session is gone and could not be renewed silently: log in again.", null);

                using (response)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    int status = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        EnvironmentDto env = null;
                        try { env = JsonConvert.DeserializeObject<EnvironmentDto>(body); }
                        catch (JsonException) { /* The answer is read back from the list instead. */ }

                        return (OutcomeKind.Updated, null, env);
                    }

                    // A platform that has no such route answers 405 (or 404, which on a supported one means the environment is not there).
                    if (status == (int)HttpStatusCode.MethodNotAllowed || (status == (int)HttpStatusCode.NotFound && support == WriteSupport.Unknown))
                        return (OutcomeKind.Unsupported, UnsupportedMessage, null);

                    if (status == (int)HttpStatusCode.Forbidden)
                        return (OutcomeKind.Refused, DescribeProblem(body, status), null);

                    return (OutcomeKind.Failed, DescribeProblem(body, status), null);
                }
            }
            catch (Exception ex)
            {
                return (OutcomeKind.Failed, ex.Message, null);
            }
        }

        /// <summary>
        /// After a write: what the platform stores now. From the answer's environment when it carries the file's address and hash, else
        /// from the place's list; a file whose hash it does not say is downloaded and hashed. Sets <see cref="FileStatus.Error"/> when it cannot.
        /// </summary>
        private static async Task ReadBackAsync(FilesTarget target, PreparedFile file, EnvironmentDto env, CancellationToken ct)
        {
            FileStatus status = target.StatusOf(file.Kind);
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(target.ConnectionKey);

            bool Complete(EnvironmentDto e) => e != null &&
                (file.Kind == FileKind.Thumbnail ? !string.IsNullOrEmpty(e.HeroImageUri) || !string.IsNullOrEmpty(e.ThumbnailUri) : !string.IsNullOrEmpty(e.LocalizationUri));

            if (!Complete(env))
                env = (await ReadPlaceAsync(target.ConnectionKey, apiUrl, target.WorldId)).Value?.FirstOrDefault(e => e.Id == target.EnvId);

            status.Error = null;
            if (Complete(env))
            {
                FilesTarget fresh = BuildTarget(target.ConnectionKey, apiUrl, target.Scene, target.WorldId, target.WorldLabel, env, target.SeenThroughWorld);
                FileStatus now = fresh.StatusOf(file.Kind);
                status.Uri = now.Uri;
                status.Present = now.Present;
                status.Sha256 = now.Sha256;
                status.HashFromPlatform = now.HashFromPlatform;
                status.Width = 0;
                status.Height = 0;
                status.Format = null;
                status.CachePath = null;
                if (file.Kind == FileKind.Thumbnail)
                    target.CardUri = fresh.CardUri;
            }

            Dictionary<string, string> index = PublishedFiles.Index();
            if (status.Present && status.Sha256 == null)
            {
                await HashByDownloadAsync(status, index, ct);
            }
            else if (status.Sha256 != null)
            {
                status.ProjectAsset = index.TryGetValue(status.Sha256, out string asset) ? asset : null;
            }

            // The file that was sent is a project asset, wherever it lives: when the platform holds exactly it, that is "the same file in the project".
            if (status.Present && status.Sha256 != null && status.ProjectAsset == null &&
                (string.Equals(status.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase) || string.Equals(status.Sha256, file.StoredSha256, StringComparison.OrdinalIgnoreCase)))
                status.ProjectAsset = file.UploadAsset;
        }

        #endregion

        #region Save to project

        /// <summary>
        /// Saves the file a place holds now into the project: a new file in <see cref="PublishFolder"/> named
        /// <c>&lt;scene&gt;-world-&lt;id&gt;-thumbnail.jpg</c> (<c>-tenant-</c> at tenant level; <c>-localization.csv</c>), unless the project already holds
        /// the same file (same SHA-256), which is reported and not written again. With <paramref name="destFolder"/>, a plain copy goes there
        /// instead, outside the project's dedupe. A dry run says what would be written.
        /// </summary>
        public static async Task<SavedFile> SaveOnlineFileAsync(FilesTarget target, FileKind kind, string destFolder, bool dryRun, CancellationToken ct = default)
        {
            FileStatus status = target.StatusOf(kind);
            SavedFile saved = new() { Target = target, Kind = kind, Sha256 = status.Sha256 };

            if (!status.Present)
            {
                saved.Status = SaveStatus.Absent;
                return saved;
            }

            Dictionary<string, string> index = PublishedFiles.Index();
            if (destFolder == null && status.Sha256 != null && index.TryGetValue(status.Sha256, out string known))
            {
                status.ProjectAsset = known;
                saved.Status = SaveStatus.AlreadyInProject;
                saved.Asset = known;
                return saved;
            }

            byte[] bytes = null;
            if (status.CachePath != null && File.Exists(status.CachePath))
                bytes = File.ReadAllBytes(status.CachePath);

            if (bytes == null || (status.Sha256 != null && !string.Equals(Sha256(bytes), status.Sha256, StringComparison.OrdinalIgnoreCase)))
            {
                Download download = await DownloadAsync(status.Uri, ct);
                if (!download.Found)
                {
                    // When the platform states a hash, the file is there: not finding it is a failure, and nothing may be replaced on the strength of it.
                    // Without a hash (an old row, or an environment the import just made, whose thumbnail path points nowhere) it is simply absent.
                    if (status.HashFromPlatform)
                    {
                        saved.Status = SaveStatus.Failed;
                        saved.Error = "The platform says this place holds a file, but its address does not answer (404).";
                        return saved;
                    }

                    status.Present = false;
                    saved.Status = SaveStatus.Absent;
                    return saved;
                }

                if (download.Error != null)
                {
                    saved.Status = SaveStatus.Failed;
                    saved.Error = $"The file could not be downloaded: {download.Error}";
                    return saved;
                }

                bytes = download.Bytes;
            }

            saved.Sha256 = Sha256(bytes);

            // What is saved must be what the platform says is there, and for a thumbnail an image: a mismatch means the file changed under us or the
            // address answers something else, and the file about to be replaced would be lost.
            if (status.HashFromPlatform && !string.Equals(saved.Sha256, status.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                saved.Status = SaveStatus.Failed;
                saved.Error = "The file downloaded is not the one the platform says this place holds (its hash differs): try again.";
                return saved;
            }

            if (kind == FileKind.Thumbnail && ThumbnailCheck.Check(bytes).Format == null)
            {
                saved.Status = SaveStatus.Failed;
                saved.Error = "The file downloaded is not a PNG or JPEG image.";
                return saved;
            }

            status.Sha256 = saved.Sha256;

            if (destFolder == null && index.TryGetValue(saved.Sha256, out string existing))
            {
                status.ProjectAsset = existing;
                saved.Status = SaveStatus.AlreadyInProject;
                saved.Asset = existing;
                return saved;
            }

            string extension = kind == FileKind.Thumbnail ? ImageExtension(bytes) : ".csv";
            string place = target.IsTenant ? "tenant" : "world-" + target.WorldId.Value.ToString(CultureInfo.InvariantCulture);
            string name = $"{target.Scene.Name}-{place}-{(kind == FileKind.Thumbnail ? "thumbnail" : "localization")}";

            try
            {
                if (destFolder != null)
                {
                    string folder = Path.Combine(destFolder, target.Scene.Key, place);
                    saved.Asset = Path.Combine(folder, name + extension);
                    if (!dryRun)
                    {
                        Directory.CreateDirectory(folder);
                        File.WriteAllBytes(saved.Asset, bytes);
                    }

                    saved.Status = dryRun ? SaveStatus.WouldWrite : SaveStatus.Written;
                    return saved;
                }

                saved.Asset = FreeName(PublishFolder, name, extension);
                if (dryRun)
                {
                    saved.Status = SaveStatus.WouldWrite;
                    return saved;
                }

                WriteAsset(saved.Asset, bytes);
                status.ProjectAsset = saved.Asset;
                saved.Status = SaveStatus.Written;
            }
            catch (Exception ex)
            {
                saved.Status = SaveStatus.Failed;
                saved.Error = $"The file could not be written: {ex.Message}";
            }

            return saved;
        }

        /// <summary>
        /// Saves every selected place's thumbnail and localization file into the project (each one as <see cref="SaveOnlineFileAsync"/> says),
        /// and writes a manifest that says which asset holds which place's file: <c>Backups/&lt;connection&gt;-&lt;yyyyMMdd-HHmmss&gt;.json</c> in
        /// <see cref="PublishFolder"/>, or <c>manifest.json</c> in <paramref name="destFolder"/> when a folder outside the project is named
        /// (then the files are copied there too). <see cref="RestoreAsync"/> reads it.
        /// </summary>
        public static async Task<SaveResult> SaveToProjectAsync(string connectionKey, IReadOnlyList<FilesTarget> targets, string destFolder, bool dryRun,
                                                                Action<string> progress = null, CancellationToken ct = default)
        {
            ConnectionRecord record = ConnectionStore.Get(connectionKey);
            SaveResult result = new()
            {
                Manifest = new SaveManifest { CreatedUtc = DateTime.UtcNow, Connection = connectionKey, TenantLabel = record?.TenantLabel },
            };

            foreach (FilesTarget target in targets)
            {
                ManifestEntry entry = new()
                {
                    Scene = target.Scene.Name, SceneKey = target.Scene.Key, WorldId = target.WorldId, WorldLabel = target.WorldLabel,
                    Tenant = target.IsTenant, EnvId = target.EnvId, EnvLabel = target.EnvLabel,
                };

                foreach (FileKind kind in new[] { FileKind.Thumbnail, FileKind.Localization })
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Invoke($"{target.Scene.Name}, {target.Place}: {(kind == FileKind.Thumbnail ? "thumbnail" : "localization file")}...");

                    SavedFile saved = await SaveOnlineFileAsync(target, kind, destFolder, dryRun, ct);
                    result.Files.Add(saved);

                    ManifestFile item = new()
                    {
                        Status = saved.Status == SaveStatus.Absent ? "absent" : saved.Status == SaveStatus.Failed ? "failed" : "saved",
                        Sha256 = saved.Sha256,
                        Error = saved.Error,
                    };

                    if (saved.Asset != null)
                    {
                        if (destFolder == null)
                            item.Asset = saved.Asset;
                        else
                            item.File = Path.GetRelativePath(destFolder, saved.Asset).Replace('\\', '/');
                    }

                    if (kind == FileKind.Thumbnail) entry.Thumbnail = item; else entry.Localization = item;
                }

                result.Manifest.Entries.Add(entry);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            result.ManifestPath = destFolder == null ? FreeName(ManifestFolder, $"{SlugOf(connectionKey)}-{stamp}", ".json") : Path.Combine(destFolder, "manifest.json");

            if (!dryRun)
            {
                byte[] json = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(result.Manifest, ManifestSettings));
                if (destFolder == null)
                {
                    WriteAsset(result.ManifestPath, json);
                }
                else
                {
                    Directory.CreateDirectory(destFolder);
                    File.WriteAllBytes(result.ManifestPath, json);
                }
            }

            return result;
        }

        private static readonly JsonSerializerSettings ManifestSettings = new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        /// <summary>Reads a manifest <see cref="SaveToProjectAsync"/> wrote; null with the reason when it cannot be read.</summary>
        public static SaveManifest ReadManifest(string path, out string error)
        {
            error = null;
            try
            {
                string full = path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) ? FullPathOf(path) : Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    error = $"The manifest '{path}' does not exist.";
                    return null;
                }

                SaveManifest manifest = JsonConvert.DeserializeObject<SaveManifest>(File.ReadAllText(full), ManifestSettings);
                if (manifest?.Entries == null || string.IsNullOrEmpty(manifest.Connection))
                {
                    error = $"'{path}' is not a manifest written by save-files.";
                    return null;
                }

                return manifest;
            }
            catch (Exception ex)
            {
                error = $"The manifest '{path}' could not be read: {ex.Message}";
                return null;
            }
        }

        /// <summary>The scenes a manifest names, so a restore can list their places without the project having the scenes.</summary>
        public static List<SceneRef> ScenesOf(SaveManifest manifest) =>
            manifest.Entries.GroupBy(e => e.SceneKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => new SceneRef { Name = g.First().Scene, Key = g.Key }).ToList();

        /// <summary>
        /// Puts back what a save manifest recorded: for every selected entry that is still published (same environment id, same place),
        /// its saved thumbnail and localization file replace what the place holds now, as <see cref="ReplaceOneAsync"/> says. A file the
        /// manifest has as absent is left alone (there is no way to remove a file). <paramref name="listing"/> is the connection's
        /// <see cref="ListAsync"/> for <see cref="ScenesOf(SaveManifest)"/>; <paramref name="manifestFolder"/> is where the manifest is, for the
        /// files it names relative to it.
        /// </summary>
        public static async Task<ReplaceResult> RestoreAsync(SaveManifest manifest, string manifestFolder, FilesListing listing, PlaceSelection selection,
                                                             IReadOnlyList<string> tenantLanguages, bool dryRun, Action<string> progress = null, CancellationToken ct = default)
        {
            ReplaceResult result = new();
            List<(FilesTarget Target, PreparedFile Thumbnail, PreparedFile Localization)> work = new();

            foreach (ManifestEntry entry in manifest.Entries.Where(e => selection.Matches(e.SceneKey, e.WorldId)))
            {
                FilesTarget target = listing.AllTargets.FirstOrDefault(t => t.EnvId == entry.EnvId && t.WorldId == entry.WorldId && string.Equals(t.Scene.Key, entry.SceneKey, StringComparison.OrdinalIgnoreCase));
                string place = entry.Tenant ? "the tenant level" : $"world {entry.WorldId}";
                if (target == null)
                {
                    result.Notes.Add($"{entry.Scene} is not published at {place} any more (environment {entry.EnvId}): left alone.");
                    continue;
                }

                PreparedFile thumbnail = PrepareFromManifest(entry.Thumbnail, FileKind.Thumbnail, manifestFolder, tenantLanguages, dryRun, result, $"{entry.Scene} at {place}");
                PreparedFile localization = PrepareFromManifest(entry.Localization, FileKind.Localization, manifestFolder, tenantLanguages, dryRun, result, $"{entry.Scene} at {place}");
                work.Add((target, thumbnail, localization));
                result.Files.AddRange(new[] { thumbnail, localization }.Where(f => f != null));
            }

            if (result.Files.Any(f => f.Check != null && !f.Check.Valid))
            {
                result.Invalid = true;
                return result;
            }

            foreach ((FilesTarget target, PreparedFile thumbnail, PreparedFile localization) in work)
            {
                foreach (PreparedFile file in new[] { thumbnail, localization }.Where(f => f != null))
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Invoke($"{target.Scene.Name}, {target.Place}: {(file.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file")}...");
                    result.Outcomes.Add(await ReplaceOneAsync(target, file, dryRun, ct));
                }
            }

            return result;
        }

        private static PreparedFile PrepareFromManifest(ManifestFile item, FileKind kind, string manifestFolder, IReadOnlyList<string> tenantLanguages,
                                                        bool dryRun, ReplaceResult result, string label)
        {
            if (item == null || item.Status != "saved")
            {
                result.Notes.Add($"{label}: no {(kind == FileKind.Thumbnail ? "thumbnail" : "localization file")} in the manifest ({item?.Status ?? "none"}): left alone.");
                return null;
            }

            bool inProject = !string.IsNullOrEmpty(item.Asset);
            string path = inProject ? item.Asset : Path.GetFullPath(Path.Combine(manifestFolder, item.File ?? string.Empty));
            return kind == FileKind.Thumbnail
                ? PrepareThumbnail(path, write: false, requireAsset: inProject)
                : PrepareLocalization(path, tenantLanguages, requireAsset: inProject);
        }

        #endregion
    }
}
