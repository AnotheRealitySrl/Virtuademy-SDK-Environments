using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Virtuademy.SDK.TenantConfiguration.Editor;
using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// Where each environment name is already published, as the Application API sees it: the worlds
    /// holding an environment with that name and whether one exists at tenant level. Filled by the
    /// Addressables management window right after it loads the worlds, read by
    /// <see cref="SceneConfigurationDrawer"/> to say, next to every scene, "Published in …".
    /// </summary>
    /// <remarks>
    /// The key is the environment <c>fileName</c>, which the publish pipeline derives from the scene
    /// name exactly like <see cref="SceneListScriptableObject.SceneConfiguration.SceneNameFiltered"/>
    /// (lower-case, alphanumeric only): the bundle is <c>…_scenes_&lt;name&gt;.bundle</c> and the API
    /// records that name. Read-only; one refresh per window load or deploy, never per repaint.
    /// </remarks>
    internal static class PublishedEnvironmentsIndex
    {
        internal class Entry
        {
            public int? WorldId;
            public string WorldLabel;
            public bool Tenant;
            public string Catalog;
            public string Status;
            public DateTime LastUpdate;
        }

        private class EnvironmentDto
        {
            public int Id; public string Label; public string FileName; public string Catalog; public string Status;
            public int? WorldId; public bool? IsTenant; public DateTime LastUpdate;
        }

        private const string api_version = "2";
        private static readonly HttpClient httpClient = new();
        private static readonly Regex filter = new(@"[^a-z0-9]", RegexOptions.Compiled);
        private static Dictionary<string, List<Entry>> index = new(StringComparer.OrdinalIgnoreCase);
        private static List<(int Id, string Label)> lastWorlds = new();
        /// <summary>Bumped by <see cref="Clear"/>, so a refresh that started before it drops its results.</summary>
        private static int generation;

        /// <summary>Raised on the main thread when the index has been (re)built or cleared.</summary>
        public static event Action Changed;

        public static bool IsLoading { get; private set; }
        public static bool HasData { get; private set; }
        /// <summary>True when the account could not read the tenant-level environments (TenantManager role).</summary>
        public static bool TenantUnknown { get; private set; }

        /// <summary>The worlds the index was built for, in the order the API listed them.</summary>
        public static IReadOnlyList<(int Id, string Label)> Worlds => lastWorlds;

        // The connection the last full refresh was for: a refresh after a deploy asks the same platform.
        private static string lastConnection;

        public static string Key(string sceneName) => sceneName == null ? null : filter.Replace(sceneName.ToLowerInvariant(), string.Empty);

        public static IReadOnlyList<Entry> Get(string key)
        {
            return key != null && index.TryGetValue(key, out List<Entry> entries) ? entries : Array.Empty<Entry>();
        }

        /// <summary>Rebuilds the index for the given worlds (every world the account can see, not only the deployable ones).</summary>
        /// <param name="connectionKey">The connection the worlds belong to; null is the project's active one, read once here.</param>
        public static async Task RefreshAsync(IEnumerable<(int Id, string Label)> worlds, string connectionKey = null)
        {
            lastWorlds = worlds?.ToList() ?? new List<(int, string)>();
            lastConnection = connectionKey ?? ProjectConnections.ActiveKey;
            await RefreshAsync();
        }

        /// <summary>Rebuilds the index for the worlds of the last refresh (after a deploy).</summary>
        public static async Task RefreshAsync()
        {
            if (IsLoading) return;
            string connectionKey = lastConnection ?? ProjectConnections.ActiveKey;
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl)) return;

            int started = generation;
            IsLoading = true;
            Changed?.Invoke();
            Dictionary<string, List<Entry>> next = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach ((int id, string label) in lastWorlds)
                {
                    List<EnvironmentDto> envs = await GetAsync<List<EnvironmentDto>>(connectionKey, $"{apiUrl}/worlds/{id}/environments?api-version={api_version}");
                    if (started != generation) return;
                    if (envs == null) continue;
                    foreach (EnvironmentDto env in envs)
                    {
                        // a tenant environment shared to the world comes back with isTenant = true: count it once, as tenant
                        Add(next, env, env.IsTenant == true ? null : (id, label));
                    }
                }

                List<EnvironmentDto> tenantEnvs = await GetAsync<List<EnvironmentDto>>(connectionKey, $"{apiUrl}/tenants/environments?api-version={api_version}");

                // Cleared (logout) while the requests were in flight: what came back belongs to the old session.
                if (started != generation) return;

                TenantUnknown = tenantEnvs == null;
                if (tenantEnvs != null)
                {
                    foreach (EnvironmentDto env in tenantEnvs) { env.IsTenant = true; Add(next, env, null); }
                }

                index = next;
                HasData = true;
            }
            catch (Exception ex)
            {
                if (started == generation)
                    Debug.LogWarning($"[AddressablesManagement] Could not read where the environments are published: {ex.Message}");
            }
            finally
            {
                // A Clear in between already reset the state and may have let a newer refresh start.
                if (started == generation)
                {
                    IsLoading = false;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Whether an environment with this name exists in the given world (or at tenant level when <paramref name="worldId"/> is null).</summary>
        public static bool IsPublishedIn(string key, int? worldId)
        {
            return Get(key).Any(e => worldId == null ? e.Tenant : (!e.Tenant && e.WorldId == worldId));
        }

        /// <summary>Forgets everything from the current session (logout): the index, the worlds, any refresh in flight.</summary>
        public static void Clear()
        {
            generation++;
            index = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
            lastWorlds = new List<(int, string)>();
            HasData = false;
            IsLoading = false;
            TenantUnknown = false;
            Changed?.Invoke();
        }

        /// <summary>One line for the scene row, in the creator's words.</summary>
        public static string Summary(string key)
        {
            if (IsLoading && !HasData) return "Checking where it is published…";
            if (!HasData) return string.Empty;
            IReadOnlyList<Entry> entries = Get(key);
            if (entries.Count == 0) return TenantUnknown ? "Not published in your worlds" : "Not published yet";

            bool tenant = entries.Any(e => e.Tenant);
            List<string> worlds = entries.Where(e => !e.Tenant).Select(e => e.WorldLabel).Distinct().OrderBy(w => w).ToList();
            if (tenant && worlds.Count == 0) return "Published at tenant level";
            string where = worlds.Count <= 3 ? string.Join(", ", worlds) : $"{string.Join(", ", worlds.Take(3))} and {worlds.Count - 3} more";
            if (tenant) return $"Published at tenant level and in {where}";
            return worlds.Count == 1 ? $"Published in {where}" : $"Published in {worlds.Count} worlds: {where}";
        }

        /// <summary>Full detail for the tooltip: one line per place.</summary>
        public static string Details(string key)
        {
            IReadOnlyList<Entry> entries = Get(key);
            if (entries.Count == 0) return TenantUnknown ? "Tenant-level environments are not visible to your account." : "No published environment has this name.";
            return string.Join("\n", entries.OrderBy(e => e.Tenant ? -1 : e.WorldId ?? 0)
                .Select(e => $"{(e.Tenant ? "Tenant" : $"{e.WorldLabel} (world {e.WorldId})")} — catalog {e.Catalog}, {e.Status}, updated {e.LastUpdate:yyyy-MM-dd}"));
        }

        private static void Add(Dictionary<string, List<Entry>> target, EnvironmentDto env, (int Id, string Label)? world)
        {
            string key = Key(env.FileName ?? env.Label);
            if (string.IsNullOrEmpty(key)) return;
            if (!target.TryGetValue(key, out List<Entry> entries)) target[key] = entries = new List<Entry>();
            if (world == null && entries.Any(e => e.Tenant && e.Catalog == env.Catalog)) return;   // same tenant environment seen through several worlds
            entries.Add(new Entry
            {
                WorldId = world?.Id, WorldLabel = world?.Label, Tenant = world == null,
                Catalog = env.Catalog, Status = env.Status, LastUpdate = env.LastUpdate
            });
        }

        private static async Task<T> GetAsync<T>(string connectionKey, string url) where T : class
        {
            HttpResponseMessage response = await EditorSessionManager.SendAuthorizedAsync(connectionKey,
                () => new HttpRequestMessage(HttpMethod.Get, url), httpClient, allowInteractive: false);
            if (response == null || !response.IsSuccessStatusCode) return null;
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }
    }
}
