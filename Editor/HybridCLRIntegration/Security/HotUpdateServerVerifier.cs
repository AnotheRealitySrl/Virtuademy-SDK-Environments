using Newtonsoft.Json;
using Virtuademy.SDK.TenantConfiguration.Editor;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;

namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    /// <summary>
    /// Calls the AUTHORITATIVE backend verification (world-agnostic):
    /// <c>POST {ApplicationApiUrl}/scripts/hybrid-clr/verify</c> with the DLL as a multipart
    /// file and a Bearer token. 200 ⇒ passed, 422 ⇒ rejected (+ violations); anything else is
    /// treated as "could not verify" (Reachable=false) so the caller can fail-closed.
    /// </summary>
    public static class HotUpdateServerVerifier
    {
        private const string VerifyPath = "/scripts/hybrid-clr/verify?api-version=2";

        public sealed class ServerViolation
        {
            public string Kind;
            public string Detail;
            public string Location;
        }

        public sealed class ServerResponse
        {
            public bool Passed;
            public string Sha256;
            public List<ServerViolation> Violations;
        }

        public struct Result
        {
            public bool Reachable;      // got a definitive 200/422 answer from the server
            public bool Passed;         // meaningful only when Reachable
            public ServerResponse Response;
            public string Error;
        }

        public static async Task<Result> VerifyAsync(byte[] dll, string fileName)
        {
            string baseUrl = EditorApiEndpoint.ApplicationApiUrl;

            if (string.IsNullOrEmpty(baseUrl) || !EditorLoginState.HasSession)
            {
                return new Result
                {
                    Reachable = false,
                    Error = "Not logged in (no Application API URL / token). " +
                            "Log in via 'Virtuademy / Available Tenants'."
                };
            }

            string url = baseUrl.TrimEnd('/') + VerifyPath;

            try
            {
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };

                // Rebuilt per attempt: the session manager may replay the request after renewing
                // the token, and neither an HttpRequestMessage nor its content can be sent twice.
                //
                // The form is owned by the request: HttpRequestMessage.Dispose disposes its
                // Content. Wrapping it in its own 'using' as well disposes MultipartContent twice,
                // which throws a NullReferenceException on Mono.
                HttpRequestMessage BuildRequest()
                {
                    MultipartFormDataContent form = new();
                    ByteArrayContent file = new(dll);
                    file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    form.Add(file, "dll", string.IsNullOrEmpty(fileName) ? "HotUpdate.dll" : fileName);

                    return new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
                }

                using HttpResponseMessage resp = await EditorSessionManager.SendAuthorizedAsync(BuildRequest, client);

                if (resp == null)
                {
                    return new Result
                    {
                        Reachable = false,
                        Error = "The editor session expired and could not be renewed. " +
                                "Log in again via 'Virtuademy / Available Tenants'."
                    };
                }

                string body = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == HttpStatusCode.OK)
                    return new Result { Reachable = true, Passed = true, Response = TryParse(body) };

                if ((int)resp.StatusCode == 422) // UnprocessableEntity
                    return new Result { Reachable = true, Passed = false, Response = TryParse(body) };

                Debug.LogError($"Server verify returned {resp.StatusCode}: {body}");
                // 400 / 401 / 403 / 5xx → not a definitive policy verdict.
                return new Result { Reachable = false, Error = $"Server verify returned {(int)resp.StatusCode}: {body}" };
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new Result { Reachable = false, Error = "Server verify failed: " + e.Message };
            }
        }

        private static ServerResponse TryParse(string body)
        {
            try { return JsonConvert.DeserializeObject<ServerResponse>(body); }
            catch { return null; }
        }
    }
}
