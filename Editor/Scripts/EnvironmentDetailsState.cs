#if !VIRTUADEMY_APPLICATION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Virtuademy.SDK.Environments.Editor
{
    // What the Environment Details window shows, as plain objects. The window draws only from these: a connection's state is set whole by
    // EnvironmentDetailsWindow.SetConnectionState, whether it comes from the platform (EnvironmentDetailsWindow.Load) or, in a harness, from
    // a test that wants to look at one state without a server.

    /// <summary>How reading a connection stands.</summary>
    internal enum ConnectionLoad
    {
        /// <summary>No session, or it ended: the list says "Log in to see where this scene is published here."</summary>
        LoggedOut,

        /// <summary>The places are being read: skeleton rows.</summary>
        Loading,

        /// <summary><see cref="DetailsConnection.Listing"/> is read.</summary>
        Loaded,

        /// <summary>The platform could not be read: <see cref="DetailsConnection.Error"/> says why, with Retry.</summary>
        Failed,
    }

    /// <summary>The colour of a connection's dot, as Publish Environments gives it.</summary>
    internal enum ConnectionDot
    {
        /// <summary>Not logged in.</summary>
        Grey,

        /// <summary>A login or a renewal is running.</summary>
        Blue,

        /// <summary>Logged in, token valid.</summary>
        Green,

        /// <summary>Logged in, token expired (the next operation renews it).</summary>
        Amber,
    }

    /// <summary>The experiences of the scene's environment in one world, as the service answered, or that it is being asked.</summary>
    internal sealed class WorldExperiences
    {
        /// <summary>The first read is running.</summary>
        public bool Loading;

        /// <summary>How the read ended; meaningful when <see cref="Loading"/> is false.</summary>
        public EnvironmentExperiencesStatus Status;

        /// <summary>The service's English message: tooltips and details only, never in place of the window's own text.</summary>
        public string Message;

        /// <summary>The experiences, when <see cref="Status"/> is <see cref="EnvironmentExperiencesStatus.Ok"/>.</summary>
        public EnvironmentExperiences Data;

        /// <summary>Experience ids whose state is being set: the row shows a spinner.</summary>
        public readonly HashSet<int> Changing = new();

        /// <summary>Experience id to the message of the last state change that failed: the row shows it in a tooltip, the popup is back to what it was.</summary>
        public readonly Dictionary<int, string> StateErrors = new();

        /// <summary>A Core experience is being created here.</summary>
        public bool Creating;

        /// <summary>The last attempt to create the Core experience here that did not work: its status and the service's message.</summary>
        public EnvironmentExperiencesStatus? CreateStatus;

        public string CreateMessage;

        public bool Known => !Loading && Data != null && Status == EnvironmentExperiencesStatus.Ok;
    }

    /// <summary>One connection of the computer, as the left list and the place pane show it.</summary>
    internal sealed class DetailsConnection
    {
        public string Key;

        /// <summary>The tenant's label (bold in the list).</summary>
        public string Label;

        /// <summary>The environment chip's text ("Sandbox", "Preproduction"); null for Production, which has none.</summary>
        public string Environment;

        public string PlatformVersion;

        /// <summary>The muted account line under the connection ("Riccardo Verza · Tenant manager · until 09:32", "Not logged in.").</summary>
        public string Account;

        public ConnectionDot Dot;
        public string DotTooltip;
        public ConnectionLoad Load;

        /// <summary>Why <see cref="ConnectionLoad.Failed"/>, in the platform's words.</summary>
        public string Error;

        /// <summary>A login started from here is waiting for the browser.</summary>
        public bool LoginPending;

        public string LoginStatus;

        /// <summary>The address of the login waiting in the browser, to copy.</summary>
        public string LoginAuthUrl;

        /// <summary>Why the last login started from here failed.</summary>
        public string LoginError;

        /// <summary>Where the scene is published on this connection, with the files; set when <see cref="Load"/> is <see cref="ConnectionLoad.Loaded"/>.</summary>
        public FilesListing Listing;

        /// <summary>World id to its experiences.</summary>
        public readonly Dictionary<int, WorldExperiences> Experiences = new();

        /// <summary>The tenant's Back Office address and what the account may open there; null for no Back Office link.</summary>
        public Uri BackofficeHome;

        public BackofficeAccess Backoffice;

        /// <summary>The tenant's languages, read when a localization file is checked; null until then.</summary>
        public List<string> TenantLanguages;

        public bool TenantLanguagesRead;

        internal CancellationTokenSource Cancellation;
        internal CancellationTokenSource LoginCancellation;

        public IEnumerable<FilesTarget> Targets => Listing?.Scenes.SelectMany(s => s.Targets) ?? Enumerable.Empty<FilesTarget>();

        public WriteSupport Support => Listing?.Support ?? WriteSupport.Unknown;

        public FilesTarget TargetAt(int? worldId) => Targets.FirstOrDefault(t => t.WorldId == worldId);

        public bool HasBackoffice => BackofficeHome != null && Backoffice != null;

        /// <summary>The Back Office's link for a place: its world's page when the account's access covers the world, the home page otherwise.</summary>
        public string BackofficeLinkFor(int? worldId) =>
            BackofficeHome == null ? null : EnvironmentBackoffice.LinkTo(BackofficeHome, worldId != null && Backoffice != null && Backoffice.Covers(worldId.Value) ? worldId : null);
    }

    /// <summary>A place of the scene on a connection: what the list selects and the pane describes.</summary>
    internal readonly struct PlaceKey : IEquatable<PlaceKey>
    {
        public PlaceKey(string connection, int? worldId)
        {
            Connection = connection;
            WorldId = worldId;
        }

        public string Connection { get; }

        /// <summary>The world; null for the tenant level.</summary>
        public int? WorldId { get; }

        public bool Equals(PlaceKey other) => string.Equals(Connection, other.Connection, StringComparison.OrdinalIgnoreCase) && WorldId == other.WorldId;

        public override bool Equals(object obj) => obj is PlaceKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Connection?.ToLowerInvariant(), WorldId);
    }

    /// <summary>The file picked to replace one place's thumbnail or localization file, between the pick and the end of the replace.</summary>
    internal sealed class PlaceReplaceFlow
    {
        public PlaceKey Place;
        public FileKind Kind;

        /// <summary>The project asset picked.</summary>
        public string Asset;

        /// <summary>Why the pick was refused before any check ("Use a PNG or JPEG image."); null when it was taken.</summary>
        public string Problem;

        /// <summary>The file as the SDK would send it, checked locally; null while the tenant's languages are read.</summary>
        public PreparedFile Prepared;

        /// <summary>True while the tenant's languages are read (a localization file is checked against them).</summary>
        public bool Checking;

        /// <summary>A replace is running.</summary>
        public bool Busy;

        /// <summary>The outcome of the last attempt: set when it ended.</summary>
        public FileOutcome Outcome;
    }

    /// <summary>How one place's row of the replace-in-all-places preview stands.</summary>
    internal enum ReplaceRowStatus
    {
        WillChange,
        WillChangeShared,
        Unchanged,
        SkippedNotAllowed,
        SkippedUnsupported,
    }

    /// <summary>What happened to one row when the replace was applied.</summary>
    internal enum ReplaceRowResult
    {
        /// <summary>Not applied (yet).</summary>
        None,

        Pending,
        Updated,
        Unchanged,
        Failed,
        Skipped,
    }

    internal sealed class ReplaceRow
    {
        public DetailsConnection Connection;
        public FilesTarget Target;
        public ReplaceRowStatus Status;

        /// <summary>The row can be ticked: it will change.</summary>
        public bool CanChange => Status == ReplaceRowStatus.WillChange || Status == ReplaceRowStatus.WillChangeShared;

        public bool Ticked;
        public ReplaceRowResult Result;
        public string ResultMessage;
    }

    /// <summary>Replace in all places: the two assets, what the local checks said, the preview table and, once applied, its results.</summary>
    internal sealed class ReplaceAllFlow
    {
        public UnityEngine.Texture2D Thumbnail;
        public UnityEngine.TextAsset Localization;

        public PreparedFile ThumbnailFile;
        public PreparedFile LocalizationFile;

        /// <summary>The tenants' languages are being read to check the localization file.</summary>
        public bool Checking;

        /// <summary>The preview table is being built (the files the places hold are read for it).</summary>
        public bool Previewing;

        /// <summary>The preview table; null while none is shown.</summary>
        public List<ReplaceRow> Rows;

        public bool Applying;

        /// <summary>The rows were applied: the table shows results.</summary>
        public bool Applied;

        public bool Valid => (ThumbnailFile != null || LocalizationFile != null)
                             && (ThumbnailFile == null || ThumbnailFile.Check.Valid)
                             && (LocalizationFile == null || LocalizationFile.Check.Valid);
    }
}
#endif
