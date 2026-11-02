using System;
using System.Collections.Generic;
using System.Linq;

namespace Virtuademy.SDK.Environments.Editor
{
    // The data the EnvironmentFilesService reads and writes. Plain classes with public fields, so a window can bind to them and the
    // command line can put them in its REPORT without a mapping layer. Nothing here talks to the network or to Unity.

    /// <summary>The two files of an environment the SDK reads and replaces. The thumbnail is the environment's hero image (the platform derives its small card from it).</summary>
    internal enum FileKind
    {
        Thumbnail,
        Localization,
    }

    /// <summary>Whether the tenant's platform can receive thumbnails and localization files from the SDK (<see cref="EnvironmentFilesService.SupportOf"/>).</summary>
    internal enum WriteSupport
    {
        /// <summary>The tenant's platform version is 2026.7.0 or later.</summary>
        Supported,

        /// <summary>It is earlier: nothing is sent, the Back Office is the way.</summary>
        NotSupported,

        /// <summary>The tenant reports no version, or one that is not a release number: a write is tried, and a 404 or 405 answer means "not supported".</summary>
        Unknown,
    }

    /// <summary>Where a file stands against the project.</summary>
    internal enum Presence
    {
        /// <summary>The environment has no such file online.</summary>
        None,

        /// <summary>It has one, and the project does not hold the same file (same SHA-256).</summary>
        OnlineOnly,

        /// <summary>It has one, and the project holds the same file.</summary>
        InProject,
    }

    /// <summary>A scene of the project (or, for a restore, of a manifest) in the terms the platform names an environment by.</summary>
    internal sealed class SceneRef
    {
        /// <summary>The scene's name.</summary>
        public string Name;

        /// <summary>The environment file name the platform records for it: <see cref="PublishedEnvironmentsIndex.Key"/> of the name.</summary>
        public string Key;

        public string Path;
        public bool IncludedInBuild;
    }

    internal readonly struct WorldRef
    {
        public WorldRef(int id, string label)
        {
            Id = id;
            Label = label;
        }

        public int Id { get; }
        public string Label { get; }
    }

    /// <summary>One of the two files at one place: what the platform holds, and whether the project holds the same file.</summary>
    internal sealed class FileStatus
    {
        public FileKind Kind;

        /// <summary>The platform's address of the file, absolute and anonymous; null when the environment has none.</summary>
        public string Uri;

        /// <summary>True when the environment has the file (false when it has no address, or the address answers 404).</summary>
        public bool Present;

        /// <summary>The SHA-256 of the file as the platform stores it (hex); null when it is absent or unknown.</summary>
        public string Sha256;

        /// <summary>True when the platform said the hash; false when the SDK downloaded the file and hashed it (a platform that does not say).</summary>
        public bool HashFromPlatform;

        /// <summary>The project asset with the same SHA-256, or null.</summary>
        public string ProjectAsset;

        /// <summary>The image's size and format when the SDK downloaded it; 0 and null otherwise.</summary>
        public int Width;

        public int Height;
        public string Format;

        /// <summary>Why the file could not be read, when it could not (a failure, not an absence).</summary>
        public string Error;

        /// <summary>Where the SDK keeps the downloaded copy it hashed (<c>Library/Virtuademy/EnvironmentDetails/</c>), or null.</summary>
        public string CachePath;

        public Presence Presence => !Present ? Presence.None : ProjectAsset != null ? Presence.InProject : Presence.OnlineOnly;
    }

    /// <summary>One environment: one scene published at one world, or at a tenant's tenant level.</summary>
    internal sealed class FilesTarget
    {
        public string ConnectionKey;
        public SceneRef Scene;

        /// <summary>The world; null for the tenant level.</summary>
        public int? WorldId;

        public string WorldLabel;
        public int EnvId;
        public string EnvLabel;
        public string Catalog;

        /// <summary>
        /// Whether the login may change this environment, as the platform says. Null when it does not say (a platform that predates
        /// the field): the write then decides.
        /// </summary>
        public bool? CanWrite;

        /// <summary>A tenant environment the account sees only through a world (it cannot read the tenant list): readable, never writable.</summary>
        public bool SeenThroughWorld;

        /// <summary>The small card derived from the hero image; not tracked, only downloaded by <c>save-files</c> when the hero is missing.</summary>
        public string CardUri;

        public FileStatus Thumbnail;
        public FileStatus Localization;

        public bool IsTenant => WorldId == null;

        public string Place => IsTenant ? "tenant level" : $"world {WorldId} ({WorldLabel})";

        public FileStatus StatusOf(FileKind kind) => kind == FileKind.Thumbnail ? Thumbnail : Localization;
    }

    /// <summary>One scene and the places it is published at.</summary>
    internal sealed class SceneFiles
    {
        public SceneRef Scene;
        public List<FilesTarget> Targets = new();
    }

    internal enum ListingStatus
    {
        Ok,

        /// <summary>The connection's tenant reported no Application API address.</summary>
        NoApiUrl,

        /// <summary>The session is gone or could not be renewed silently.</summary>
        SessionRequired,

        /// <summary>The platform answered with an error, or the request failed.</summary>
        Failed,
    }

    /// <summary>Where the scenes are published, with their files, on one connection.</summary>
    internal sealed class FilesListing
    {
        public string ConnectionKey;
        public ListingStatus Status;
        public string Message;

        /// <summary>The tenant's platform version as the connection last had it, and what the SDK makes of it.</summary>
        public string PlatformVersion;

        public WriteSupport Support;

        /// <summary>Every world the account can see.</summary>
        public List<WorldRef> Worlds = new();

        /// <summary>True when the account could not read the tenant level (it is not a TenantManager).</summary>
        public bool TenantListUnreadable;

        public List<SceneFiles> Scenes = new();

        public IEnumerable<FilesTarget> AllTargets => Scenes.SelectMany(s => s.Targets);
    }

    /// <summary>Which environments a command acts on: some scenes, at some places.</summary>
    internal sealed class PlaceSelection
    {
        /// <summary>The scenes' keys; null for every scene.</summary>
        public HashSet<string> SceneKeys;

        /// <summary>Every place the scene is published at, tenant level included.</summary>
        public bool AllPlaces;

        /// <summary>Every world (not the tenant level).</summary>
        public bool AllWorlds;

        public HashSet<int> Worlds = new();
        public HashSet<int> ExcludedWorlds = new();
        public bool Tenant;

        public bool Matches(FilesTarget target) => Matches(target.Scene.Key, target.WorldId);

        /// <summary>Whether a scene at a place (<paramref name="worldId"/> null is the tenant level) is selected.</summary>
        public bool Matches(string sceneKey, int? worldId)
        {
            if (SceneKeys != null && !SceneKeys.Contains(sceneKey))
                return false;

            if (worldId == null)
                return AllPlaces || Tenant;

            if (ExcludedWorlds.Contains(worldId.Value))
                return false;

            return AllPlaces || AllWorlds || Worlds.Contains(worldId.Value);
        }
    }

    /// <summary>A file ready to be sent: what goes over the wire, and what the check said about it.</summary>
    internal sealed class PreparedFile
    {
        public FileKind Kind;

        /// <summary>The project asset the caller named (or, for a restore, the file it read).</summary>
        public string SourceAsset;

        /// <summary>The bytes that are uploaded, read from disk (or, for a converted image, the new JPEG).</summary>
        public byte[] Bytes;

        /// <summary>The SHA-256 of <see cref="Bytes"/>.</summary>
        public string Sha256;

        /// <summary>For a localization file that starts with a UTF-8 BOM: the SHA-256 of the file without it, which is what the platform stores and hashes.</summary>
        public string StoredSha256;

        public string ContentType;

        /// <summary>The JPEG written to the publish folder (or that would be, in a dry run) when the source had to be converted; null when it is sent as it is.</summary>
        public string ConvertedAsset;

        /// <summary>True when the conversion only would be written: a dry run.</summary>
        public bool ConversionPending;

        public int Width;
        public int Height;
        public FileCheckResult Check;

        /// <summary>The asset that holds exactly what is uploaded.</summary>
        public string UploadAsset => ConvertedAsset ?? SourceAsset;

        public bool Converted => ConvertedAsset != null;

        /// <summary>The line that says an image is converted, when it is.</summary>
        public string ConversionNote =>
            Converted ? $"The image will be converted to {ConvertedAsset} ({Width} × {Height}): that file goes online." : null;
    }

    internal enum OutcomeKind
    {
        Updated,

        /// <summary>A dry run: the file would be sent.</summary>
        WouldUpdate,

        /// <summary>The place already holds this file (same SHA-256): nothing sent.</summary>
        Unchanged,

        /// <summary>The login may not change it.</summary>
        Refused,

        /// <summary>The tenant's platform cannot receive the file from the SDK.</summary>
        Unsupported,

        /// <summary>The platform answered with an error, or the request failed.</summary>
        Failed,
    }

    internal sealed class FileOutcome
    {
        public FilesTarget Target;
        public FileKind Kind;
        public OutcomeKind Result;
        public string Message;

        /// <summary>The project asset the place's previous file was saved to first (or already was), when it had one.</summary>
        public string SavedPrevious;

        /// <summary>True when the previous file was not saved because the replace is a dry run, and would be.</summary>
        public bool WouldSavePrevious;

        /// <summary>Set when the file was sent but something after it did not work (the stored file could not be read back).</summary>
        public string Warning;

        /// <summary>True when the target was left as it was although a change was asked for.</summary>
        public bool NotChanged => Result == OutcomeKind.Refused || Result == OutcomeKind.Unsupported || Result == OutcomeKind.Failed;
    }

    internal sealed class ReplaceResult
    {
        /// <summary>True when a file failed its check: nothing was sent.</summary>
        public bool Invalid;

        public List<PreparedFile> Files = new();
        public List<FileOutcome> Outcomes = new();

        /// <summary>What was left out and why (a place of a manifest that is not published any more): not an error.</summary>
        public List<string> Notes = new();

        public bool AnyNotChanged => Outcomes.Any(o => o.NotChanged);
    }

    /// <summary>What <see cref="EnvironmentFilesService.SaveToProjectAsync"/> did with one file.</summary>
    internal enum SaveStatus
    {
        /// <summary>The file was new to the project and was written.</summary>
        Written,

        /// <summary>A dry run: the file is new to the project and would be written.</summary>
        WouldWrite,

        /// <summary>The project already holds the same file: nothing written.</summary>
        AlreadyInProject,

        /// <summary>The environment has no such file.</summary>
        Absent,

        /// <summary>It has one and it could not be downloaded or written.</summary>
        Failed,
    }

    internal sealed class SavedFile
    {
        public FilesTarget Target;
        public FileKind Kind;
        public SaveStatus Status;

        /// <summary>The project asset that holds the file (a path under <c>Assets/</c>, or under the backup folder when one was named).</summary>
        public string Asset;

        public string Sha256;
        public string Error;
    }

    /// <summary>One file of an entry of a save manifest.</summary>
    internal sealed class ManifestFile
    {
        /// <summary><c>saved</c> (the project holds it), <c>absent</c> (the environment has none) or <c>failed</c>.</summary>
        public string Status;

        /// <summary>A path under <c>Assets/</c>; null unless saved into the project.</summary>
        public string Asset;

        /// <summary>A path relative to the manifest, for a copy written outside the project; null otherwise.</summary>
        public string File;

        public string Sha256;
        public string Error;
    }

    /// <summary>One environment of a save manifest.</summary>
    internal sealed class ManifestEntry
    {
        public string Scene;
        public string SceneKey;
        public int? WorldId;
        public string WorldLabel;
        public bool Tenant;
        public int EnvId;
        public string EnvLabel;
        public ManifestFile Thumbnail;
        public ManifestFile Localization;
    }

    /// <summary>The record <c>save-files</c> writes, which <c>restore</c> reads.</summary>
    internal sealed class SaveManifest
    {
        public int Version = 1;
        public DateTime CreatedUtc;
        public string Connection;
        public string TenantLabel;
        public List<ManifestEntry> Entries = new();
    }

    internal sealed class SaveResult
    {
        public List<SavedFile> Files = new();
        public SaveManifest Manifest;

        /// <summary>The manifest's path (under <c>Assets/</c>, or in the backup folder).</summary>
        public string ManifestPath;

        public int Written => Files.Count(f => f.Status == SaveStatus.Written || f.Status == SaveStatus.WouldWrite);
        public int AlreadyInProject => Files.Count(f => f.Status == SaveStatus.AlreadyInProject);
        public int Absent => Files.Count(f => f.Status == SaveStatus.Absent);
        public int Failed => Files.Count(f => f.Status == SaveStatus.Failed);
    }
}
