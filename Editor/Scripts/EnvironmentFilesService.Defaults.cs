using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>What a deploy would do with one scene's default files at one place (a dry run), or did.</summary>
    internal sealed class DefaultFilesPlan
    {
        public string Scene;
        public string Place;

        /// <summary>True when the scene is not published at the place yet: only then are the defaults sent.</summary>
        public bool IsNew;

        /// <summary>The default thumbnail and localization file of the scene, as project paths; null when the scene has none.</summary>
        public string Thumbnail;

        public string Localization;

        /// <summary>The thumbnail as it would be sent: when the default image has to be converted, the JPEG that goes online.</summary>
        public string ThumbnailUpload;

        public string ConversionNote;

        /// <summary>The checks' warnings and errors of the two files, one line each. An error means the file is not sent.</summary>
        public List<string> Warnings = new();

        public List<string> Errors = new();

        /// <summary>The line a scene without defaults gets in a deploy, when it is new there: it is published without them.</summary>
        public string Note;
    }

    internal static partial class EnvironmentFilesService
    {
        /// <summary>How long a deploy waits for an environment it just imported to appear in its place's list: the platform imports asynchronously.</summary>
        public const int DefaultsWaitSeconds = 180;

        private const int defaults_poll_seconds = 3;

        /// <summary>The scene's default files, as project paths (null for none): the fields of its entry in the scene list.</summary>
        public static (string Thumbnail, string Localization) DefaultsOf(string sceneKey, out string sceneName)
        {
            sceneName = sceneKey;
            SceneListScriptableObject.SceneConfiguration config = SceneListScriptableObject.Find()?.SceneConfigurations?
                .FirstOrDefault(c => c.Scene != null && string.Equals(c.SceneNameFiltered, sceneKey, StringComparison.OrdinalIgnoreCase));

            if (config == null)
                return (null, null);

            sceneName = config.Scene.name;
            string thumbnail = config.DefaultThumbnail != null ? AssetDatabase.GetAssetPath(config.DefaultThumbnail) : null;
            string localization = config.DefaultLocalization != null ? AssetDatabase.GetAssetPath(config.DefaultLocalization) : null;
            return (string.IsNullOrEmpty(thumbnail) ? null : thumbnail, string.IsNullOrEmpty(localization) ? null : localization);
        }

        /// <summary>What a place held before a deploy imported into it: the keys of its environments, or why they could not be read.</summary>
        internal sealed class ExistingKeys
        {
            /// <summary>The keys; null when the list could not be read (then the defaults are not sent: whether a scene is new is not known).</summary>
            public HashSet<string> Keys;

            public string Problem;
        }

        /// <summary>
        /// The keys of the environments a place holds now, read before a deploy imports into it, so that afterwards the scenes that were
        /// not there are the new ones. Null when no scene of the deploy has a default file (nothing to wait for).
        /// </summary>
        public static async Task<ExistingKeys> ExistingKeysAsync(string connectionKey, int? worldId, IEnumerable<string> sceneKeys)
        {
            if (!sceneKeys.Any(k => DefaultsOf(k, out _) != (null, null)))
                return null;

            string apiUrl = ApiUrlOrNull(connectionKey);
            if (apiUrl == null)
                return new ExistingKeys { Problem = "there is no application API address" };

            Read<List<EnvironmentDto>> read = await ReadPlaceAsync(connectionKey, apiUrl, worldId);
            if (!read.Ok)
                return new ExistingKeys { Problem = read.NoSession ? "the session ended" : read.Error ?? "the platform did not answer" };

            return new ExistingKeys { Keys = new HashSet<string>(read.Value.Where(e => worldId == null || e.IsTenant != true).Select(KeyOf), StringComparer.OrdinalIgnoreCase) };
        }

        private static string ApiUrlOrNull(string connectionKey)
        {
            string url = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            return string.IsNullOrEmpty(url) ? null : url;
        }

        /// <summary>
        /// What a deploy to a place would do with the default files of <paramref name="sceneKeys"/>, without sending or writing anything:
        /// the scenes that are new there, which files they would get, what the checks say. A scene already published there is not touched
        /// by a later deploy, so it is not listed.
        /// </summary>
        public static async Task<List<DefaultFilesPlan>> PlanDefaultsAsync(string connectionKey, int? worldId, string placeLabel, IReadOnlyList<string> sceneKeys,
                                                                           CancellationToken ct = default)
        {
            List<DefaultFilesPlan> plans = new();
            string apiUrl = ApiUrlOrNull(connectionKey);
            Read<List<EnvironmentDto>> read = apiUrl == null ? null : await ReadPlaceAsync(connectionKey, apiUrl, worldId);
            HashSet<string> existing = read != null && read.Ok
                ? new HashSet<string>(read.Value.Where(e => worldId == null || e.IsTenant != true).Select(KeyOf), StringComparer.OrdinalIgnoreCase)
                : null;

            List<string> languages = null;
            bool languagesRead = false;

            foreach (string key in sceneKeys)
            {
                ct.ThrowIfCancellationRequested();
                (string thumbnail, string localization) = DefaultsOf(key, out string name);
                DefaultFilesPlan plan = new() { Scene = name, Place = placeLabel, Thumbnail = thumbnail, Localization = localization };
                plans.Add(plan);

                if (existing == null)
                {
                    plan.Note = $"{name}: what the place holds could not be read, so whether it is new there is not known: its default files would not be sent.";
                    continue;
                }

                plan.IsNew = !existing.Contains(key);
                if (!plan.IsNew)
                    continue;

                // The sentences are the same ones the Publish Environments window says from the scene list (MissingDefaultsNote).
                plan.Note = MissingDefaultsNote(name, thumbnail, localization);
                if (thumbnail == null && localization == null)
                    continue;

                if (thumbnail != null)
                {
                    PreparedFile file = PrepareThumbnail(thumbnail, write: false);
                    plan.Errors.AddRange(file.Check.Errors.Select(e => $"{thumbnail}: {e}"));
                    plan.Warnings.AddRange(file.Check.Warnings.Select(w => $"{thumbnail}: {w}"));
                    plan.ThumbnailUpload = file.UploadAsset;
                    plan.ConversionNote = file.ConversionNote;
                }

                if (localization != null)
                {
                    if (!languagesRead)
                    {
                        languagesRead = true;
                        languages = (await GetTenantLanguagesAsync(connectionKey, ct)).Languages;
                    }

                    PreparedFile file = PrepareLocalization(localization, languages);
                    plan.Errors.AddRange(file.Check.Errors.Select(e => $"{localization}: {e}"));
                    plan.Warnings.AddRange(file.Check.Warnings.Select(w => $"{localization}: {w}"));
                }
            }

            return plans;
        }

        /// <summary>
        /// After a deploy imported scenes into a place: for each scene that was not there before (<paramref name="existingBefore"/>) and has default
        /// files, waits until its environment shows up in the place's list (the import runs on the platform afterwards: polled every few seconds, up to
        /// <see cref="DefaultsWaitSeconds"/>), then sends the defaults like a replace does. What happened is put in each outcome's
        /// <see cref="SceneDeployOutcome.DefaultFiles"/>. Never a deploy problem: the scene is published either way. Later deploys never come back here.
        /// </summary>
        /// <param name="stopReason">Asked between polls: a non-null answer ends the wait (the deploy's own time limit).</param>
        public static async Task ApplyDefaultsAfterImportAsync(string connectionKey, int? worldId, string placeLabel, ExistingKeys existing,
                                                               IReadOnlyList<SceneDeployOutcome> imported, Func<string> stopReason, Action<string> progress)
        {
            if (existing == null)
                return;

            if (existing.Keys == null)
            {
                // Whether a scene is new is not known: say so instead of silently leaving it without its defaults.
                foreach (SceneDeployOutcome outcome in imported.Where(o => o.ImportStarted))
                {
                    (string thumbnail, string localization) = DefaultsOf(outcome.Scene, out string name);
                    if (thumbnail != null || localization != null)
                        outcome.DefaultFiles = $"{name} was published in {placeLabel}, but what the place held before could not be read ({existing.Problem}), so whether the scene is new there is not known and its defaults were not sent. Set them from Environment Details.";
                }

                return;
            }

            HashSet<string> existingBefore = existing.Keys;

            Dictionary<string, SceneDeployOutcome> pending = new(StringComparer.OrdinalIgnoreCase);
            foreach (SceneDeployOutcome outcome in imported.Where(o => o.ImportStarted && !existingBefore.Contains(o.Scene)))
            {
                (string thumbnail, string localization) = DefaultsOf(outcome.Scene, out string name);
                if (thumbnail == null && localization == null)
                {
                    outcome.DefaultFiles = $"{name} has no default thumbnail or localization file: published without them.";
                    continue;
                }

                pending[outcome.Scene] = outcome;
            }

            if (pending.Count == 0)
                return;

            string apiUrl = ApiUrlOrNull(connectionKey);
            DateTime deadline = DateTime.UtcNow.AddSeconds(DefaultsWaitSeconds);
            List<string> languages = null;
            bool languagesRead = false;

            while (pending.Count > 0)
            {
                progress?.Invoke($"Waiting for the environment of {string.Join(", ", pending.Keys)} to appear...");
                Read<List<EnvironmentDto>> read = await ReadPlaceAsync(connectionKey, apiUrl, worldId);
                if (read.NoSession)
                {
                    foreach (SceneDeployOutcome outcome in pending.Values)
                    {
                        DefaultsOf(outcome.Scene, out string sceneName);
                        outcome.DefaultFiles = $"{sceneName} was published in {placeLabel}, but its defaults were not set: the session ended. Set them from Environment Details.";
                    }

                    return;
                }

                foreach (EnvironmentDto env in (read.Value ?? new List<EnvironmentDto>()).Where(e => worldId == null || e.IsTenant != true))
                {
                    string key = KeyOf(env);
                    if (!pending.TryGetValue(key, out SceneDeployOutcome outcome))
                        continue;

                    pending.Remove(key);
                    (string thumbnail, string localization) = DefaultsOf(key, out string name);
                    if (localization != null && !languagesRead)
                    {
                        languagesRead = true;
                        languages = (await GetTenantLanguagesAsync(connectionKey)).Languages;
                    }

                    outcome.DefaultFiles = await SendDefaultsAsync(
                        BuildTarget(connectionKey, apiUrl, new SceneRef { Name = name, Key = key }, worldId, placeLabel, env, false),
                        thumbnail, localization, languages, placeLabel);
                }

                if (pending.Count == 0 || stopReason?.Invoke() != null || DateTime.UtcNow >= deadline)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(defaults_poll_seconds));
            }

            foreach (SceneDeployOutcome outcome in pending.Values)
            {
                DefaultsOf(outcome.Scene, out string name);
                outcome.DefaultFiles = $"{name} was published in {placeLabel}, but its defaults were not set: the environment was not ready in time. Set them from Environment Details.";
            }
        }

        private static async Task<string> SendDefaultsAsync(FilesTarget target, string thumbnail, string localization, IReadOnlyList<string> languages, string placeLabel)
        {
            List<string> done = new();
            List<string> problems = new();

            foreach (PreparedFile file in new[]
            {
                thumbnail != null ? PrepareThumbnail(thumbnail) : null,
                localization != null ? PrepareLocalization(localization, languages) : null,
            }.Where(f => f != null))
            {
                string label = file.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file";
                if (!file.Check.Valid)
                {
                    problems.Add($"its default {label} ({file.SourceAsset}) is not valid and was not sent: {file.Check.Errors.First()}");
                    continue;
                }

                FileOutcome outcome = await ReplaceOneAsync(target, file, dryRun: false);
                if (outcome.Result == OutcomeKind.Updated || outcome.Result == OutcomeKind.Unchanged)
                    done.Add(label);
                else
                    problems.Add($"its {label} was not set: {outcome.Message}");
            }

            string name = target.Scene.Name;
            if (problems.Count == 0)
                return $"{name} at {placeLabel}: default {string.Join(" and ", done)} set.";

            return $"{name} was published in {placeLabel}, but {string.Join("; ", problems)}. Set them from Environment Details." +
                   (done.Count > 0 ? $" (Set: {string.Join(", ", done)}.)" : string.Empty);
        }
    }
}
