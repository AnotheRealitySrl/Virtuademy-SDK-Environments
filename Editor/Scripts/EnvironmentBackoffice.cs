using Newtonsoft.Json;

using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>The answer of <c>GET /tenants/backoffice-access/my</c> (meta-repo ADR 0038, decision 7).</summary>
    internal sealed class BackofficeAccess
    {
        [JsonProperty("canAccess")] public bool CanAccess;
        [JsonProperty("tenantLevel")] public bool TenantLevel;
        [JsonProperty("worldIds")] public List<int> WorldIds;

        public bool Covers(int worldId) => TenantLevel || (WorldIds?.Contains(worldId) ?? false);
    }

    /// <summary>
    /// The way into a tenant's Back Office, shared by the Publish Environments window (a button per connection) and the Environment
    /// Details window (a link per place): whether the account may enter, the tenant's address and the link that opens it on a world.
    /// </summary>
    internal static class EnvironmentBackoffice
    {
        /// <summary>
        /// The access when the account may enter, else null. The token goes to this connection's Application API only.
        /// A tenant with no Back Office address is not asked. An API older than the route answers 404 or 405, and is
        /// skipped without a word (the version rule of meta-repo ADR 0037); any other failure is logged and shows no
        /// button either.
        /// </summary>
        public static async Task<BackofficeAccess> LoadAccessAsync(string connectionKey)
        {
            string applicationApiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(applicationApiUrl) || UriOf(connectionKey) == null)
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
        public static Uri UriOf(string connectionKey)
        {
            string value = ConnectionStore.Get(connectionKey)?.BackofficeUrl;
            return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri
                : null;
        }

        /// <summary>The Back Office's home page, or its page for a world (<paramref name="worldId"/> not null).</summary>
        public static string LinkTo(Uri home, int? worldId)
        {
            if (worldId == null)
                return home.AbsoluteUri;

            UriBuilder builder = new(home);
            string query = builder.Query.TrimStart('?');
            builder.Query = (query.Length > 0 ? query + "&" : "") + "world=" + worldId.Value.ToString(CultureInfo.InvariantCulture);
            return builder.Uri.AbsoluteUri;
        }
    }
}
