using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;

using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The three commands about the experiences of an environment published in a world: <c>experiences</c>, <c>create-core</c> and
    /// <c>experience-status</c>. They are thin: the calls, the permission rule and the wording are those of
    /// <see cref="EnvironmentExperiencesService"/>; this part reads the arguments, applies the connection and version checks the
    /// other commands that call the platform apply, and turns the outcomes into a REPORT and an exit code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Commands.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>experiences -vdWorlds &lt;ids&gt; [-vdScenes a,b] [-vdConnection &lt;connection&gt;] [-vdExpectTenant &lt;label&gt;]</c>:
    /// for every scene (the included ones, or <c>-vdScenes</c>'s) in every world, the environment's id there or that it is not published
    /// there, how many experiences use it by type and state, the Core experiences the login is shown (state, whether it is the author's,
    /// whether it may change the state), whether it may create one, and how many Core drafts of other authors it is not shown. Read-only.</description></item>
    /// <item><description><c>create-core -vdWorlds &lt;ids&gt; -vdScenes a,b [-vdLabel &lt;text&gt;] [-vdPublished] [-vdDryRun] [-vdConnection &lt;connection&gt;] [-vdExpectTenant &lt;label&gt;]</c>:
    /// makes sure each scene's environment has a Core experience in each world, creating one only where none exists (of any author and state),
    /// as a draft unless <c>-vdPublished</c>. Run twice, it creates once: the second run reports <c>alreadyExisted</c>. <c>-vdLabel</c> is the new
    /// experience's label (the environment's when absent) and needs exactly one scene. <c>-vdDryRun</c> reads and says what it would do.</description></item>
    /// <item><description><c>experience-status -vdWorlds &lt;one id&gt; -vdExperience &lt;id&gt; -vdStatus Draft|Published [-vdConnection &lt;connection&gt;] [-vdExpectTenant &lt;label&gt;]</c>:
    /// sets the publication state of one experience of the world. Only its author and the world's Owner may; anyone else gets
    /// <see cref="ExitNotAllowed"/>.</description></item>
    /// </list>
    /// <para>
    /// <b>Exit codes.</b> <see cref="ExitNotAllowed"/> (the login may not do it in that world), <see cref="ExitNotOfferedByTenant"/> (the tenant's API
    /// has no such route: the SDK cannot do it there yet) and <see cref="ExitNotPublishedInWorld"/> (the scene's environment is not published in that
    /// world, or the experience does not exist there), <see cref="ExitWorldHasNoCapacity"/> (<c>create-core</c>: the world has no session capacity
    /// set). Every other answer the platform gives that is not one of those is <see cref="ExitPlatformUnreachable"/>, and the report's <c>status</c> and
    /// <c>message</c> of the target say which. A session that cannot be renewed silently is <see cref="ExitLoginRequired"/>. The version rule of
    /// <c>preview</c> and <c>deploy</c> does not apply: these routes are the server's, whatever version the project was set up from.
    /// </para>
    /// <para>
    /// <b>Several targets.</b> The commands try every scene in every world and report each one in <c>targets</c>. The exit code is the one of the
    /// most telling outcome, in this order: 11 login required (the later targets are not tried), 20 platform error, 34 the tenant does not
    /// offer it, 33 not allowed, 36 no session capacity, 35 not published there or not found, then 0. A script that needs more than "all went
    /// through" reads the <c>targets</c>.
    /// </para>
    /// </remarks>
    public static partial class EnvironmentsCli
    {
        /// <summary>
        /// <c>experiences</c>, <c>create-core</c> or <c>experience-status</c>: the login may not do it in that world (it lacks the permission to manage
        /// environments or Core Experiences there, or, for the state, is neither the experience's author nor the world's Owner). Nothing was changed.
        /// </summary>
        public const int ExitNotAllowed = 33;

        /// <summary>
        /// <c>experiences</c>, <c>create-core</c> or <c>experience-status</c>: the tenant's API has no such route (a 404 or 405 with no reason), so the SDK cannot
        /// show or change the experiences there yet; the Back Office can. Nothing was changed.
        /// </summary>
        public const int ExitNotOfferedByTenant = 34;

        /// <summary>The scene's environment is not published in that world, or the experience does not exist there (or the login cannot read it).</summary>
        public const int ExitNotPublishedInWorld = 35;

        /// <summary><c>create-core</c>: the world has no session capacity set, so a Core experience cannot be created there. Its Owner sets it in the Back Office.</summary>
        public const int ExitWorldHasNoCapacity = 36;

        private const int max_label_length = 100;

        // The most telling first: a run that could not finish says so before anything else; a tenant-wide answer before a per-world one.
        private static readonly int[] experience_exit_precedence =
        {
            ExitLoginRequired, ExitPlatformUnreachable, ExitNotOfferedByTenant, ExitNotAllowed, ExitWorldHasNoCapacity, ExitNotPublishedInWorld,
        };

        private sealed partial class Options
        {
            public string Label;
            public bool Published;
            public int? Experience;
            public ExperiencePublicationStatus? ExperienceStatus;
        }

        private static string DescribeExperienceExit(int code) => code switch
        {
            ExitNotAllowed => "not allowed: the login may not do it in that world",
            ExitNotOfferedByTenant => "not offered by the tenant: its API cannot show or change the experiences of an environment yet",
            ExitNotPublishedInWorld => "not published in the world, or the experience was not found",
            ExitWorldHasNoCapacity => "the world has no session capacity set",
            _ => "unknown",
        };

        #region Arguments

        /// <summary>
        /// The options only these commands read. <c>-vdWorlds</c> is already in <paramref name="options"/>, and <c>-vdScenes</c> stays a string that
        /// <see cref="SelectScenes"/> reads. Nothing happens for another command.
        /// </summary>
        private static bool TryReadExperienceOptions(Dictionary<string, string> values, HashSet<string> flags, Options options, out string error)
        {
            error = null;

            if (command != "experiences" && command != "create-core" && command != "experience-status")
                return true;

            options.Published = flags.Contains("-vdPublished");

            if (options.Worlds == null)
            {
                error = $"{command} needs -vdWorlds <ids>: the target is always explicit.";
                return false;
            }

            if (command == "experience-status")
            {
                if (options.Worlds.Count != 1)
                {
                    error = "experience-status takes exactly one world: -vdWorlds <id>.";
                    return false;
                }

                if (!values.TryGetValue("-vdExperience", out string experience))
                {
                    error = "experience-status needs -vdExperience <id>.";
                    return false;
                }

                if (!(int.TryParse(experience, out int experienceId) && experienceId > 0))
                {
                    error = $"-vdExperience takes an experience id: '{experience}' is not one.";
                    return false;
                }

                options.Experience = experienceId;

                if (!values.TryGetValue("-vdStatus", out string status))
                {
                    error = "experience-status needs -vdStatus Draft|Published.";
                    return false;
                }

                string name = Enum.GetNames(typeof(ExperiencePublicationStatus)).FirstOrDefault(n => string.Equals(n, status, StringComparison.OrdinalIgnoreCase));
                if (name == null)
                {
                    error = $"-vdStatus is Draft or Published: '{status}' is neither.";
                    return false;
                }

                options.ExperienceStatus = (ExperiencePublicationStatus)Enum.Parse(typeof(ExperiencePublicationStatus), name);
                return true;
            }

            if (command == "create-core")
            {
                if (string.IsNullOrEmpty(options.Scenes))
                {
                    error = "create-core needs -vdScenes <names>: it never creates for every scene by default.";
                    return false;
                }

                if (values.TryGetValue("-vdLabel", out string label))
                {
                    options.Label = label.Trim();
                    if (options.Label.Length == 0 || options.Label.Length > max_label_length)
                    {
                        error = $"-vdLabel takes 1 to {max_label_length} characters.";
                        return false;
                    }

                    int sceneCount = options.Scenes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    if (sceneCount != 1)
                    {
                        error = "-vdLabel names the one experience that is created, so it needs exactly one scene in -vdScenes.";
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Commands

        /// <summary>The checks a command that calls the platform makes before it sends anything, as <c>preview</c> makes them, and the session.</summary>
        private static async Task<int> RequireExperienceTargetAsync(Options options)
        {
            int code = UseNamedConnection(options.Connection);
            if (code == ExitDone)
                code = RequireConnection();
            if (code == ExitDone)
                code = await RequireCreatorKitAppAsync();
            if (code != ExitDone)
                return code;

            code = ConfirmTarget(options.Connection, options.ExpectTenant, required: false);
            if (code != ExitDone)
                return code;

            // No version rule here: the routes live on the server, so the project's version does not matter, and a tenant without
            // them is told by its 404/405 (ExitNotOfferedByTenant) with a message that names no version.
            return await RequireSessionAsync();
        }

        /// <summary><c>experiences</c> (<paramref name="create"/> false) and <c>create-core</c>: one target per scene and world.</summary>
        private static async Task<int> SceneExperiencesAsync(Options options, bool create)
        {
            int code = SelectScenes(options, out List<SceneConfiguration> scenes);
            if (code != ExitDone)
                return code;

            code = await RequireExperienceTargetAsync(options);
            if (code != ExitDone)
                return code;

            if (create)
            {
                report["dryRun"] = options.DryRun;
                report["requested"] = new Dictionary<string, object> { { "label", options.Label }, { "published", options.Published } };
            }

            List<Dictionary<string, object>> targets = new();
            List<(EnvironmentExperiencesStatus Status, string Message)> outcomes = new();
            bool loginLost = false;

            foreach (int worldId in options.Worlds)
            {
                // One list of the world's environments serves every scene.
                EnvironmentExperiencesResult<List<WorldEnvironment>> listing = loginLost
                    ? new EnvironmentExperiencesResult<List<WorldEnvironment>>
                    {
                        Status = EnvironmentExperiencesStatus.LoginRequired,
                        Message = EnvironmentExperiencesService.LoginRequiredMessage + " The later targets were not tried.",
                        WorldId = worldId,
                    }
                    : await EnvironmentExperiencesService.ListEnvironmentsAsync(targetKey, worldId);

                foreach (SceneConfiguration scene in scenes)
                {
                    Dictionary<string, object> row = new()
                    {
                        { "scene", scene.Scene.name },
                        { "addressName", scene.SceneNameFiltered },
                        { "worldId", worldId },
                    };

                    EnvironmentExperiencesStatus status;
                    string message;

                    if (!listing.Ok)
                    {
                        status = listing.Status;
                        message = listing.Message;
                        row["environmentId"] = null;
                        AddCallDetails(row, listing);
                    }
                    else
                    {
                        EnvironmentExperiencesResult<WorldEnvironment> resolved = EnvironmentExperiencesService.ResolveEnvironment(listing.Data, worldId, scene.SceneNameFiltered);
                        if (!resolved.Ok)
                        {
                            status = resolved.Status;
                            message = resolved.Message;
                            row["environmentId"] = null;
                            row["environmentPublished"] = false;
                        }
                        else
                        {
                            int environmentId = resolved.Data.Id;
                            row["environmentId"] = environmentId;
                            row["environmentPublished"] = true;
                            (status, message) = create
                                ? await CreateCoreTargetAsync(options, row, worldId, environmentId)
                                : await ReadTargetAsync(row, worldId, environmentId);
                        }
                    }

                    row["status"] = status.ToString();
                    row["message"] = message;
                    targets.Add(row);
                    outcomes.Add((status, message));
                    loginLost |= status == EnvironmentExperiencesStatus.LoginRequired;

                    string line = $"{log_prefix}{scene.Scene.name} in world {worldId}: {status}. {message}";
                    if (status == EnvironmentExperiencesStatus.Ok)
                        Debug.Log(line);
                    else
                        Debug.LogError(line);
                }
            }

            return FinishExperienceTargets(targets, outcomes);
        }

        private static async Task<(EnvironmentExperiencesStatus, string)> ReadTargetAsync(Dictionary<string, object> row, int worldId, int environmentId)
        {
            EnvironmentExperiencesResult<EnvironmentExperiences> result = await EnvironmentExperiencesService.GetExperiencesAsync(targetKey, worldId, environmentId);
            AddCallDetails(row, result);
            if (!result.Ok)
                return (result.Status, result.Message);

            AddExperienceData(row, result.Data);
            ExperienceCounts c = result.Data.Counts;
            return (result.Status, $"{c.Total} experience(s) use this environment in world {worldId}: Core {c.CorePublished} published and {c.CoreDraft} draft, " +
                                   $"authored {c.AuthoredPublished} published and {c.AuthoredDraft} draft, external app {c.ExternalApp}.");
        }

        private static async Task<(EnvironmentExperiencesStatus, string)> CreateCoreTargetAsync(Options options, Dictionary<string, object> row, int worldId, int environmentId)
        {
            if (options.DryRun)
            {
                // Reads only, and answers as the real run would: the permission to create is the one the platform reports.
                EnvironmentExperiencesResult<EnvironmentExperiences> read = await EnvironmentExperiencesService.GetExperiencesAsync(targetKey, worldId, environmentId);
                AddCallDetails(row, read);
                row["created"] = false;
                if (!read.Ok)
                    return (read.Status, read.Message);

                AddExperienceData(row, read.Data);
                if (!read.Data.CanCreateCore)
                {
                    row["wouldCreate"] = false;
                    return (EnvironmentExperiencesStatus.NotAllowed, EnvironmentExperiencesService.NotAllowedCreateMessage);
                }

                bool wouldCreate = read.Data.HasNoCore;
                row["wouldCreate"] = wouldCreate;
                row["alreadyExisted"] = !wouldCreate;
                return (EnvironmentExperiencesStatus.Ok, wouldCreate
                    ? $"Dry run: it would create a Core experience in world {worldId}. Nothing was changed."
                    : $"Dry run: a Core experience already exists in world {worldId}, so nothing would be created.");
            }

            EnvironmentExperiencesResult<CoreExperienceOutcome> result = await EnvironmentExperiencesService.EnsureCoreAsync(targetKey, worldId, environmentId, options.Label, options.Published);
            AddCallDetails(row, result);
            row["created"] = result.Ok && result.Data.Created;
            if (!result.Ok)
                return (result.Status, result.Message);

            AddExperienceData(row, result.Data);
            row["alreadyExisted"] = !result.Data.Created;
            row["createdId"] = result.Data.CreatedId;
            return (result.Status, result.Message);
        }

        private static async Task<int> ExperienceStatusAsync(Options options)
        {
            int code = await RequireExperienceTargetAsync(options);
            if (code != ExitDone)
                return code;

            int worldId = options.Worlds[0];
            ExperiencePublicationStatus wanted = options.ExperienceStatus.Value;
            EnvironmentExperiencesResult<ExperienceStatusOutcome> result = await EnvironmentExperiencesService.SetStatusAsync(targetKey, worldId, options.Experience.Value, wanted);

            Dictionary<string, object> row = new()
            {
                { "worldId", worldId },
                { "experienceId", options.Experience.Value },
                { "requestedStatus", wanted.ToString() },
                { "status", result.Status.ToString() },
                { "message", result.Message },
                { "currentStatus", result.Data?.Status },
                { "changed", result.Data?.Changed },
            };
            AddCallDetails(row, result);

            report["experienceStatus"] = row;

            if (result.Ok)
            {
                Debug.Log($"{log_prefix}{result.Message}");
                return ExitDone;
            }

            Debug.LogError($"{log_prefix}{result.Status}. {result.Message}");
            report["error"] = result.Message;
            return ExperienceExitOf(result.Status);
        }

        private static int FinishExperienceTargets(List<Dictionary<string, object>> targets, List<(EnvironmentExperiencesStatus Status, string Message)> outcomes)
        {
            report["targets"] = targets;
            report["summary"] = outcomes.GroupBy(o => o.Status).ToDictionary(g => g.Key.ToString(), g => g.Count());

            int code = WorstExperienceExit(outcomes.Select(o => ExperienceExitOf(o.Status)));
            if (code != ExitDone)
                report["error"] = outcomes.First(o => ExperienceExitOf(o.Status) == code).Message;

            return code;
        }

        private static void AddCallDetails<T>(Dictionary<string, object> row, EnvironmentExperiencesResult<T> result) where T : class
        {
            row["httpStatus"] = result.HttpStatus == 0 ? null : (int?)result.HttpStatus;
            row["serverCode"] = result.ServerCode;
        }

        /// <summary>What the answer says about the experiences, in the contract's names plus <c>total</c> (every experience that uses the environment there).</summary>
        private static void AddExperienceData(Dictionary<string, object> row, EnvironmentExperiences data)
        {
            ExperienceCounts c = data.Counts ?? new ExperienceCounts();
            row["counts"] = new Dictionary<string, object>
            {
                { "corePublished", c.CorePublished },
                { "coreDraft", c.CoreDraft },
                { "authoredPublished", c.AuthoredPublished },
                { "authoredDraft", c.AuthoredDraft },
                { "externalApp", c.ExternalApp },
                { "total", c.Total },
            };
            row["coreExperiences"] = data.CoreExperiences;
            row["coreHiddenDrafts"] = data.CoreHiddenDrafts;
            row["canCreateCore"] = data.CanCreateCore;
        }

        private static int ExperienceExitOf(EnvironmentExperiencesStatus status) => status switch
        {
            EnvironmentExperiencesStatus.Ok => ExitDone,
            EnvironmentExperiencesStatus.NotAllowed => ExitNotAllowed,
            EnvironmentExperiencesStatus.NotSupportedByTenant => ExitNotOfferedByTenant,
            EnvironmentExperiencesStatus.NotPublishedInWorld => ExitNotPublishedInWorld,
            EnvironmentExperiencesStatus.ExperienceNotFound => ExitNotPublishedInWorld,
            EnvironmentExperiencesStatus.LoginRequired => ExitLoginRequired,
            EnvironmentExperiencesStatus.WorldHasNoCapacity => ExitWorldHasNoCapacity,
            // Unreachable and any other answer: the platform could not do it.
            _ => ExitPlatformUnreachable,
        };

        private static int WorstExperienceExit(IEnumerable<int> codes)
        {
            HashSet<int> seen = new(codes);
            foreach (int code in experience_exit_precedence)
                if (seen.Contains(code))
                    return code;

            return ExitDone;
        }

        #endregion
    }
}
