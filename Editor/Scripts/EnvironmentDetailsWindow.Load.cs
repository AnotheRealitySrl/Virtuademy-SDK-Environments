#if !VIRTUADEMY_APPLICATION
using Virtuademy.SDK.TenantConfiguration;
using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    // Reading the platform: one pass per connection, each in its own cancellation, each filling its DetailsConnection when it is done. No call here
    // blocks the main thread; a result that comes back after the connection was dropped (a new scene, Refresh, the window closed) is ignored.
    internal sealed partial class EnvironmentDetailsWindow
    {
        private const int max_parallel_experiences = 4;

        // Files being downloaded for a preview, so that a redraw does not start the same download twice.
        private readonly HashSet<FileStatus> caching = new();

        #region Loading

        /// <summary>Drops what is shown and reads every connection of the computer again, for the current scene. Each fills in on its own.</summary>
        private void LoadAll()
        {
            CancelAll();
            ClearPreviews();
            contents.Clear();
            caching.Clear();
            cards.Clear();
            ResetFlows();

            // A login waiting in the browser outlives a Refresh or a new scene: the connection's new state takes it over.
            List<DetailsConnection> logins = connections.Where(c => c.LoginPending).ToList();
            connections.Clear();
            connectionsSignature = Signature();

            if (DisableLoading || scene == null)
            {
                Render();
                return;
            }

            foreach (ConnectionRecord record in ProjectConnections.Usable())
            {
                DetailsConnection added = HeaderOf(record);
                DetailsConnection waiting = logins.FirstOrDefault(c => string.Equals(c.Key, record.Key, StringComparison.OrdinalIgnoreCase));
                if (waiting != null)
                {
                    added.LoginPending = true;
                    added.LoginStatus = waiting.LoginStatus;
                    added.LoginAuthUrl = waiting.LoginAuthUrl;
                    added.LoginCancellation = waiting.LoginCancellation;
                    ApplyHeader(added, record);
                }

                connections.Add(added);
            }

            Render();
            foreach (DetailsConnection connection in connections.ToList())
                _ = LoadConnectionAsync(connection);
        }

        private void CancelAll()
        {
            foreach (DetailsConnection connection in connections)
            {
                connection.Cancellation?.Cancel();
            }

            flowCancellation?.Cancel();
            flowCancellation = null;
        }

        /// <summary>Cancels the logins waiting in the browser too: for the window closing. A Refresh or a new scene leaves them running.</summary>
        private void CancelLogins()
        {
            foreach (DetailsConnection connection in connections)
                connection.LoginCancellation?.Cancel();
        }

        private void ResetFlows()
        {
            ReleaseNewPreviews();
            placeFlow = null;
            allFlow = new ReplaceAllFlow();
            saving.Clear();
            saveProblems.Clear();
            changingExperiences.Clear();
            savingAll = false;
            saveAllMessage = null;
            saveAllFailed = false;
            creatingAll = false;
            createAllMessages.Clear();
            previewNotes.Clear();
        }

        private static string Signature() =>
            string.Join("|", ProjectConnections.Usable().Select(r => $"{r.Key}#{r.HasSession}"));

        /// <summary>A login or a logout on any connection, or a connection added or removed: the connections are read again.</summary>
        private void OnLoginStateChanged()
        {
            if (root == null || DisableLoading || connections.Any(c => c.LoginPending) || Busy)
                return;

            if (Signature() != connectionsSignature)
                LoadAll();
            else
                ScheduleRender();
        }

        private static DetailsConnection HeaderOf(ConnectionRecord record)
        {
            DetailsConnection connection = new() { Key = record.Key };
            ApplyHeader(connection, record);
            return connection;
        }

        /// <summary>The connection's header as Publish Environments gives it: tenant, environment, platform version, account line and the colour of the dot.</summary>
        private static void ApplyHeader(DetailsConnection connection, ConnectionRecord record)
        {
            if (record == null)
                return;

            connection.Label = record.TenantLabel;
            connection.Environment = AddressablesManagementWindow.IsProduction(record)
                ? null
                : string.Equals(record.Environment, nameof(Env.PreProduction), StringComparison.OrdinalIgnoreCase) ? "Preproduction" : record.Environment ?? "?";
            connection.PlatformVersion = record.PlatformVersion ?? "no version";
            connection.BackofficeHome = EnvironmentBackoffice.UriOf(record.Key);

            if (connection.LoginPending)
            {
                connection.Dot = ConnectionDot.Blue;
                connection.DotTooltip = "Logging in";
                connection.Account = connection.LoginAuthUrl != null ? "Waiting for the browser…" : connection.LoginStatus ?? "Logging in…";
                return;
            }

            if (!record.HasSession)
            {
                connection.Dot = ConnectionDot.Grey;
                connection.DotTooltip = "Not logged in";
                connection.Account = "Not logged in.";
                return;
            }

            string user = string.IsNullOrEmpty(record.Username) ? "Logged in" : record.Username;
            string role = record.IsTenantManager ? " · Tenant manager" : string.Empty;
            connection.Dot = record.IsTokenValid ? ConnectionDot.Green : ConnectionDot.Amber;
            connection.DotTooltip = record.IsTokenValid ? "Logged in" : "Session expired: it could not be renewed now, the next operation renews it";
            connection.Account = record.IsTokenValid
                ? $"{user}{role} · until {AddressablesManagementWindow.ShortTime(record.UsableUntilUtc)}"
                : $"{user}{role} · session expired, renewed on the next operation";
        }

        private bool IsCurrent(DetailsConnection connection) => connections.Contains(connection);

        /// <summary>
        /// Reads one connection: its platform version again (silently: an upgraded tenant changes what can be written), where the scene is published
        /// there with the files, then, beside each other, the account's Back Office access and the experiences of every world.
        /// </summary>
        private async Task LoadConnectionAsync(DetailsConnection connection)
        {
            connection.Cancellation?.Cancel();
            connection.Cancellation = new CancellationTokenSource();
            CancellationToken ct = connection.Cancellation.Token;

            connection.Listing = null;
            connection.Error = null;
            connection.LoginError = null;
            connection.Experiences.Clear();
            connection.Backoffice = null;

            ConnectionRecord record = ConnectionStore.Get(connection.Key);
            ApplyHeader(connection, record);
            if (record?.HasSession != true)
            {
                connection.Load = ConnectionLoad.LoggedOut;
                ScheduleRender();
                return;
            }

            connection.Load = ConnectionLoad.Loading;
            ScheduleRender();

            try
            {
                await ConnectionLoginService.RefreshConnectionAsync(connection.Key);
                ct.ThrowIfCancellationRequested();
                ApplyHeader(connection, ConnectionStore.Get(connection.Key));

                FilesListing listing = await EnvironmentFilesService.ListAsync(connection.Key, new[] { scene }, true, null, ct);
                ct.ThrowIfCancellationRequested();
                if (!IsCurrent(connection))
                    return;

                switch (listing.Status)
                {
                    case ListingStatus.Ok:
                        connection.Listing = listing;
                        connection.Load = ConnectionLoad.Loaded;
                        break;

                    case ListingStatus.SessionRequired:
                        connection.Load = ConnectionLoad.LoggedOut;
                        break;

                    default:
                        connection.Load = ConnectionLoad.Failed;
                        connection.Error = listing.Message;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested || !IsCurrent(connection))
                    return;

                connection.Load = ConnectionLoad.Failed;
                connection.Error = ex.Message;
            }

            ScheduleRender();
            if (connection.Load != ConnectionLoad.Loaded)
                return;

            _ = LoadBackofficeAsync(connection, ct);
            await LoadExperiencesAsync(connection, ct);
        }

        private async Task LoadBackofficeAsync(DetailsConnection connection, CancellationToken ct)
        {
            try
            {
                BackofficeAccess access = await EnvironmentBackoffice.LoadAccessAsync(connection.Key);
                if (ct.IsCancellationRequested || !IsCurrent(connection))
                    return;

                connection.Backoffice = access;
                ScheduleRender();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EnvironmentDetails] Could not read the Back Office access of {PublishedEnvironmentsIndex.LabelOf(connection.Key)}: {ex.Message}");
            }
        }

        /// <summary>The experiences of the scene's environment in every world it is published in, a few requests at a time.</summary>
        private async Task LoadExperiencesAsync(DetailsConnection connection, CancellationToken ct)
        {
            List<FilesTarget> worlds = connection.Targets.Where(t => !t.IsTenant).ToList();
            foreach (FilesTarget target in worlds)
                connection.Experiences[target.WorldId.Value] = new WorldExperiences { Loading = true };

            ScheduleRender();
            using SemaphoreSlim slots = new(max_parallel_experiences);
            await Task.WhenAll(worlds.Select(async target =>
            {
                try
                {
                    await slots.WaitAsync(ct);
                    try
                    {
                        await ReadExperiencesAsync(connection, target, ct);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // The window moved on: nothing to show for this world.
                }
            }));
        }

        private async Task ReadExperiencesAsync(DetailsConnection connection, FilesTarget target, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EnvironmentExperiencesResult<EnvironmentExperiences> result;
            try
            {
                result = await EnvironmentExperiencesService.GetExperiencesAsync(connection.Key, target.WorldId.Value, target.EnvId);
            }
            catch (Exception ex)
            {
                result = new EnvironmentExperiencesResult<EnvironmentExperiences> { Status = EnvironmentExperiencesStatus.Failed, Message = ex.Message, WorldId = target.WorldId.Value };
            }

            if (ct.IsCancellationRequested || !IsCurrent(connection))
                return;

            connection.Experiences[target.WorldId.Value] = new WorldExperiences { Status = result.Status, Message = result.Message, Data = result.Ok ? result.Data : null };
            ScheduleRender();
        }

        /// <summary>Reads one world's experiences again (the pane's Refresh, Retry, and after a change that moved the world's state).</summary>
        private async Task ReloadExperiencesAsync(DetailsConnection connection, int worldId)
        {
            FilesTarget target = connection.TargetAt(worldId);
            if (target == null || DisableLoading)
                return;

            connection.Experiences[worldId] = new WorldExperiences { Loading = true };
            ScheduleRender();
            await ReadExperiencesAsync(connection, target, connection.Cancellation?.Token ?? CancellationToken.None);
        }

        /// <summary>Downloads a file a place holds into the cache folder, if it is not there, so the pane can show it; redraws when it is.</summary>
        private void RequestCache(DetailsConnection connection, FileStatus status)
        {
            if (DisableLoading || status == null || !status.Present || status.CachePath != null || status.Error != null || !caching.Add(status))
                return;

            _ = CacheAsync(connection, status);
        }

        private async Task CacheAsync(DetailsConnection connection, FileStatus status)
        {
            CancellationToken ct = connection.Cancellation?.Token ?? CancellationToken.None;
            try
            {
                await EnvironmentFilesService.EnsureCachedAsync(status, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                status.Error = ex.Message;
            }
            finally
            {
                caching.Remove(status);
            }

            if (!ct.IsCancellationRequested && IsCurrent(connection))
                ScheduleRender();
        }

        /// <summary>The tenant's languages, read once for a connection: what a localization file is checked against. Null when they could not be read (the file is then checked against the platform's rules only).</summary>
        private async Task<List<string>> LanguagesOfAsync(DetailsConnection connection)
        {
            if (connection.TenantLanguagesRead || DisableLoading)
                return connection.TenantLanguages;

            (List<string> languages, string error) = await EnvironmentFilesService.GetTenantLanguagesAsync(connection.Key);
            connection.TenantLanguages = error == null ? languages : null;
            connection.TenantLanguagesRead = true;
            return connection.TenantLanguages;
        }

        #endregion

        #region Login

        /// <summary>The browser login of a connection, as Publish Environments does it; when it succeeds the connection is read again.</summary>
        private async void LoginConnection(DetailsConnection connection)
        {
            if (connection.LoginPending)
                return;

            connection.LoginPending = true;
            connection.LoginStatus = null;
            connection.LoginAuthUrl = null;
            connection.LoginError = null;
            connection.LoginCancellation = new CancellationTokenSource();
            CancellationToken token = connection.LoginCancellation.Token;
            ApplyHeader(connection, ConnectionStore.Get(connection.Key));
            Render();

            // A Refresh or a new scene makes new connection objects: the login reports to whichever one is current.
            DetailsConnection Current() => connections.FirstOrDefault(c => string.Equals(c.Key, connection.Key, StringComparison.OrdinalIgnoreCase)) ?? connection;

            LoginOutcome outcome = null;
            try
            {
                LoginOptions options = new()
                {
                    Cancellation = token,
                    OnStatus = status => EditorApplication.delayCall += () =>
                    {
                        DetailsConnection now = Current();
                        now.LoginStatus = status;
                        ApplyHeader(now, ConnectionStore.Get(now.Key));
                        ScheduleRender();
                    },
                };
                options.Browser.OnAuthUrl = url => EditorApplication.delayCall += () =>
                {
                    DetailsConnection now = Current();
                    now.LoginAuthUrl = url;
                    ApplyHeader(now, ConnectionStore.Get(now.Key));
                    ScheduleRender();
                };

                outcome = await ConnectionLoginService.LoginAsync(connection.Key, options);
                if (!outcome.Success && outcome.Code != "cancelled" && !token.IsCancellationRequested)
                    Current().LoginError = $"The login failed. {outcome.Message}";
            }
            catch (Exception ex)
            {
                Current().LoginError = $"The login failed. {ex.Message}";
            }

            DetailsConnection done = Current();
            done.LoginPending = false;
            done.LoginCancellation = null;
            connection.LoginPending = false;
            connection.LoginCancellation = null;
            ApplyHeader(done, ConnectionStore.Get(done.Key));
            connectionsSignature = Signature();
            if (IsCurrent(done) && outcome?.Success == true)
                await LoadConnectionAsync(done);
            else
                Render();
        }

        #endregion

        #region Places

        /// <summary>The places of a loaded connection in the order the list shows them: the tenant level first, then the worlds by name.</summary>
        private static IEnumerable<FilesTarget> OrderedPlaces(DetailsConnection connection) => connection.Targets
            .OrderBy(t => t.IsTenant ? 0 : 1)
            .ThenBy(t => t.WorldLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.WorldId);

        private IEnumerable<(DetailsConnection Connection, FilesTarget Target)> AllPlaces() => connections
            .Where(c => c.Load == ConnectionLoad.Loaded)
            .SelectMany(c => OrderedPlaces(c).Select(t => (c, t)));

        private int PlaceCount() => AllPlaces().Count();

        private bool TryGetSelected(out DetailsConnection connection, out FilesTarget target)
        {
            connection = null;
            target = null;
            if (allSelected || selected == null)
                return false;

            connection = connections.FirstOrDefault(c => string.Equals(c.Key, selected.Value.Connection, StringComparison.OrdinalIgnoreCase) && c.Load == ConnectionLoad.Loaded);
            target = connection?.TargetAt(selected.Value.WorldId);
            return target != null;
        }

        /// <summary>
        /// Keeps the selection on something that exists. With nothing selected, the first place of the list (a world rather than the tenant level
        /// when there is one: that is where the experiences are); with one place only, that place; All places only while there are several.
        /// </summary>
        private void EnsureSelection()
        {
            List<(DetailsConnection Connection, FilesTarget Target)> places = AllPlaces().ToList();
            if (places.Count <= 1)
                allSelected = false;

            if (allSelected)
                return;

            if (TryGetSelected(out _, out _))
                return;

            (DetailsConnection Connection, FilesTarget Target) first = places.FirstOrDefault(p => !p.Target.IsTenant);
            if (first.Target == null)
                first = places.FirstOrDefault();

            selected = first.Target == null ? null : new PlaceKey(first.Connection.Key, first.Target.WorldId);
        }

        private static string PlaceName(FilesTarget target) => target.IsTenant ? "Tenant level" : $"{target.WorldLabel} #{target.WorldId.Value.ToString(CultureInfo.InvariantCulture)}";

        private static string ConnectionName(DetailsConnection connection) =>
            string.IsNullOrEmpty(connection.Environment) ? connection.Label : $"{connection.Label} ({connection.Environment})";

        #endregion
    }
}
#endif
