using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.BreakingChangeSolvers
{
    /// <summary>
    /// Finds the project assets whose GUID is also the GUID of an asset in an installed package, and
    /// resolves each collision for good: either the project's copy keeps its content under a new
    /// GUID, or it is deleted in favour of the package's copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creator projects imported template assets (fonts, a click sound, UI prefabs, utility graphs)
    /// with their .meta files, and some of those assets later moved into
    /// Virtuademy-SDK-Environments with the same GUIDs. Once the package is installed, Unity finds
    /// two assets claiming one GUID; it logs "GUID [...] for asset 'Assets/...' conflicts with
    /// 'Packages/...' — Assigning a new guid", keeps the package's asset on the GUID and gives the
    /// project's copy a new one, <b>in memory only</b>: the .meta on disk keeps the old GUID. Two
    /// things follow. Every reference the project made to its own copy now resolves to the
    /// package's asset, which is not always the same content. And every scene saved afterwards
    /// writes the in-memory GUID, which differs on every machine and every reimport, so the next
    /// colleague who opens it finds missing prefabs (observed on Reflectis-EducationContent,
    /// 2026-10-12: six assets, three of them already saved under a GUID only one editor knew).
    /// </para>
    /// <para>
    /// For each collision there are two resolutions, chosen per asset in the routine's window:
    /// <list type="bullet">
    /// <item><b>Keep the project's copy</b> (the default when the two differ): its .meta gets a
    /// permanent new GUID, and every reference under Assets/ and ProjectSettings/ that points at
    /// the old GUID is rewritten to it, because those references were made to the project's
    /// copy. The new GUID is the one Unity assigned in memory when there is one, so references
    /// saved on this machine since the conflict stay valid.</item>
    /// <item><b>Use the package's copy</b> (the default when the two are identical): the
    /// project's copy is deleted. References to the old GUID already resolve to the package's
    /// asset; references saved on this machine under the in-memory GUID are rewritten back to
    /// the old one.</item>
    /// </list>
    /// References saved on another machine under that machine's in-memory GUID cannot be told
    /// apart from any other missing reference, and are not touched.
    /// </para>
    /// <para>
    /// Detection reads the package .meta files of our own packages directly, so it does not
    /// depend on how Unity resolved the conflict; for every other package it relies on the
    /// AssetDatabase reporting a GUID different from the one the project's .meta declares.
    /// </para>
    /// <para>
    /// This has no menu entry of its own: it is part of the v2026.5 -> v2026.6 update routine,
    /// driven by <c>VirtuademyRenameMigrator</c>, and the scenes, prefabs and assets it rewrites
    /// are re-saved by <c>VirtuademyUpdateResave</c>. Idempotent: a second run finds nothing.
    /// </para>
    /// </remarks>
    public static class VirtuademyGuidCollisionMigrator
    {
        public enum Resolution
        {
            KeepProjectCopy,
            UsePackageCopy,
        }

        public class Entry
        {
            /// <summary>The project asset, relative to the project root.</summary>
            public string Path;
            /// <summary>The package asset that holds the same GUID, as the AssetDatabase names it.</summary>
            public string PackagePath;
            /// <summary>The GUID both .meta files declare.</summary>
            public string Guid;
            /// <summary>The GUID the AssetDatabase gave the project's copy, or null when it gave none.</summary>
            public string InMemoryGuid;
            public bool IsFolder;
            public bool Identical;
            /// <summary>Files under Assets/ and ProjectSettings/ that name <see cref="Guid"/> or
            /// <see cref="InMemoryGuid"/>, the asset's own .meta excluded.</summary>
            public List<string> ReferencingFiles = new();
            public Resolution Resolution;
        }

        /// <summary>The id prefix of our packages, whose .meta files are read directly.</summary>
        private const string OwnPackagePrefix = "com.anotherealitysrl.";

        private static readonly Regex MetaGuidLine =
            new(@"^guid: (?<guid>[0-9a-f]{32})\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>Text files that can hold a GUID reference, besides every .meta.</summary>
        private static readonly string[] ReferenceExtensions =
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".overridecontroller", ".anim",
            ".playable", ".mask", ".signal", ".lighting", ".spriteatlas", ".spriteatlasv2",
            ".rendertexture", ".physicmaterial", ".physicsmaterial2d", ".mixer", ".terrainlayer",
            ".preset", ".brush", ".flare", ".guiskin", ".fontsettings", ".cubemap",
            ".uxml", ".uss", ".tss", ".shadergraph", ".shadersubgraph", ".vfx", ".json", ".asmref",
        };

        public static List<Entry> Scan()
        {
            Dictionary<string, string> ownPackageGuids = OwnPackageGuids();
            List<Entry> entries = new();

            foreach (string metaPath in Directory.EnumerateFiles("Assets", "*.meta", SearchOption.AllDirectories))
            {
                string guid = ReadMetaGuid(metaPath);
                if (guid == null)
                {
                    continue;
                }

                string assetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length).Replace('\\', '/');
                string inMemory = AssetDatabase.AssetPathToGUID(assetPath);
                bool reassigned = !string.IsNullOrEmpty(inMemory) && inMemory != guid;

                string packageFile = null;
                if (ownPackageGuids.TryGetValue(guid, out string ownFile))
                {
                    packageFile = ownFile;
                }
                else if (reassigned)
                {
                    // Not one of ours: trust the AssetDatabase, which put the old GUID on the
                    // package's asset and a new one on ours.
                    string holder = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(holder) && holder.StartsWith("Packages/", StringComparison.Ordinal))
                    {
                        packageFile = Path.GetFullPath(holder);
                    }
                }

                if (packageFile == null)
                {
                    continue;
                }

                bool isFolder = Directory.Exists(assetPath);
                bool identical = !isFolder && SameContent(assetPath, packageFile);
                entries.Add(new Entry
                {
                    Path = assetPath,
                    PackagePath = PackageDisplayPath(guid, packageFile),
                    Guid = guid,
                    InMemoryGuid = reassigned ? inMemory : null,
                    IsFolder = isFolder,
                    Identical = identical,
                    Resolution = identical ? Resolution.UsePackageCopy : Resolution.KeepProjectCopy,
                });
            }

            if (entries.Count > 0)
            {
                foreach (string path in ReferenceFiles())
                {
                    string text = ReadTextOrNull(path);
                    if (text == null)
                    {
                        continue;
                    }
                    string normalised = path.Replace('\\', '/');
                    foreach (Entry entry in entries)
                    {
                        // The asset's own .meta names its GUID too; it is not a reference.
                        if (normalised.Equals(entry.Path + ".meta", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (text.Contains(entry.Guid) || (entry.InMemoryGuid != null && text.Contains(entry.InMemoryGuid)))
                        {
                            entry.ReferencingFiles.Add(normalised);
                        }
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Applies the chosen resolution of every entry: .meta GUIDs rewritten or project copies
        /// deleted, then references rewritten across the project. Returns how many collisions were
        /// resolved, and adds the rewritten files to <paramref name="changedPaths"/> when given.
        /// Call AssetDatabase.Refresh afterwards.
        /// </summary>
        public static int Apply(IEnumerable<Entry> entries, out int failed, ICollection<string> changedPaths = null)
        {
            failed = 0;
            int resolved = 0;
            Dictionary<string, string> referenceMap = new(StringComparer.Ordinal);
            HashSet<string> skip = new(StringComparer.OrdinalIgnoreCase);

            foreach (Entry entry in entries)
            {
                try
                {
                    string metaPath = entry.Path + ".meta";
                    if (entry.Resolution == Resolution.KeepProjectCopy || entry.IsFolder)
                    {
                        string newGuid = entry.InMemoryGuid ?? GUID.Generate().ToString();
                        string meta = File.ReadAllText(metaPath, new UTF8Encoding(false));
                        File.WriteAllText(metaPath,
                            MetaGuidLine.Replace(meta, m => m.Value.Replace(entry.Guid, newGuid), 1),
                            new UTF8Encoding(false));
                        skip.Add(Path.GetFullPath(metaPath));
                        referenceMap[entry.Guid] = newGuid;
                    }
                    else
                    {
                        File.Delete(entry.Path);
                        File.Delete(metaPath);
                        if (entry.InMemoryGuid != null)
                        {
                            referenceMap[entry.InMemoryGuid] = entry.Guid;
                        }
                    }
                    resolved++;
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[GUID collisions] Failed to resolve {entry.Path}: {e}");
                }
            }

            if (referenceMap.Count == 0)
            {
                return resolved;
            }

            foreach (string path in ReferenceFiles())
            {
                if (skip.Contains(Path.GetFullPath(path)))
                {
                    continue;
                }

                try
                {
                    string text = ReadTextOrNull(path);
                    if (text == null)
                    {
                        continue;
                    }

                    string rewritten = RewriteReferences(text, referenceMap, out int hits);
                    if (hits > 0)
                    {
                        File.WriteAllText(path, rewritten, new UTF8Encoding(HasUtf8Bom(path)));
                        changedPaths?.Add(path.Replace('\\', '/'));
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[GUID collisions] Failed to rewrite references in {path}: {e}");
                }
            }

            return resolved;
        }

        /// <summary>
        /// Replaces every GUID in <paramref name="map"/> with its new value, as a whole 32-digit
        /// token, in one pass so that a GUID that is both a key and a value is not rewritten
        /// twice. Pure, so it can be checked outside the editor.
        /// </summary>
        public static string RewriteReferences(string text, IReadOnlyDictionary<string, string> map, out int hits)
        {
            int count = 0;
            string result = Regex.Replace(text, @"(?<![0-9a-f])[0-9a-f]{32}(?![0-9a-f])", m =>
            {
                if (map.TryGetValue(m.Value, out string replacement))
                {
                    count++;
                    return replacement;
                }
                return m.Value;
            });
            hits = count;
            return result;
        }

        /// <summary>GUID -> full path of the .meta's asset, for every package of ours.</summary>
        private static Dictionary<string, string> OwnPackageGuids()
        {
            Dictionary<string, string> guids = new(StringComparer.Ordinal);
            foreach (UnityEditor.PackageManager.PackageInfo package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (!package.name.StartsWith(OwnPackagePrefix, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(package.resolvedPath)
                    || !Directory.Exists(package.resolvedPath))
                {
                    continue;
                }

                foreach (string metaPath in Directory.EnumerateFiles(package.resolvedPath, "*.meta", SearchOption.AllDirectories))
                {
                    string guid = ReadMetaGuid(metaPath);
                    if (guid != null)
                    {
                        guids[guid] = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                    }
                }
            }
            return guids;
        }

        private static string PackageDisplayPath(string guid, string packageFile)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Packages/", StringComparison.Ordinal)
                ? path
                : packageFile.Replace('\\', '/');
        }

        private static IEnumerable<string> ReferenceFiles()
        {
            IEnumerable<string> assets = Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                         || ReferenceExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
            IEnumerable<string> settings = Directory.Exists("ProjectSettings")
                ? Directory.EnumerateFiles("ProjectSettings", "*.asset", SearchOption.TopDirectoryOnly)
                : Enumerable.Empty<string>();
            return assets.Concat(settings);
        }

        private static string ReadMetaGuid(string metaPath)
        {
            try
            {
                using StreamReader reader = new(metaPath);
                for (int i = 0; i < 4 && !reader.EndOfStream; i++)
                {
                    Match m = MetaGuidLine.Match(reader.ReadLine() ?? string.Empty);
                    if (m.Success)
                    {
                        return m.Groups["guid"].Value;
                    }
                }
            }
            catch (IOException)
            {
            }
            return null;
        }

        /// <summary>Byte-equal, or equal as text once line endings are normalised.</summary>
        private static bool SameContent(string a, string b)
        {
            try
            {
                byte[] x = File.ReadAllBytes(a);
                byte[] y = File.ReadAllBytes(b);
                if (x.AsSpan().SequenceEqual(y))
                {
                    return true;
                }
                if (IsBinary(x) || IsBinary(y))
                {
                    return false;
                }
                static string Normalise(byte[] bytes) =>
                    new UTF8Encoding(false).GetString(bytes).TrimStart('﻿').Replace("\r\n", "\n");
                return Normalise(x) == Normalise(y);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool IsBinary(byte[] bytes)
        {
            int probe = Math.Min(bytes.Length, 8000);
            for (int i = 0; i < probe; i++)
            {
                if (bytes[i] == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReadTextOrNull(string path)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return null;
            }

            if (IsBinary(bytes))
            {
                return null;
            }

            string text = new UTF8Encoding(false).GetString(bytes);
            return text.Length > 0 && text[0] == '﻿' ? text.Substring(1) : text;
        }

        private static bool HasUtf8Bom(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return stream.Length >= 3 && stream.ReadByte() == 0xEF && stream.ReadByte() == 0xBB && stream.ReadByte() == 0xBF;
        }
    }
}
