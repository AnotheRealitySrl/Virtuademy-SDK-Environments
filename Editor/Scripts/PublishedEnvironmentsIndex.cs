using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Virtuademy.SDK.TenantConfiguration.Editor;
using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// Where each environment name is already published, as the Application API of each connection sees it: the worlds
    /// holding an environment with that name and whether one exists at tenant level. Filled per connection by the
    /// Publish Environments window right after it loads that connection's worlds (and by the command line for the
    /// connection it acts on), read by <see cref="SceneConfigurationDrawer"/> to say, next to every scene,
    /// "Published in …", and by the deploy previews under each ticked world.
    /// </summary>
    /// <remarks>
    /// The key is the environment <c>fileName</c>, which the publish pipeline derives from the scene
    /// name exactly like <see cref="SceneListScriptableObject.SceneConfiguration.SceneNameFiltered"/>
    /// (lower-case, alphanumeric only): the bundle is <c>…_scenes_&lt;name&gt;.bundle</c> and the API
    /// records that name. A world id means something only within its connection, so every lookup names the connection;
    /// <see cref="Summary"/> and <see cref="Details"/> go across all of them, with the tenant's label when there is more than
    /// one. Read-only; one refresh per connection per window load or deploy, never per repaint.
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

        /// <summary>What is known about one connection.</summary>
        private sealed class State
        {
            public Dictionary<string, List<Entry>> Index = new(StringComparer.OrdinalIgnoreCase);
            public List<(int Id, string Label)> Worlds = new();
            public bool IsLoading;
            public bool HasData;
            public bool TenantUnknown;

            /// <summary>Bumped by <see cref="Clear"/>, so a refresh that started before it drops its results.</summary>
            public int Generation;
        }

        private const string api_version = "2";
        private const int max_parallel_requests = 4;
        private static readonly HttpClient httpClient = new();
        private static readonly Regex filter = new(@"[^a-z0-9]", RegexOptions.Compiled);

        // Per connection key, in the order the connections were first refreshed.
        private static readonly Dictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> order = new();

        /// <summary>Raised on the main thread when the index of a connection has been (re)built or cleared.</summary>
        public static event Action Changed;

        public static string Key(string sceneName) => sceneName == null ? null : filter.Replace(sceneName.ToLowerInvariant(), string.Empty);

        private static State StateOf(string connectionKey) =>
            connectionKey != null && states.TryGetValue(connectionKey, out State state) ? state : null;

        #region One connection

        public static bool IsLoadingFor(string connectionKey) => StateOf(connectionKey)?.IsLoading ?? false;

        public static bool HasDataFor(string connectionKey) => StateOf(connectionKey)?.HasData ?? false;

        /// <summary>True when the account could not read the tenant-level environments (TenantManager role).</summary>
        public static bool TenantUnknownFor(string connectionKey) => StateOf(connectionKey)?.TenantUnknown ?? false;

        /// <summary>The worlds the connection's index was built for, in the order the API listed them.</summary>
        public static IReadOnlyList<(int Id, string Label)> WorldsOf(string connectionKey) =>
            StateOf(connectionKey)?.Worlds ?? new List<(int, string)>();

        public static IReadOnlyList<Entry> Get(string connectionKey, string key)
        {
            State state = StateOf(connectionKey);
            return state != null && key != null && state.Index.TryGetValue(key, out List<Entry> entries) ? entries : Array.Empty<Entry>();
        }

        /// <summary>Whether an environment with this name exists in the given world of the connection (or at its tenant level when <paramref name="worldId"/> is null).</summary>
        public static bool IsPublishedIn(string connectionKey, string key, int? worldId)
        {
            return Get(connectionKey, key).Any(e => worldId == null ? e.Tenant : (!e.Tenant && e.WorldId == worldId));
        }

        /// <summary>Rebuilds the index of a connection for the given worlds (every world the account can see, not only the deployable ones).</summary>
        public static async Task RefreshAsync(IEnumerable<(int Id, string Label)> worlds, string connectionKey)
        {
            if (string.IsNullOrEmpty(connectionKey)) return;
            State state = Ensure(connectionKey);
            state.Worlds = worlds?.ToList() ?? new List<(int, string)>();
            await RefreshAsync(connectionKey);
        }

        /// <summary>Rebuilds the index of a connection for the worlds of its last refresh (after a deploy).</summary>
        public static async Task RefreshAsync(string connectionKey)
        {
            State state = StateOf(connectionKey);
            if (state == null || state.IsLoading) return;
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl)) return;

            int started = state.Generation;
            state.IsLoading = true;
            Changed?.Invoke();
            Dictionary<string, List<Entry>> next = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                // The worlds are read a few at a time, the tenant list alongside them; the results are added below in
                // the order the API listed the worlds, then the tenant list, as a sequential read added them.
                List<(int Id, string Label)> worlds = state.Worlds.ToList();
                using SemaphoreSlim slots = new(max_parallel_requests);
                Task<List<EnvironmentDto>[]> worldsRead = Task.WhenAll(worlds.Select(async world =>
                {
                    await slots.WaitAsync();
                    try
                    {
                        if (started != state.Generation) return null;
                        return await GetAsync<List<EnvironmentDto>>(connectionKey, $"{apiUrl}/worlds/{world.Id}/environments?api-version={api_version}");
                    }
                    finally
                    {
                        slots.Release();
                    }
                }));
                Task<List<EnvironmentDto>> tenantRead = GetAsync<List<EnvironmentDto>>(connectionKey, $"{apiUrl}/tenants/environments?api-version={api_version}");
                await Task.WhenAll(worldsRead, tenantRead);

                // Cleared (logout) while the requests were in flight: what came back belongs to the old session.
                if (started != state.Generation) return;

                List<EnvironmentDto>[] worldEnvs = worldsRead.Result;
                for (int i = 0; i < worlds.Count; i++)
                {
                    if (worldEnvs[i] == null) continue;
                    foreach (EnvironmentDto env in worldEnvs[i])
                    {
                        // a tenant environment shared to the world comes back with isTenant = true: count it once, as tenant
                        Add(next, env, env.IsTenant == true ? null : worlds[i]);
                    }
                }

                List<EnvironmentDto> tenantEnvs = tenantRead.Result;

                state.TenantUnknown = tenantEnvs == null;
                if (tenantEnvs != null)
                {
                    foreach (EnvironmentDto env in tenantEnvs) { env.IsTenant = true; Add(next, env, null); }
                }

                state.Index = next;
                state.HasData = true;
            }
            catch (Exception ex)
            {
                if (started == state.Generation)
                    Debug.LogWarning($"[PublishEnvironments] Could not read where the environments are published ({LabelOf(connectionKey)}): {ex.Message}");
            }
            finally
            {
                // A Clear in between already reset the state and may have let a newer refresh start.
                if (started == state.Generation)
                {
                    state.IsLoading = false;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Forgets what is known about one connection (its logout): the index, the worlds, any refresh in flight.</summary>
        public static void Clear(string connectionKey)
        {
            State state = StateOf(connectionKey);
            if (state == null) return;
            Reset(state);
            Changed?.Invoke();
        }

        /// <summary>Forgets every connection.</summary>
        public static void ClearAll()
        {
            foreach (State state in states.Values)
                Reset(state);
            Changed?.Invoke();
        }

        private static void Reset(State state)
        {
            state.Generation++;
            state.Index = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
            state.Worlds = new List<(int, string)>();
            state.HasData = false;
            state.IsLoading = false;
            state.TenantUnknown = false;
        }

        private static State Ensure(string connectionKey)
        {
            if (!states.TryGetValue(connectionKey, out State state))
            {
                states[connectionKey] = state = new State();
                order.Add(connectionKey);
            }

            return state;
        }

        #endregion

        #region Across connections

        /// <summary>The connections whose index has data, in the order they were first refreshed.</summary>
        public static IReadOnlyList<string> ConnectionsWithData => order.Where(HasDataFor).ToList();

        public static bool AnyData => order.Any(HasDataFor);

        public static bool AnyLoading => order.Any(IsLoadingFor);

        /// <summary>Whether an environment with this name exists anywhere a connection with data can see.</summary>
        public static bool IsPublishedAnywhere(string key) => ConnectionsWithData.Any(c => Get(c, key).Count > 0);

        /// <summary>One line for the scene row, in the creator's words; one part per connection when there are several.</summary>
        public static string Summary(string key)
        {
            List<string> withData = ConnectionsWithData.ToList();
            if (withData.Count == 0) return AnyLoading ? "Checking where it is published…" : string.Empty;
            if (withData.Count == 1) return SummaryFor(withData[0], key);
            return string.Join("; ", withData.Select(c => $"{LabelOf(c)}: {SummaryFor(c, key)}"));
        }

        /// <summary>Full detail for the tooltip: one line per place, with the tenant when there are several connections.</summary>
        public static string Details(string key)
        {
            List<string> withData = ConnectionsWithData.ToList();
            if (withData.Count == 0) return string.Empty;
            if (withData.Count == 1) return DetailsFor(withData[0], key);
            return string.Join("\n", withData.Select(c => $"{LabelOf(c)}:\n{DetailsFor(c, key)}"));
        }

        private static string SummaryFor(string connectionKey, string key)
        {
            IReadOnlyList<Entry> entries = Get(connectionKey, key);
            bool tenantUnknown = TenantUnknownFor(connectionKey);
            if (entries.Count == 0) return tenantUnknown ? "Not published in your worlds" : "Not published yet";

            bool tenant = entries.Any(e => e.Tenant);
            List<string> worlds = entries.Where(e => !e.Tenant).Select(e => e.WorldLabel).Distinct().OrderBy(w => w).ToList();
            if (tenant && worlds.Count == 0) return "Published at tenant level";
            string where = worlds.Count <= 3 ? string.Join(", ", worlds) : $"{string.Join(", ", worlds.Take(3))} and {worlds.Count - 3} more";
            if (tenant) return $"Published at tenant level and in {where}";
            return worlds.Count == 1 ? $"Published in {where}" : $"Published in {worlds.Count} worlds: {where}";
        }

        private static string DetailsFor(string connectionKey, string key)
        {
            IReadOnlyList<Entry> entries = Get(connectionKey, key);
            if (entries.Count == 0) return TenantUnknownFor(connectionKey) ? "Tenant-level environments are not visible to your account." : "No published environment has this name.";
            return string.Join("\n", entries.OrderBy(e => e.Tenant ? -1 : e.WorldId ?? 0)
                .Select(e => $"{(e.Tenant ? "Tenant" : $"{e.WorldLabel} (world {e.WorldId})")} — catalog {e.Catalog}, {e.Status}, updated {e.LastUpdate:yyyy-MM-dd}"));
        }

        /// <summary>The tenant label and environment a connection is shown with.</summary>
        internal static string LabelOf(string connectionKey)
        {
            ConnectionRecord record = ConnectionStore.Get(connectionKey);
            if (record == null) return connectionKey;
            return string.IsNullOrEmpty(record.Environment) ? record.TenantLabel : $"{record.TenantLabel} ({record.Environment})";
        }

        #endregion

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
