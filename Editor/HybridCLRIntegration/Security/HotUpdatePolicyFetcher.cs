using Virtuademy.SDK.TenantConfiguration.Editor;
using Newtonsoft.Json;

using Virtuademy.SDK.Environments.Editor;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    /// <summary>
    /// Downloads the whitelist <c>policy.json</c> from the platform's public GET endpoint —
    /// the single source of truth shared with the backend — and caches it. The verifier calls
    /// this BEFORE checking a DLL, so it always validates against the current policy.
    ///
    /// The HOST is not hardcoded: it comes from the logged-in tenant's Application API URL
    /// (same source <c>AddressablesManagementWindow</c> uses). Only the path is fixed.
    /// </summary>
    public static class HotUpdatePolicyFetcher
    {
        // Only the path is fixed; the host is resolved from the logged-in tenant.
        private const string PolicyPath = "/scripts/hybrid-clr/policy?api-version=2";

        private const string CacheKey = "Virtuademy_HotUpdatePolicyCachedJson";

        public enum SourceKind { Fresh, Cached, None }

        public struct FetchResult
        {
            public HotUpdatePolicy Policy;

            /// <summary>
            /// The policy exactly as it arrived. Callers that need to notice the whitelist
            /// changing hash this, not the deserialized object: two runs of the deserializer
            /// give equal objects with no cheap way to compare them.
            /// </summary>
            public string Json;

            public SourceKind Source;
            public string Error;

            public bool Ok => Policy != null;
        }

        /// <summary>Full policy URL = the logged-in tenant's Application API URL + the fixed
        /// path, or null when not logged in / no URL in the tenant config.</summary>
        public static string ResolvePolicyUrl()
        {
            string baseUrl = EditorApiEndpoint.ApplicationApiUrl;
            return string.IsNullOrEmpty(baseUrl) ? null : baseUrl.TrimEnd('/') + PolicyPath;
        }

        /// <summary>Fetch fresh from the network; on failure fall back to the cached copy;
        /// if neither is available, fail-closed (Policy == null).</summary>
        public static async Task<FetchResult> FetchAsync()
        {
            string url = ResolvePolicyUrl();

            // 1) Network (fresh) — only if we could resolve a host (i.e. logged in).
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
                    using HttpResponseMessage resp = await client.GetAsync(url);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync();
                        HotUpdatePolicy policy = JsonConvert.DeserializeObject<HotUpdatePolicy>(json);
                        if (policy != null)
                        {
                            EditorPrefs.SetString(CacheKey, json);
                            return new FetchResult { Policy = policy, Json = json, Source = SourceKind.Fresh };
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[HotUpdateSecurity] Policy GET returned {(int)resp.StatusCode}. Trying cache.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HotUpdateSecurity] Policy fetch failed: {e.Message}. Trying cache.");
                }
            }

            // 2) Cache (stale).
            string cached = EditorPrefs.GetString(CacheKey, string.Empty);
            if (!string.IsNullOrEmpty(cached))
            {
                try
                {
                    HotUpdatePolicy policy = JsonConvert.DeserializeObject<HotUpdatePolicy>(cached);
                    if (policy != null)
                        return new FetchResult { Policy = policy, Json = cached, Source = SourceKind.Cached };
                }
                catch { /* corrupt cache */ }
            }

            // 3) Nothing → fail-closed.
            string why = string.IsNullOrEmpty(url)
                ? "Not logged in (no tenant Application API URL). Log in via 'Virtuademy / Available Tenants'."
                : $"Could not fetch the policy from {url} and no cached copy exists.";
            return new FetchResult { Source = SourceKind.None, Error = why };
        }
    }
}
