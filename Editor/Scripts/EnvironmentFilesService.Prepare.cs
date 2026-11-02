using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The project's images and localization files by SHA-256: what makes "the same file in the project" one dictionary lookup, and what keeps the
    /// SDK from ever writing a file the project already holds. It covers every <c>.jpg</c>, <c>.jpeg</c>, <c>.png</c> and <c>.csv</c> under
    /// <c>Assets/</c> and the scene list's default assets wherever they are, so a JPEG sent as it is from <c>Assets/Art/</c> is recognised and never
    /// copied. When two files are equal the one in <see cref="EnvironmentFilesService.PublishFolder"/> wins, then the first by path. A file is hashed again only
    /// when its length or modification time changed: the hashes are kept in <c>Library/Virtuademy/EnvironmentDetails/project-hashes.json</c> (outside git).
    /// </summary>
    internal static class PublishedFiles
    {
        private static readonly string[] extensions = { ".jpg", ".jpeg", ".png", ".csv" };

        private sealed class Entry
        {
            public long Length;
            public long Ticks;
            public string Sha;
        }

        // full path -> (length, last write ticks, hash)
        private static Dictionary<string, Entry> hashes;
        private static bool dirty;

        // The index is rebuilt at most every few seconds, and at once after the SDK writes a file.
        private static Dictionary<string, string> memo;
        private static DateTime memoAt;

        private static string CacheFile => Path.Combine(EnvironmentFilesService.CacheFolder, "project-hashes.json");

        public static void Invalidate() => memo = null;

        /// <summary>The project's files: SHA-256 to asset path.</summary>
        public static Dictionary<string, string> Index()
        {
            if (memo != null && (DateTime.UtcNow - memoAt).TotalSeconds < 3)
                return memo;

            LoadCache();
            Dictionary<string, string> index = new(StringComparer.OrdinalIgnoreCase);

            string publish = EnvironmentFilesService.FullPathOf(EnvironmentFilesService.PublishFolder);
            string assets = EnvironmentFilesService.FullPathOf("Assets");
            if (Directory.Exists(publish))
                AddFolder(index, publish, null);

            if (Directory.Exists(assets))
                AddFolder(index, assets, publish);

            foreach (string asset in DefaultAssets())
            {
                string full = EnvironmentFilesSettingsPath(asset);
                string sha = full == null ? null : HashOf(full);
                if (sha != null && !index.ContainsKey(sha))
                    index[sha] = asset;
            }

            SaveCache();
            memo = index;
            memoAt = DateTime.UtcNow;
            return index;
        }

        private static void AddFolder(Dictionary<string, string> index, string root, string skipFolder)
        {
            string backups = EnvironmentFilesService.FullPathOf(EnvironmentFilesService.ManifestFolder);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (!extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) || file.StartsWith(backups, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (skipFolder != null && file.StartsWith(skipFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;

                string sha = HashOf(file);
                if (sha != null && !index.ContainsKey(sha))
                    index[sha] = AssetPathOf(file);
            }
        }

        // The scene list's default files that are not under Assets/ (a package's): the ones under Assets/ are in the walk above.
        private static IEnumerable<string> DefaultAssets()
        {
            SceneListScriptableObject list = SceneListScriptableObject.Find();
            if (list?.SceneConfigurations == null)
                yield break;

            foreach (SceneListScriptableObject.SceneConfiguration config in list.SceneConfigurations)
            {
                foreach (UnityEngine.Object asset in new UnityEngine.Object[] { config.DefaultThumbnail, config.DefaultLocalization })
                {
                    string path = asset == null ? null : AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets/", StringComparison.Ordinal))
                        yield return path;
                }
            }
        }

        private static string EnvironmentFilesSettingsPath(string assetPath)
        {
            if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                return null;

            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            if (package == null || string.IsNullOrEmpty(package.resolvedPath))
                return null;

            string full = Path.GetFullPath(Path.Combine(package.resolvedPath, assetPath.Substring(("Packages/" + package.name).Length).TrimStart('/')));
            return File.Exists(full) ? full : null;
        }

        public static string HashOf(string fullPath)
        {
            try
            {
                LoadCache();
                FileInfo info = new(fullPath);
                if (hashes.TryGetValue(fullPath, out Entry known) && known.Length == info.Length && known.Ticks == info.LastWriteTimeUtc.Ticks)
                    return known.Sha;

                string sha = EnvironmentFilesService.Sha256(File.ReadAllBytes(fullPath));
                hashes[fullPath] = new Entry { Length = info.Length, Ticks = info.LastWriteTimeUtc.Ticks, Sha = sha };
                dirty = true;
                return sha;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The asset path (<c>Assets/...</c>) of a file inside the project.</summary>
        public static string AssetPathOf(string fullPath)
        {
            string root = EnvironmentFilesService.ProjectRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return Path.GetFullPath(fullPath).Substring(root.Length).Replace('\\', '/');
        }

        private static void LoadCache()
        {
            if (hashes != null)
                return;

            hashes = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(CacheFile))
                {
                    Dictionary<string, Entry> read = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(CacheFile));
                    if (read != null)
                        foreach (KeyValuePair<string, Entry> pair in read)
                            hashes[pair.Key] = pair.Value;
                }
            }
            catch (Exception)
            {
                // A cache that cannot be read is a cache that is not there: every file is hashed again.
            }
        }

        private static void SaveCache()
        {
            if (!dirty)
                return;

            dirty = false;
            try
            {
                // Only files that still exist are kept.
                foreach (string gone in hashes.Keys.Where(k => !File.Exists(k)).ToList())
                    hashes.Remove(gone);

                Directory.CreateDirectory(EnvironmentFilesService.CacheFolder);
                File.WriteAllText(CacheFile, Newtonsoft.Json.JsonConvert.SerializeObject(hashes));
            }
            catch (Exception)
            {
                // Not fatal: the next run hashes again.
            }
        }
    }

    internal static partial class EnvironmentFilesService
    {
        /// <summary>
        /// Reads an image of the project and gets it ready to go online as a hero image: a JPEG of at most 2560 x 1440, never upscaled,
        /// transparency flattened on white, quality 85. A JPEG the platform keeps as it is (<see cref="ThumbnailCheck.StoredAsIs"/>) is
        /// sent unchanged; anything else is converted into a new JPEG named after the source (<c>hero.png</c> to <c>hero.jpg</c>, <c> (2)</c>
        /// when a different file has the name) in <see cref="PublishFolder"/>, unless the project already holds the same file, which is reused.
        /// The bytes are the file's own, read from disk, never Unity's import of it.
        /// </summary>
        /// <param name="assetPath">The image: a path under <c>Assets/</c> (a restore may name any file, with <paramref name="requireAsset"/> false).</param>
        /// <param name="write">False in a dry run: the conversion is done in memory and reported, nothing is written.</param>
        public static PreparedFile PrepareThumbnail(string assetPath, bool write = false, bool requireAsset = true)
        {
            PreparedFile prepared = new() { Kind = FileKind.Thumbnail, SourceAsset = assetPath, ContentType = "image/jpeg" };
            ThumbnailCheck check = new();
            prepared.Check = check;

            if (!TryRead(assetPath, requireAsset, check, out byte[] bytes))
                return prepared;

            ThumbnailCheck source = ThumbnailCheck.Check(bytes);
            prepared.Check = source;
            prepared.Width = source.Width;
            prepared.Height = source.Height;

            // The platform's size limit is about what is uploaded: a large image that is converted is not refused for its size here.
            if (source.Errors.Any(e => e.Code != "too-large") || (source.Errors.Count > 0 && source.StoredAsIs))
                return prepared;

            source.Errors.Clear();

            // The platform refuses an image over 50 megapixels; the size is in the header, so say so before decoding anything.
            if ((long)source.Width * source.Height > max_pixels)
            {
                source.Errors.Add(new FileIssue { Code = "too-many-pixels", Message = $"The image is {source.Width} \u00d7 {source.Height}, over {max_pixels / 1_000_000} megapixels: the platform refuses it." });
                return prepared;
            }

            if (source.StoredAsIs)
            {
                // Sent as it is, so decode it once here: scan data that is corrupt would be refused by the platform.
                if (!Decodes(bytes))
                {
                    source.Errors.Add(new FileIssue { Code = "corrupt", Message = "The JPEG could not be decoded: its image data looks corrupt." });
                    return prepared;
                }

                prepared.Bytes = bytes;
                prepared.Sha256 = Sha256(bytes);
                return prepared;
            }

            byte[] jpeg;
            try
            {
                jpeg = ConvertToHeroJpeg(bytes, source, out int width, out int height);
                prepared.Width = width;
                prepared.Height = height;
            }
            catch (Exception ex)
            {
                // A JPEG that Unity cannot read (CMYK, say) is one the platform may still accept: it goes as it is and the platform re-encodes it.
                if (source.Format == "jpeg")
                {
                    source.Warnings.Add(new FileIssue { Code = "not-prepared", Message = $"The JPEG could not be prepared here ({ex.Message}): it is sent as it is and the platform re-encodes it, so the place will not read as holding the same file." });
                    prepared.Bytes = bytes;
                    prepared.Sha256 = Sha256(bytes);
                    return prepared;
                }

                source.Errors.Add(new FileIssue { Code = "conversion", Message = $"The image could not be converted to a JPEG: {ex.Message}" });
                return prepared;
            }

            if (jpeg.Length > ThumbnailCheck.MaxBytes)
            {
                source.Errors.Add(new FileIssue { Code = "too-large", Message = $"The converted JPEG is {jpeg.Length / (1024.0 * 1024.0):0.#} MB: the platform accepts a thumbnail of at most {ThumbnailCheck.MaxBytes / (1024 * 1024)} MB." });
                return prepared;
            }

            // The platform keeps this JPEG as it is only if it passes its own rule; Unity's encoder output does (checked), and a hash that
            // does not match what is stored would make "same file" fail, so say so if that ever stops being true.
            if (!ThumbnailCheck.Check(jpeg).StoredAsIs)
                source.Warnings.Add(new FileIssue { Code = "not-kept-as-is", Message = "The converted JPEG is not one the platform keeps as it is: it may re-encode it, and the place will then not read as holding the same file." });

            prepared.Bytes = jpeg;
            prepared.Sha256 = Sha256(jpeg);

            // The project may already hold exactly this JPEG: reuse it, never write it twice.
            if (PublishedFiles.Index().TryGetValue(prepared.Sha256, out string existing))
            {
                prepared.ConvertedAsset = existing;
                return prepared;
            }

            string target = FreeName(PublishFolder, Path.GetFileNameWithoutExtension(assetPath), ".jpg");
            prepared.ConvertedAsset = target;
            prepared.ConversionPending = true;
            if (write)
                CommitConversion(prepared);

            return prepared;
        }

        /// <summary>
        /// Writes a converted image into the publish folder, right before it is sent (nothing is in <c>Assets/</c> when a run stops earlier). The name was free
        /// when the image was prepared; if another file has taken it since, the next free one is used. Null when done; the reason when it cannot be written.
        /// </summary>
        internal static string CommitConversion(PreparedFile file)
        {
            if (!file.ConversionPending)
                return null;

            try
            {
                if (File.Exists(FullPathOf(file.ConvertedAsset)) && PublishedFiles.HashOf(FullPathOf(file.ConvertedAsset)) != file.Sha256)
                    file.ConvertedAsset = FreeName(PublishFolder, Path.GetFileNameWithoutExtension(file.ConvertedAsset), ".jpg");

                WriteAsset(file.ConvertedAsset, file.Bytes);
                file.ConversionPending = false;
                return null;
            }
            catch (Exception ex)
            {
                return $"The converted image could not be written to {file.ConvertedAsset}: {ex.Message}";
            }
        }

        private const long max_pixels = 50_000_000;

        // Whether Unity can read the image (it keeps nothing: the texture is destroyed at once).
        private static bool Decodes(byte[] bytes)
        {
            Texture2D probe = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                return probe.LoadImage(bytes, true);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Reads a localization file of the project and checks it against the platform's rules and, when the tenant's languages are
        /// given, against them. The bytes are sent as they are.
        /// </summary>
        public static PreparedFile PrepareLocalization(string assetPath, IReadOnlyList<string> tenantLanguages, bool requireAsset = true)
        {
            PreparedFile prepared = new() { Kind = FileKind.Localization, SourceAsset = assetPath, ContentType = "text/csv" };
            LocalizationCsvCheck probe = new();
            prepared.Check = probe;

            if (!TryRead(assetPath, requireAsset, probe, out byte[] bytes))
                return prepared;

            prepared.Check = LocalizationCsvCheck.Check(bytes, tenantLanguages);
            prepared.Bytes = bytes;
            prepared.Sha256 = Sha256(bytes);
            prepared.StoredSha256 = StoredSha256(bytes);
            return prepared;
        }

        private static bool TryRead(string assetPath, bool requireAsset, FileCheckResult check, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                check.Errors.Add(new FileIssue { Code = "no-file", Message = "No file was given." });
                return false;
            }

            string normalized = assetPath.Replace('\\', '/');
            if (requireAsset && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                check.Errors.Add(new FileIssue { Code = "outside-assets", Message = $"'{assetPath}' is not under Assets/: only files of the project are sent." });
                return false;
            }

            string full = requireAsset ? FullPathOf(normalized) : Path.GetFullPath(assetPath);
            if (!File.Exists(full))
            {
                check.Errors.Add(new FileIssue { Code = "not-found", Message = $"The file '{assetPath}' does not exist." });
                return false;
            }

            bytes = File.ReadAllBytes(full);
            return true;
        }

        /// <summary>The asset path of a file in the publish folder, with <c> (2)</c>, <c> (3)</c>... when the name is taken.</summary>
        internal static string FreeName(string folder, string name, string extension)
        {
            string safe = Regex.Replace(name, @"[\\/:*?""<>|]+", "-").Trim();
            if (safe.Length == 0)
                safe = "file";

            string candidate = $"{folder}/{safe}{extension}";
            for (int n = 2; File.Exists(FullPathOf(candidate)); n++)
                candidate = $"{folder}/{safe} ({n}){extension}";

            return candidate;
        }

        /// <summary>Writes a file into the project and imports it, so the asset exists when the call returns.</summary>
        internal static void WriteAsset(string assetPath, byte[] bytes)
        {
            string full = FullPathOf(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
            PublishedFiles.Invalidate();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        #region Image conversion

        private const int jpeg_quality = 85;

        // Decodes, applies the EXIF orientation (Unity's decoder ignores it), flattens on white, scales down by area averaging and encodes.
        private static byte[] ConvertToHeroJpeg(byte[] bytes, ThumbnailCheck source, out int width, out int height)
        {
            Texture2D decoded = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(bytes, false))
                    throw new InvalidOperationException("Unity cannot decode it (a CMYK JPEG, for one).");

                int w = decoded.width, h = decoded.height;
                Color32[] bottomUp = decoded.GetPixels32();

                // Top-left origin, the way the EXIF orientations are written.
                Color32[] pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    Array.Copy(bottomUp, y * w, pixels, (h - 1 - y) * w, w);

                if (source.Format == "jpeg" && source.ExifOrientation > 1)
                    pixels = Orient(pixels, ref w, ref h, source.ExifOrientation);

                // Flatten before scaling, so a soft transparent edge averages into white and not into black.
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    if (p.a == 255)
                        continue;

                    int a = p.a;
                    pixels[i] = new Color32((byte)((p.r * a + 255 * (255 - a)) / 255), (byte)((p.g * a + 255 * (255 - a)) / 255),
                                            (byte)((p.b * a + 255 * (255 - a)) / 255), 255);
                }

                if (w > ThumbnailCheck.MaxWidth || h > ThumbnailCheck.MaxHeight)
                {
                    double scale = Math.Min((double)ThumbnailCheck.MaxWidth / w, (double)ThumbnailCheck.MaxHeight / h);
                    int nw = Math.Max(1, (int)Math.Floor(w * scale));
                    int nh = Math.Max(1, (int)Math.Floor(h * scale));
                    pixels = Shrink(pixels, w, h, nw, nh);
                    w = nw;
                    h = nh;
                }

                Color32[] up = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    Array.Copy(pixels, y * w, up, (h - 1 - y) * w, w);

                Texture2D output = new(w, h, TextureFormat.RGB24, false);
                try
                {
                    output.SetPixels32(up);
                    output.Apply(false);
                    width = w;
                    height = h;
                    return output.EncodeToJPG(jpeg_quality);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        // The EXIF orientation values 2 to 8, each as the way back from an output pixel to its source pixel.
        private static Color32[] Orient(Color32[] src, ref int w, ref int h, int orientation)
        {
            bool swap = orientation >= 5;
            int ow = swap ? h : w, oh = swap ? w : h;
            Color32[] result = new Color32[ow * oh];

            for (int oy = 0; oy < oh; oy++)
            {
                for (int ox = 0; ox < ow; ox++)
                {
                    int sx, sy;
                    switch (orientation)
                    {
                        case 2: sx = w - 1 - ox; sy = oy; break;
                        case 3: sx = w - 1 - ox; sy = h - 1 - oy; break;
                        case 4: sx = ox; sy = h - 1 - oy; break;
                        case 5: sx = oy; sy = ox; break;
                        case 6: sx = oy; sy = h - 1 - ox; break;
                        case 7: sx = w - 1 - oy; sy = h - 1 - ox; break;
                        default: sx = w - 1 - oy; sy = ox; break;
                    }

                    result[oy * ow + ox] = src[sy * w + sx];
                }
            }

            w = ow;
            h = oh;
            return result;
        }

        // Area averaging: every output pixel is the mean of the source pixels it covers.
        private static Color32[] Shrink(Color32[] src, int w, int h, int nw, int nh)
        {
            Color32[] result = new Color32[nw * nh];
            for (int dy = 0; dy < nh; dy++)
            {
                int y0 = (int)((long)dy * h / nh), y1 = Math.Max(y0 + 1, (int)(((long)(dy + 1) * h + nh - 1) / nh));
                for (int dx = 0; dx < nw; dx++)
                {
                    int x0 = (int)((long)dx * w / nw), x1 = Math.Max(x0 + 1, (int)(((long)(dx + 1) * w + nw - 1) / nw));
                    long r = 0, g = 0, b = 0;
                    for (int y = y0; y < y1 && y < h; y++)
                    {
                        for (int x = x0; x < x1 && x < w; x++)
                        {
                            Color32 p = src[y * w + x];
                            r += p.r;
                            g += p.g;
                            b += p.b;
                        }
                    }

                    int count = (Math.Min(y1, h) - y0) * (Math.Min(x1, w) - x0);
                    result[dy * nw + dx] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }

            return result;
        }

        #endregion
    }
}
