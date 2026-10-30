using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Virtuademy.SDK.TenantConfiguration.Editor;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>How a call of <see cref="EnvironmentExperiencesService"/> ended.</summary>
    public enum EnvironmentExperiencesStatus
    {
        /// <summary>The platform did what was asked.</summary>
        Ok,

        /// <summary>The login may not do it in that world (the platform answered 403, with or without a reason).</summary>
        NotAllowed,

        /// <summary>The tenant's API has no such route: 404 or 405 with no <c>code</c>. It cannot be done from the SDK there.</summary>
        NotSupportedByTenant,

        /// <summary>The environment is not published in that world, or the login cannot read it there.</summary>
        NotPublishedInWorld,

        /// <summary>No experience with that id in that world that the login can read.</summary>
        ExperienceNotFound,

        /// <summary>The world has no session capacity, which a Core experience is sized on.</summary>
        WorldHasNoCapacity,

        /// <summary>The connection has no session, or it could not be renewed silently.</summary>
        LoginRequired,

        /// <summary>The platform could not be reached: no API address, or the request did not complete.</summary>
        Unreachable,

        /// <summary>The platform answered something this service does not map: <see cref="EnvironmentExperiencesResult{T}.Message"/> and <see cref="EnvironmentExperiencesResult{T}.HttpStatus"/> say what.</summary>
        Failed,
    }

    /// <summary>The publication state of an experience: <c>Published</c> is in the catalog, which the Landing lists.</summary>
    public enum ExperiencePublicationStatus
    {
        Draft,
        Published,
    }

    /// <summary>An environment of a world, as <c>GET /worlds/{wid}/environments</c> lists it.</summary>
    public sealed class WorldEnvironment
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public string FileName { get; set; }

        /// <summary>A tenant-level environment shared with the world: it counts as in the world.</summary>
        public bool? IsTenant { get; set; }

        public string Catalog { get; set; }
        public string Status { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>The experiences of an environment in a world, by type and state.</summary>
    public sealed class ExperienceCounts
    {
        public int CorePublished { get; set; }
        public int CoreDraft { get; set; }
        public int AuthoredPublished { get; set; }
        public int AuthoredDraft { get; set; }
        public int ExternalApp { get; set; }

        /// <summary>Every experience that uses the environment in the world.</summary>
        [JsonIgnore]
        public int Total => CorePublished + CoreDraft + AuthoredPublished + AuthoredDraft + ExternalApp;

        /// <summary>Core experiences of every author and state, the ones the caller is not shown included.</summary>
        [JsonIgnore]
        public int CoreTotal => CorePublished + CoreDraft;
    }

    /// <summary>One Core experience of the environment that the caller is shown.</summary>
    public sealed class CoreExperienceItem
    {
        public int Id { get; set; }
        public string Label { get; set; }

        /// <summary><c>Draft</c> or <c>Published</c>, as the platform writes it.</summary>
        public string Status { get; set; }

        /// <summary>The caller is the author.</summary>
        public bool IsMine { get; set; }

        /// <summary>The caller is the author or the world's Owner, so setting its state would be allowed.</summary>
        public bool CanChangeStatus { get; set; }
    }

    /// <summary>What <c>GET …/environments/{envId}/experiences</c> answers.</summary>
    public class EnvironmentExperiences
    {
        public int EnvironmentId { get; set; }
        public int WorldId { get; set; }
        public ExperienceCounts Counts { get; set; } = new();
        public List<CoreExperienceItem> CoreExperiences { get; set; } = new();

        /// <summary>Core drafts of other authors that the caller is not shown (always 0 for a world's Owner).</summary>
        public int CoreHiddenDrafts { get; set; }

        /// <summary>The caller may create a Core experience in the world. It also needs that none exists yet.</summary>
        public bool CanCreateCore { get; set; }

        /// <summary>No Core experience exists yet, of any author and state.</summary>
        [JsonIgnore]
        public bool HasNoCore => Counts == null || Counts.CoreTotal == 0;
    }

    /// <summary>What <c>POST …/environments/{envId}/experiences/core</c> answers: the experiences as they are after it, and whether it created one.</summary>
    public sealed class CoreExperienceOutcome : EnvironmentExperiences
    {
        public bool Created { get; set; }

        /// <summary>Present only when <see cref="Created"/> is true.</summary>
        public int? CreatedId { get; set; }
    }

    /// <summary>What <c>PUT …/experiences/{expId}/status</c> answers.</summary>
    public sealed class ExperienceStatusOutcome
    {
        public int Id { get; set; }

        /// <summary>The state the experience has now.</summary>
        public string Status { get; set; }

        /// <summary>False when it already had the requested state.</summary>
        public bool Changed { get; set; }
    }

    /// <summary>The answer of every call of <see cref="EnvironmentExperiencesService"/>: how it ended, why in words, and the data when there is any.</summary>
    public sealed class EnvironmentExperiencesResult<T> where T : class
    {
        public EnvironmentExperiencesStatus Status;

        /// <summary>One sentence for the creator, in English. It never names a platform version.</summary>
        public string Message;

        public T Data;

        public int WorldId;

        /// <summary>Set once the environment is known (resolved from the scene, or given).</summary>
        public int? EnvironmentId;

        /// <summary>The HTTP status of the answer; 0 when there was none.</summary>
        public int HttpStatus;

        /// <summary>
        /// The platform's <c>code</c> (<c>NotAllowedInWorld</c>, <c>EnvironmentNotInWorld</c>, <c>ExperienceNotFound</c>,
        /// <c>WorldHasNoCapacity</c>), or null when the answer had none: a 403 with no code is the role filter of the API, a
        /// 403 with <c>NotAllowedInWorld</c> the check of the facets in the world. Both are <see cref="EnvironmentExperiencesStatus.NotAllowed"/>
        /// with the same message.
        /// </summary>
        public string ServerCode;

        public bool Ok => Status == EnvironmentExperiencesStatus.Ok;
    }

    /// <summary>
    /// What the SDK can know and do about the experiences of an environment published in a world: which ones use it and how
    /// many, whether a Core experience exists, creating it, and setting an experience's state. A thin client of three routes of the
    /// Application API (contract <c>contracts/openapi/environment-experiences.yaml</c> of the meta-repo), used by the command line
    /// (<c>experiences</c>, <c>create-core</c>, <c>experience-status</c>) and meant for the Publish Environments window.
    /// </summary>
    /// <remarks>
    /// Every method names the connection it acts on, calls the API address that connection's tenant reported, and goes through
    /// <see cref="EditorSessionManager.SendAuthorizedAsync(string, Func{HttpRequestMessage}, HttpClient, bool)"/> without an interactive
    /// login: a session that cannot be renewed silently is <see cref="EnvironmentExperiencesStatus.LoginRequired"/>. Nothing here throws for
    /// a platform answer; a failure is a status. The permission to do something is the platform's to decide, per world: a login that may
    /// not is <see cref="EnvironmentExperiencesStatus.NotAllowed"/>, and a tenant whose API has no such route answers
    /// <see cref="EnvironmentExperiencesStatus.NotSupportedByTenant"/> (a 404 or 405 with no <c>code</c>). Read-only calls are not cached.
    /// </remarks>
    public static class EnvironmentExperiencesService
    {
        public const string NotSupportedMessage = "This tenant cannot show or change the experiences of an environment from the SDK yet: use the Back Office.";
        public const string NotAllowedReadMessage = "You cannot see the experiences of this environment in this world: it needs the permission to manage environments or Core Experiences there.";
        public const string NotAllowedCreateMessage = "You cannot create a Core Experience in this world: it needs the permission to manage Core Experiences there.";
        public const string NotAllowedStateMessage = "Only the experience's author or the world's Owner can change its state.";
        public const string NotAllowedEnvironmentsMessage = "You cannot see the environments of this world.";
        public const string NoCapacityMessage = "This world has no session capacity set, so a Core experience cannot be created here. Ask its Owner to set it in the Back Office.";
        public const string NotPublishedMessage = "This environment is not published in this world: deploy it there first.";
        public const string ExperienceNotFoundMessage = "This experience does not exist in this world, or you cannot see it.";
        public const string LoginRequiredMessage = "Your session is not valid and could not be renewed silently: log in again.";

        private const string api_version = "2";
        private static readonly HttpClient httpClient = new();

        private enum Operation
        {
            ListEnvironments,
            Read,
            CreateCore,
            SetState,
        }

        #region Environments of a world

        /// <summary>The environments of a world (<c>GET /worlds/{wid}/environments</c>): the ones published there and the tenant-level ones shared with it.</summary>
        public static async Task<EnvironmentExperiencesResult<List<WorldEnvironment>>> ListEnvironmentsAsync(string connectionKey, int worldId)
        {
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl))
                return NoApiUrl<List<WorldEnvironment>>(worldId, null);

            string url = $"{apiUrl}/worlds/{worldId}/environments?api-version={api_version}";
            return await SendAsync<List<WorldEnvironment>>(connectionKey, Operation.ListEnvironments, worldId, null,
                () => new HttpRequestMessage(HttpMethod.Get, url));
        }

        /// <summary>
        /// The environment of a scene among the ones <see cref="ListEnvironmentsAsync"/> returned: the one whose <c>fileName</c> is the
        /// scene's <see cref="SceneListScriptableObject.SceneConfiguration.SceneNameFiltered"/> (lower-case, alphanumeric only, which is how the
        /// publish pipeline names it). <paramref name="sceneName"/> may be the scene's name or already that filtered form. When several
        /// match (the same name at tenant level and in the world, or in two catalogs) the world's own wins, then the most recently updated.
        /// </summary>
        public static EnvironmentExperiencesResult<WorldEnvironment> ResolveEnvironment(IEnumerable<WorldEnvironment> environments, int worldId, string sceneName)
        {
            string key = PublishedEnvironmentsIndex.Key(sceneName);
            WorldEnvironment match = string.IsNullOrEmpty(key)
                ? null
                : (environments ?? Enumerable.Empty<WorldEnvironment>())
                    .Where(e => PublishedEnvironmentsIndex.Key(e.FileName ?? e.Label) == key)
                    .OrderBy(e => e.IsTenant == true ? 1 : 0)
                    .ThenByDescending(e => e.LastUpdate)
                    .FirstOrDefault();

            if (match == null)
                return Result<WorldEnvironment>(EnvironmentExperiencesStatus.NotPublishedInWorld, NotPublishedMessage, worldId, null);

            EnvironmentExperiencesResult<WorldEnvironment> result = Result<WorldEnvironment>(EnvironmentExperiencesStatus.Ok, $"Environment {match.Id} in world {worldId}.", worldId, match.Id);
            result.Data = match;
            return result;
        }

        #endregion

        #region Experiences

        /// <summary>
        /// Which experiences use an environment in a world, how many by type and state, and what the login may do about them
        /// (<c>GET /worlds/{wid}/environments/{envId}/experiences</c>).
        /// </summary>
        public static async Task<EnvironmentExperiencesResult<EnvironmentExperiences>> GetExperiencesAsync(string connectionKey, int worldId, int environmentId)
        {
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl))
                return NoApiUrl<EnvironmentExperiences>(worldId, environmentId);

            string url = $"{apiUrl}/worlds/{worldId}/environments/{environmentId}/experiences?api-version={api_version}";
            return await SendAsync<EnvironmentExperiences>(connectionKey, Operation.Read, worldId, environmentId,
                () => new HttpRequestMessage(HttpMethod.Get, url));
        }

        /// <summary>The same for a scene: finds its environment in the world first. <see cref="EnvironmentExperiencesStatus.NotPublishedInWorld"/> when it has none there.</summary>
        public static async Task<EnvironmentExperiencesResult<EnvironmentExperiences>> GetExperiencesForSceneAsync(string connectionKey, int worldId, string sceneName)
        {
            EnvironmentExperiencesResult<WorldEnvironment> resolved = await ResolveSceneAsync(connectionKey, worldId, sceneName);
            return resolved.Ok
                ? await GetExperiencesAsync(connectionKey, worldId, resolved.Data.Id)
                : Carry<WorldEnvironment, EnvironmentExperiences>(resolved);
        }

        /// <summary>
        /// Makes sure the environment has a Core experience in the world, creating one when none exists
        /// (<c>POST /worlds/{wid}/environments/{envId}/experiences/core</c>). Idempotent: when one exists, in any state and of any author,
        /// nothing is created or changed (<see cref="CoreExperienceOutcome.Created"/> is false), its state included.
        /// </summary>
        /// <param name="label">The new experience's label (1 to 100 characters); null or empty is the environment's label.</param>
        /// <param name="published">Create it already in the catalog; otherwise it is a draft.</param>
        public static async Task<EnvironmentExperiencesResult<CoreExperienceOutcome>> EnsureCoreAsync(string connectionKey, int worldId, int environmentId,
                                                                                                    string label = null, bool published = false)
        {
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl))
                return NoApiUrl<CoreExperienceOutcome>(worldId, environmentId);

            string url = $"{apiUrl}/worlds/{worldId}/environments/{environmentId}/experiences/core?api-version={api_version}";
            string json = JsonConvert.SerializeObject(string.IsNullOrWhiteSpace(label)
                ? new Dictionary<string, object> { { "published", published } }
                : new Dictionary<string, object> { { "label", label.Trim() }, { "published", published } });

            EnvironmentExperiencesResult<CoreExperienceOutcome> result = await SendAsync<CoreExperienceOutcome>(connectionKey, Operation.CreateCore, worldId, environmentId,
                () => new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

            if (result.Ok)
                result.Message = result.Data.Created
                    ? $"Created the Core experience {result.Data.CreatedId} in world {worldId}."
                    : $"A Core experience already exists in world {worldId}: nothing was created or changed.";
            return result;
        }

        /// <summary>The same for a scene: finds its environment in the world first.</summary>
        public static async Task<EnvironmentExperiencesResult<CoreExperienceOutcome>> EnsureCoreForSceneAsync(string connectionKey, int worldId, string sceneName,
                                                                                                           string label = null, bool published = false)
        {
            EnvironmentExperiencesResult<WorldEnvironment> resolved = await ResolveSceneAsync(connectionKey, worldId, sceneName);
            return resolved.Ok
                ? await EnsureCoreAsync(connectionKey, worldId, resolved.Data.Id, label, published)
                : Carry<WorldEnvironment, CoreExperienceOutcome>(resolved);
        }

        /// <summary>
        /// Sets an experience's publication state (<c>PUT /worlds/{wid}/experiences/{expId}/status</c>). Idempotent: setting the state it
        /// already has changes nothing (<see cref="ExperienceStatusOutcome.Changed"/> is false). Allowed to the experience's author and to the
        /// world's Owner; anyone else is <see cref="EnvironmentExperiencesStatus.NotAllowed"/>.
        /// </summary>
        public static async Task<EnvironmentExperiencesResult<ExperienceStatusOutcome>> SetStatusAsync(string connectionKey, int worldId, int experienceId,
                                                                                                     ExperiencePublicationStatus status)
        {
            string apiUrl = EditorApiEndpoint.ApplicationApiUrlOf(connectionKey);
            if (string.IsNullOrEmpty(apiUrl))
                return NoApiUrl<ExperienceStatusOutcome>(worldId, null);

            string url = $"{apiUrl}/worlds/{worldId}/experiences/{experienceId}/status?api-version={api_version}";
            string json = JsonConvert.SerializeObject(new Dictionary<string, object> { { "status", status.ToString() } });

            EnvironmentExperiencesResult<ExperienceStatusOutcome> result = await SendAsync<ExperienceStatusOutcome>(connectionKey, Operation.SetState, worldId, null,
                () => new HttpRequestMessage(HttpMethod.Put, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

            if (result.Ok)
                result.Message = result.Data.Changed
                    ? $"Experience {experienceId} is now {result.Data.Status}."
                    : $"Experience {experienceId} was already {result.Data.Status}: nothing was changed.";
            return result;
        }

        #endregion

        private static async Task<EnvironmentExperiencesResult<WorldEnvironment>> ResolveSceneAsync(string connectionKey, int worldId, string sceneName)
        {
            EnvironmentExperiencesResult<List<WorldEnvironment>> listing = await ListEnvironmentsAsync(connectionKey, worldId);
            return listing.Ok
                ? ResolveEnvironment(listing.Data, worldId, sceneName)
                : Carry<List<WorldEnvironment>, WorldEnvironment>(listing);
        }

        #region Sending and reading

        private sealed class Problem
        {
            public string Code { get; set; }
            public string Title { get; set; }
        }

        private static async Task<EnvironmentExperiencesResult<T>> SendAsync<T>(string connectionKey, Operation operation, int worldId, int? environmentId,
                                                                                Func<HttpRequestMessage> request) where T : class
        {
            int http = 0;
            string body;
            try
            {
                using HttpResponseMessage response = await EditorSessionManager.SendAuthorizedAsync(connectionKey, request, httpClient, allowInteractive: false);

                // Null is "no session", which is not a failed request.
                if (response == null)
                    return Result<T>(EnvironmentExperiencesStatus.LoginRequired, LoginRequiredMessage, worldId, environmentId);

                http = (int)response.StatusCode;
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
            {
                return Result<T>(EnvironmentExperiencesStatus.Unreachable, "The platform could not be reached: " + ex.Message, worldId, environmentId);
            }

            if (http >= 200 && http < 300)
            {
                try
                {
                    T data = JsonConvert.DeserializeObject<T>(body);
                    if (data == null)
                        return Result<T>(EnvironmentExperiencesStatus.Failed, "The platform answered with no data.", worldId, environmentId, http);

                    // The answer names the environment and the world; keep what the caller asked for when it does not.
                    EnvironmentExperiencesResult<T> ok = Result<T>(EnvironmentExperiencesStatus.Ok, "Done.", worldId, environmentId, http);
                    ok.Data = data;
                    return ok;
                }
                catch (JsonException)
                {
                    return Result<T>(EnvironmentExperiencesStatus.Failed, "The platform's answer could not be read.", worldId, environmentId, http);
                }
            }

            Problem problem = ReadProblem(body);
            string code = string.IsNullOrWhiteSpace(problem?.Code) ? null : problem.Code;
            EnvironmentExperiencesResult<T> failure = Classify<T>(operation, http, code, problem?.Title, worldId, environmentId);
            failure.ServerCode = code;
            return failure;
        }

        private static Problem ReadProblem(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<Problem>(body);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The one place an HTTP answer becomes a status. A 404 or 405 with no <c>code</c> on a route of this service is a route the tenant's API
        /// lacks; a 404 with a <c>code</c> is the route answering. The 404 of <c>GET /worlds/{wid}/environments</c> is a world that is not there.
        /// </summary>
        private static EnvironmentExperiencesResult<T> Classify<T>(Operation operation, int http, string code, string title, int worldId, int? environmentId) where T : class
        {
            EnvironmentExperiencesStatus status;
            string message;

            switch (http)
            {
                case 401:
                    status = EnvironmentExperiencesStatus.LoginRequired;
                    message = LoginRequiredMessage;
                    break;

                case 403:
                    status = EnvironmentExperiencesStatus.NotAllowed;
                    message = operation switch
                    {
                        Operation.CreateCore => NotAllowedCreateMessage,
                        Operation.SetState => NotAllowedStateMessage,
                        Operation.ListEnvironments => NotAllowedEnvironmentsMessage,
                        _ => NotAllowedReadMessage,
                    };
                    break;

                case 404 when operation == Operation.ListEnvironments:
                    status = EnvironmentExperiencesStatus.Failed;
                    message = $"World {worldId} was not found, or this login cannot see it.";
                    break;

                case 404 when code == "EnvironmentNotInWorld":
                    status = EnvironmentExperiencesStatus.NotPublishedInWorld;
                    message = NotPublishedMessage;
                    break;

                case 404 when code == "ExperienceNotFound":
                    status = EnvironmentExperiencesStatus.ExperienceNotFound;
                    message = ExperienceNotFoundMessage;
                    break;

                case 404 when code == null:
                case 405 when operation != Operation.ListEnvironments:
                    status = EnvironmentExperiencesStatus.NotSupportedByTenant;
                    message = NotSupportedMessage;
                    break;

                case 400 when code == "WorldHasNoCapacity":
                    status = EnvironmentExperiencesStatus.WorldHasNoCapacity;
                    message = NoCapacityMessage;
                    break;

                default:
                    status = EnvironmentExperiencesStatus.Failed;
                    message = $"The platform answered with an error ({http}){(string.IsNullOrWhiteSpace(title) ? string.Empty : ": " + title)}.";
                    break;
            }

            return Result<T>(status, message, worldId, environmentId, http);
        }

        private static EnvironmentExperiencesResult<T> Result<T>(EnvironmentExperiencesStatus status, string message, int worldId, int? environmentId, int http = 0) where T : class =>
            new() { Status = status, Message = message, WorldId = worldId, EnvironmentId = environmentId, HttpStatus = http };

        private static EnvironmentExperiencesResult<T> NoApiUrl<T>(int worldId, int? environmentId) where T : class =>
            Result<T>(EnvironmentExperiencesStatus.Unreachable, "The connection's tenant reported no Application API address.", worldId, environmentId);

        /// <summary>A failed step's answer, for the call that depended on it.</summary>
        private static EnvironmentExperiencesResult<TTo> Carry<TFrom, TTo>(EnvironmentExperiencesResult<TFrom> from) where TFrom : class where TTo : class =>
            new()
            {
                Status = from.Status,
                Message = from.Message,
                WorldId = from.WorldId,
                EnvironmentId = from.EnvironmentId,
                HttpStatus = from.HttpStatus,
                ServerCode = from.ServerCode,
            };

        #endregion
    }
}
