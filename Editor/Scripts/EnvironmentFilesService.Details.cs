using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Virtuademy.SDK.Environments.Editor
{
    // What the Environment Details window needs on top of the listing, the saves and the replaces: the file a place holds, in the cache folder,
    // for a preview; and the sentence a scene without defaults gets. Nothing here is used by the command line.
    internal static partial class EnvironmentFilesService
    {
        /// <summary>
        /// Makes sure the file a place holds is in the cache folder (<see cref="CacheFolder"/>), so that a window can show it: a platform that
        /// says the file's hash is never asked for the file by <see cref="ListAsync"/>, so nothing was downloaded. Downloads it anonymously
        /// like <see cref="HashByDownloadAsync"/>, keeps it under its SHA-256, and for a thumbnail fills the size and the format. The hash the
        /// platform said is left as it is. True when <see cref="FileStatus.CachePath"/> names a file afterwards; otherwise
        /// <see cref="FileStatus.Error"/> says why (not when the place has no such file).
        /// </summary>
        public static async Task<bool> EnsureCachedAsync(FileStatus status, CancellationToken ct = default)
        {
            if (status == null || !status.Present || status.Uri == null)
                return false;

            if (status.CachePath != null && File.Exists(status.CachePath))
                return true;

            string uri = status.Uri;
            Download download = await DownloadAsync(uri, ct);

            // The place's file was replaced while this one downloaded: what came is not what the status describes any more.
            if (status.Uri != uri)
                return false;

            if (!download.Found)
            {
                status.Error = "The file's address does not answer (404).";
                return false;
            }

            if (download.Error != null)
            {
                status.Error = $"The file could not be downloaded: {download.Error}";
                return false;
            }

            string sha = Sha256(download.Bytes);
            if (status.Kind == FileKind.Thumbnail)
            {
                ThumbnailCheck info = ThumbnailCheck.Check(download.Bytes);
                status.Width = info.Width;
                status.Height = info.Height;
                status.Format = info.Format;
            }

            status.CachePath = TryCache(download.Bytes, sha, status.Kind == FileKind.Thumbnail ? ImageExtension(download.Bytes) : ".csv");
            if (status.CachePath == null)
            {
                status.Error = "The file could not be kept in the Library folder.";
                return false;
            }

            status.Error = null;
            return true;
        }

        /// <summary>
        /// What a deploy says about a scene that is new in a place and lacks default files, without asking the platform: the sentence of
        /// <see cref="PlanDefaultsAsync"/>. Null when the scene has both. <paramref name="thumbnail"/> and <paramref name="localization"/> are
        /// what <see cref="DefaultsOf"/> returned.
        /// </summary>
        public static string MissingDefaultsNote(string sceneName, string thumbnail, string localization)
        {
            if (thumbnail == null && localization == null)
                return $"{sceneName} has no default thumbnail or localization file: it will be published without them.";

            if (thumbnail == null)
                return $"{sceneName} has no default thumbnail: it will be published without it.";

            return localization == null ? $"{sceneName} has no default localization file: it will be published without it." : null;
        }
    }
}
