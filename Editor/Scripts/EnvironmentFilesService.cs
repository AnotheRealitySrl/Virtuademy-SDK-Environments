using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Virtuademy.SDK.Http;
using Virtuademy.SDK.TenantConfiguration;
using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The thumbnail and the localization file of an environment, per place (a world, or a tenant's tenant level): read where a
    /// scene is published and what it holds, say which of those files the project holds too, save them into the project, and replace
    /// them. What the Environment Details window and the <c>EnvironmentsCli</c> commands <c>published</c>, <c>save-files</c>,
    /// <c>set-files</c> and <c>restore</c> do, and what a deploy does with a scene's default files when it makes the scene's
    /// environment in a place (<see cref="EnvironmentFilesService.ApplyDefaultsAfterImportAsync"/>). No UI and no static state beyond
    /// HTTP clients: every method takes the connection it acts on, is asynchronous, and throws <see cref="OperationCanceledException"/>
    /// when its token is cancelled between two steps.
    /// </summary>
    /// <remarks>
    /// <b>The platform is the source; the project holds copies.</b> A file at a place is "the same" as a project asset when their
    /// SHA-256 are equal. The platform says the hash of what it stores (<c>heroImageHash</c>, <c>localizationHash</c> in the environment
    /// answers, parsed from the stored file's name); a platform that does not say (null) is asked for the file instead, which the SDK
    /// downloads, hashes and keeps under <c>Library/Virtuademy/EnvironmentDetails/</c>. Downloads are anonymous: a plain
    /// <see cref="HttpClient"/> that never carries the session's token. Writes go through
    /// <see cref="EditorSessionManager.SendAuthorizedAsync(string, Func{HttpRequestMessage}, HttpClient, bool)"/> to the Application API
    /// of the connection (<see cref="EditorApiEndpoint.ApplicationApiUrlOf"/>). Before a place's file is replaced, the file it holds is
    /// saved into <see cref="PublishFolder"/> unless the project already holds it; nothing is rolled back.
    /// <para>
    /// <b>Writes need a tenant on 2026.7.0 or later</b> (<see cref="SupportOf(string)"/>), decided before any request; a tenant that
    /// does not report its version is tried, and a 404 or 405 answer is the same refusal. Reads work on every tenant. The messages never
    /// name a version.
    /// </para>
    /// </remarks>
    internal static partial class EnvironmentFilesService
    {
        public const string ApiVersion = "2";

        /// <summary>The folder the SDK writes files into: saved online files, conversions, save manifests. Committed; an <c>Editor</c> folder never reaches a build.</summary>
        public const string PublishFolder = "Assets/Virtuademy/Editor/PublishFiles";

        /// <summary>Where <c>save-files</c> writes its manifests.</summary>
        public const string ManifestFolder = PublishFolder + "/Backups";

        public const string UnsupportedMessage = "This tenant cannot receive thumbnails and localization files from the SDK yet: set them in the Back Office.";

        public const string NotAllowedMessage = "You cannot change this environment in this world: only the world's Owner, or the person who published it there, can.";

        public const string NotAllowedTenantMessage = "Only a TenantManager can change tenant-level environments.";

        /// <summary>The release that added the write routes.</summary>
        private const int support_year = 2026;

        private const int support_release = 7;

        private const int max_parallel_requests = 4;

        private static readonly HttpClient apiClient = new();
        private static readonly HttpClient uploadClient = new() { Timeout = TimeSpan.FromMinutes(5) };

        // Anonymous on purpose: the files are public, and this client is never given a token.
        private static readonly HttpClient downloadClient = new() { Timeout = TimeSpan.FromSeconds(60) };

        private static string projectRoot;

        /// <summary>The project's folder (the one with <c>Assets/</c>). Read on the main thread the first time.</summary>
        internal static string ProjectRoot => projectRoot ??= Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Where the downloaded copies the SDK hashed are kept, outside git.</summary>
        internal static string CacheFolder => Path.Combine(ProjectRoot, "Library", "Virtuademy", "EnvironmentDetails");

        internal static string FullPathOf(string assetPath) => Path.GetFullPath(Path.Combine(ProjectRoot, assetPath));

        #region Support

        /// <summary>Whether the connection's tenant can receive files, from the platform version its record holds.</summary>
        public static WriteSupport SupportOf(string connectionKey) => SupportOfVersion(ConnectionStore.Get(connectionKey)?.PlatformVersion);

        public static WriteSupport SupportOfVersion(string platformVersion)
        {
            Match match = Regex.Match(platformVersion ?? string.Empty, @"^\s*(\d{4})\.(\d+)", RegexOptions.CultureInvariant);
            if (!match.Success)
                return WriteSupport.Unknown;

            int year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int release = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            return year > support_year || (year == support_year && release >= support_release) ? WriteSupport.Supported : WriteSupport.NotSupported;
        }

        /// <summary>Why the login may not change this environment, or null when it may (or the platform did not say).</summary>
        public static string RefusalFor(FilesTarget target)
        {
            if (target.IsTenant && ConnectionStore.Get(target.ConnectionKey)?.IsTenantManager != true)
                return NotAllowedTenantMessage;

            return target.CanWrite == false ? (target.IsTenant ? NotAllowedTenantMessage : NotAllowedMessage) : null;
        }

        /// <summary>
        /// The languages the connection's tenant offers (<c>supportedLanguages</c> of its public configuration), read over the app's
        /// credential; an empty list stands for English alone, as the app reads it. Null with the reason when they could not be read.
        /// </summary>
        public static async Task<(List<string> Languages, string Error)> GetTenantLanguagesAsync(string connectionKey, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ConnectionRecord record = ConnectionStore.Get(connectionKey);
            if (record == null)
                return (null, $"There is no connection '{connectionKey}'.");

            try
            {
                ApiResponse<TenantPublicConfig> response = await TenantConfigurationClient.GetTenantPublicConfig(record.ToAppIdentification());
                if (!response.IsSuccess || response.Content == null)
                    return (null, $"The tenant's languages could not be read: {response.ReasonPhrase}");

                return (response.Content.SupportedLanguages?.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? new List<string>(), null);
            }
            catch (Exception ex)
            {
                return (null, $"The tenant's languages could not be read: {ex.Message}");
            }
        }

        #endregion

        #region Scenes

        /// <summary>Every scene of the scene list, included in the build or not.</summary>
        public static List<SceneRef> ScenesOf(SceneListScriptableObject list) =>
            (list?.SceneConfigurations ?? new List<SceneListScriptableObject.SceneConfiguration>())
            .Where(c => c.Scene != null)
            .Select(c => new SceneRef
            {
                Name = c.Scene.name,
                Key = c.SceneNameFiltered,
                Path = UnityEditor.AssetDatabase.GetAssetPath(c.Scene),
                IncludedInBuild = c.IncludeInBuild,
            }).ToList();

        #endregion

        #region Listing

        private sealed class EnvironmentDto
        {
            public int Id;
            public string Label;
            public string FileName;
            public string Catalog;
            public bool? IsTenant;
            public string ThumbnailUri;
            public string HeroImageUri;
            public string LocalizationUri;
            public string HeroImageHash;
            public string LocalizationHash;
            public bool? CanWrite;
        }

        private sealed class WorldDto
        {
            public int Id;
            public string Label;
        }

        private sealed class Read<T> where T : class
        {
            public bool NoSession;
            public int Status;
            public T Value;
            public string Error;
            public bool Ok => Value != null;
        }

        /// <summary>
        /// Where the scenes are published on a connection, with what each place holds. Reads every world the account can see and the
        /// tenant level, a few requests at a time. With <paramref name="readFiles"/> (the default), a file the platform gives no hash
        /// for is downloaded and hashed; without it such a file has no hash and reads as online only.
        /// </summary>
        public static async Task<FilesListing> ListAsync(string connectionKey, IReadOnlyList<SceneRef> scenes, bool readFiles = true,
                                                         Action<string> progress = null, CancellationToken ct = default)
        {
            ConnectionRecord record = ConnectionStore.Get(connectionKey);
            FilesListing listing = new()
            {
                ConnectionKey = connectionKey,
                PlatformVersion = record?.PlatformVersion,
                Support = SupportOfVersion(record?.PlatformVersion),
            };

            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl))
            {
                listing.Status = ListingStatus.NoApiUrl;
                listing.Message = "Error: no application API URL in tenant config";
                return listing;
            }

            try
            {
                progress?.Invoke("Reading the worlds...");
                Read<List<WorldDto>> worlds = await GetAsync<List<WorldDto>>(connectionKey, $"{apiUrl}/worlds?api-version={ApiVersion}");
                if (worlds.NoSession)
                    return Fail(listing, ListingStatus.SessionRequired, "Session expired. Log in again to read the places.");
                if (!worlds.Ok)
                    return Fail(listing, ListingStatus.Failed, $"Error loading worlds: {worlds.Error}");

                listing.Worlds = worlds.Value.Select(w => new WorldRef(w.Id, w.Label)).ToList();

                progress?.Invoke("Reading the environments...");
                using SemaphoreSlim slots = new(max_parallel_requests);
                Task<Read<List<EnvironmentDto>>>[] worldReads = listing.Worlds.Select(async world =>
                {
                    await slots.WaitAsync(ct);
                    try
                    {
                        return await ReadPlaceAsync(connectionKey, apiUrl, world.Id);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }).ToArray();
                Task<Read<List<EnvironmentDto>>> tenantRead = ReadPlaceAsync(connectionKey, apiUrl, null);
                await Task.WhenAll(worldReads.Cast<Task>().Append(tenantRead));
                ct.ThrowIfCancellationRequested();

                if (worldReads.Any(t => t.Result.NoSession) || tenantRead.Result.NoSession)
                    return Fail(listing, ListingStatus.SessionRequired, "Session expired. Log in again to read the places.");

                Dictionary<string, SceneFiles> byKey = new(StringComparer.OrdinalIgnoreCase);
                foreach (SceneRef scene in scenes)
                    if (!string.IsNullOrEmpty(scene.Key) && !byKey.ContainsKey(scene.Key))
                        byKey[scene.Key] = new SceneFiles { Scene = scene };

                HashSet<int> tenantSeen = new();
                List<(EnvironmentDto Env, SceneFiles Scene)> viaWorld = new();
                for (int i = 0; i < listing.Worlds.Count; i++)
                {
                    List<EnvironmentDto> envs = worldReads[i].Result.Value;
                    if (envs == null)
                        continue;

                    foreach (EnvironmentDto env in envs)
                    {
                        if (!byKey.TryGetValue(KeyOf(env), out SceneFiles scene))
                            continue;

                        if (env.IsTenant == true)
                            viaWorld.Add((env, scene));
                        else
                            scene.Targets.Add(BuildTarget(connectionKey, apiUrl, scene.Scene, listing.Worlds[i].Id, listing.Worlds[i].Label, env, false));
                    }
                }

                // The tenant level. An account that cannot read the list still sees the tenant environments its worlds share.
                listing.TenantListUnreadable = !tenantRead.Result.Ok;
                if (tenantRead.Result.Ok)
                {
                    foreach (EnvironmentDto env in tenantRead.Result.Value)
                        if (byKey.TryGetValue(KeyOf(env), out SceneFiles scene) && tenantSeen.Add(env.Id))
                            scene.Targets.Add(BuildTarget(connectionKey, apiUrl, scene.Scene, null, null, env, false));
                }
                else
                {
                    foreach ((EnvironmentDto env, SceneFiles scene) in viaWorld)
                        if (tenantSeen.Add(env.Id))
                            scene.Targets.Add(BuildTarget(connectionKey, apiUrl, scene.Scene, null, null, env, true));
                }

                listing.Scenes = scenes.Where(s => !string.IsNullOrEmpty(s.Key)).Select(s => byKey[s.Key]).Distinct().ToList();

                progress?.Invoke("Comparing the files with the project...");
                await FillStatusesAsync(listing, readFiles, ct);
                listing.Status = ListingStatus.Ok;
                return listing;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail(listing, ListingStatus.Failed, $"Error: {ex.Message}");
            }
        }

        private static FilesListing Fail(FilesListing listing, ListingStatus status, string message)
        {
            listing.Status = status;
            listing.Message = message;
            return listing;
        }

        private static string KeyOf(EnvironmentDto env) => PublishedEnvironmentsIndex.Key(env.FileName ?? env.Label) ?? string.Empty;

        private static Task<Read<List<EnvironmentDto>>> ReadPlaceAsync(string connectionKey, string apiUrl, int? worldId) =>
            GetAsync<List<EnvironmentDto>>(connectionKey, worldId == null
                ? $"{apiUrl}/tenants/environments?api-version={ApiVersion}"
                : $"{apiUrl}/worlds/{worldId}/environments?api-version={ApiVersion}");

        private static FilesTarget BuildTarget(string connectionKey, string apiUrl, SceneRef scene, int? worldId, string worldLabel, EnvironmentDto env, bool seenThroughWorld)
        {
            FilesTarget target = new()
            {
                ConnectionKey = connectionKey,
                Scene = scene,
                WorldId = worldId,
                WorldLabel = worldLabel,
                EnvId = env.Id,
                EnvLabel = env.Label,
                Catalog = env.Catalog,
                CanWrite = seenThroughWorld ? false : env.CanWrite,
                SeenThroughWorld = seenThroughWorld,
                CardUri = Absolute(env.ThumbnailUri, apiUrl),
            };

            // The hero image is the thumbnail the Back Office and the app show; an environment that has only the small card is shown with it.
            string imageUri = Absolute(env.HeroImageUri, apiUrl) ?? target.CardUri;
            target.Thumbnail = new FileStatus { Kind = FileKind.Thumbnail, Uri = imageUri, Present = imageUri != null, Sha256 = NormalizeHash(env.HeroImageHash), HashFromPlatform = NormalizeHash(env.HeroImageHash) != null };

            string localizationUri = Absolute(env.LocalizationUri, apiUrl);
            target.Localization = new FileStatus { Kind = FileKind.Localization, Uri = localizationUri, Present = localizationUri != null, Sha256 = NormalizeHash(env.LocalizationHash), HashFromPlatform = NormalizeHash(env.LocalizationHash) != null };
            return target;
        }

        private static string NormalizeHash(string hash) => string.IsNullOrWhiteSpace(hash) ? null : hash.Trim().ToLowerInvariant();

        private static string Absolute(string uri, string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri absolute))
                return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps ? absolute.AbsoluteUri : null;

            return Uri.TryCreate(new Uri(apiUrl.TrimEnd('/') + "/"), uri.TrimStart('/'), out Uri combined) ? combined.AbsoluteUri : null;
        }

        private static async Task FillStatusesAsync(FilesListing listing, bool readFiles, CancellationToken ct)
        {
            Dictionary<string, string> index = PublishedFiles.Index();
            List<FileStatus> toRead = new();

            foreach (FileStatus status in listing.AllTargets.SelectMany(t => new[] { t.Thumbnail, t.Localization }))
            {
                if (status.Present && status.Sha256 != null)
                    status.ProjectAsset = index.TryGetValue(status.Sha256, out string asset) ? asset : null;
                else if (status.Present && readFiles)
                    toRead.Add(status);
            }

            using SemaphoreSlim slots = new(max_parallel_requests);
            await Task.WhenAll(toRead.Select(async status =>
            {
                await slots.WaitAsync(ct);
                try
                {
                    await HashByDownloadAsync(status, index, ct);
                }
                finally
                {
                    slots.Release();
                }
            }));
        }

        /// <summary>Downloads a file the platform gave no hash for, hashes it, keeps a copy in the cache folder and looks it up in the project.</summary>
        private static async Task HashByDownloadAsync(FileStatus status, Dictionary<string, string> index, CancellationToken ct)
        {
            Download download = await DownloadAsync(status.Uri, ct);
            if (!download.Found)
            {
                status.Present = false;
                return;
            }

            if (download.Error != null)
            {
                status.Error = download.Error;
                return;
            }

            status.Sha256 = Sha256(download.Bytes);
            status.HashFromPlatform = false;
            status.ProjectAsset = index.TryGetValue(status.Sha256, out string asset) ? asset : null;

            if (status.Kind == FileKind.Thumbnail)
            {
                ThumbnailCheck info = ThumbnailCheck.Check(download.Bytes);
                status.Width = info.Width;
                status.Height = info.Height;
                status.Format = info.Format;
            }

            status.CachePath = TryCache(download.Bytes, status.Sha256, status.Kind == FileKind.Thumbnail ? ImageExtension(download.Bytes) : ".csv");
        }

        private static string TryCache(byte[] bytes, string sha, string extension)
        {
            try
            {
                string path = Path.Combine(CacheFolder, sha + extension);
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(CacheFolder);
                    File.WriteAllBytes(path, bytes);
                }

                return path;
            }
            catch (Exception)
            {
                // A cache that cannot be written is a cache that is not there.
                return null;
            }
        }

        private static string ImageExtension(byte[] bytes) =>
            bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 ? ".png" : ".jpg";

        #endregion

        #region Anonymous downloads

        internal sealed class Download
        {
            /// <summary>False when the platform answered 404 or 410: the file is not there.</summary>
            public bool Found = true;

            public byte[] Bytes;

            /// <summary>Set when the file could not be read for another reason.</summary>
            public string Error;
        }

        /// <summary>
        /// Downloads a file from its public address. Never sends the session's token: the address is anonymous (the platform redirects
        /// it to the stored file), and the client used here has no way to attach one.
        /// </summary>
        public static async Task<Download> DownloadAsync(string uri, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
                return new Download { Found = false };

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, address);
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                using HttpResponseMessage response = await downloadClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
                    return new Download { Found = false };

                if (!response.IsSuccessStatusCode)
                    return new Download { Error = $"HTTP {(int)response.StatusCode}" };

                return new Download { Bytes = await response.Content.ReadAsByteArrayAsync() };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new Download { Error = ex.Message };
            }
        }

        #endregion

        #region Requests

        private static async Task<Read<T>> GetAsync<T>(string connectionKey, string url) where T : class
        {
            Read<T> read = new();
            try
            {
                HttpResponseMessage response = await EditorSessionManager.SendAuthorizedAsync(connectionKey,
                    () => new HttpRequestMessage(HttpMethod.Get, url), apiClient, allowInteractive: false);

                if (response == null)
                {
                    read.NoSession = true;
                    return read;
                }

                using (response)
                {
                    read.Status = (int)response.StatusCode;
                    string body = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                        read.Value = JsonConvert.DeserializeObject<T>(body);
                    else
                        read.Error = DescribeProblem(body, read.Status);
                }
            }
            catch (Exception ex)
            {
                read.Error = ex.Message;
            }

            return read;
        }

        /// <summary>A ProblemDetails answer in one line: its title or detail, then its first errors; the status code when it is not one.</summary>
        internal static string DescribeProblem(string body, int status)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(body) && JToken.Parse(body) is JObject problem)
                {
                    List<string> parts = new();
                    string title = (string)problem["title"];
                    string detail = (string)problem["detail"];
                    if (!string.IsNullOrWhiteSpace(title)) parts.Add(title);
                    if (!string.IsNullOrWhiteSpace(detail) && detail != title) parts.Add(detail);

                    if (problem["errors"] is JObject errors)
                    {
                        IEnumerable<string> lines = errors.Properties().Take(10).Select(p =>
                            $"{p.Name}: {(p.Value is JArray array ? string.Join(" ", array.Select(v => (string)v)) : (string)p.Value)}");
                        parts.AddRange(lines);
                    }

                    if (parts.Count > 0)
                        return string.Join(" ", parts);
                }
            }
            catch (Exception)
            {
                // Not JSON: the status code is all there is to say.
            }

            return $"HTTP {status}";
        }

        #endregion

        public static string Sha256(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            StringBuilder hex = new(64);
            foreach (byte b in sha.ComputeHash(bytes))
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));

            return hex.ToString();
        }

        /// <summary>The same as <see cref="Sha256(byte[])"/> without a leading UTF-8 BOM: the platform strips it before it stores (and hashes) a localization file.</summary>
        internal static string StoredSha256(byte[] bytes) =>
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? Sha256(bytes.Skip(3).ToArray()) : null;

        /// <summary>A connection key in a form fit for a file name.</summary>
        internal static string SlugOf(string connectionKey) =>
            Regex.Replace(Regex.Replace(connectionKey ?? "connection", @"^https?://", string.Empty, RegexOptions.IgnoreCase), @"[^A-Za-z0-9.]+", "-").Trim('-');
    }
}
