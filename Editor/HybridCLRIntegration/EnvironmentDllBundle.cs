using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

using UnityEngine;

namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    /// <summary>
    /// Packs this project's compiled hot-update assembly — one DLL per build target — into
    /// the zip the platform's environment-DLL import expects:
    ///
    ///     &lt;Platform&gt;/&lt;AssemblyName&gt;.dll.bytes
    ///
    /// The assembly belongs to the PROJECT, not to a scene, which is why this does not live in
    /// the per-scene zip: it is built once per addressables build, beside the scene zips, and
    /// each scene zip names it in its environment-dll.json. The window uploads it once per world;
    /// someone importing from the Backoffice uploads it next to the scene zips by hand.
    /// </summary>
    public static class EnvironmentDllBundle
    {
        /// <summary>Extension the player and the backend agree on for a shipped assembly.</summary>
        private const string DllExtension = ".dll.bytes";

        /// <summary>Same folder the addressables zips are staged in.</summary>
        private const string OutputFolder = "ServerData";

        /// <summary>Assembly name the bundle carries. Exposed for the caller's API calls.</summary>
        public static string AssemblyName => HotUpdateSetupper.HotUpdateAssemblyName;

        /// <summary>
        /// Builds the bundle from the DLLs produced by the last compile. Returns the zip path,
        /// or null when a target is missing — the import rejects an incomplete bundle anyway,
        /// so failing here gives the author the actionable message instead of a server 400.
        /// </summary>
        public static string Build()
        {
            string assemblyName = AssemblyName;
            string projectRoot = HotUpdateDllLocator.ProjectRoot;

            string[] platforms = HotUpdateSetupper.TargetNames;
            string[] missing = platforms
                .Where(p => !File.Exists(SourceDll(projectRoot, p, assemblyName)))
                .ToArray();

            if (missing.Length > 0)
            {
                Debug.LogError($"[EnvironmentDll] Cannot build the bundle: no compiled assembly for " +
                               $"{string.Join(", ", missing)}. Compile the interpreted scripts first.");
                return null;
            }

            string outputDir = Path.Combine(projectRoot, OutputFolder);
            Directory.CreateDirectory(outputDir);

            string zipPath = Path.Combine(outputDir, assemblyName + ".zip");

            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using FileStream zipStream = new(zipPath, FileMode.CreateNew);
                using ZipArchive zip = new(zipStream, ZipArchiveMode.Create);

                foreach (string platform in platforms)
                {
                    // Forward slashes: zip entry names are not OS paths, and the backend splits
                    // them on '/' to read the platform.
                    zip.CreateEntryFromFile(
                        SourceDll(projectRoot, platform, assemblyName),
                        $"{platform}/{assemblyName}{DllExtension}",
                        System.IO.Compression.CompressionLevel.Optimal);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentDll] Failed to build the bundle: {e.Message}");
                return null;
            }

            Debug.Log($"[EnvironmentDll] Bundle ready: {zipPath} ({platforms.Length} targets)");
            return zipPath;
        }

        private static string SourceDll(string projectRoot, string platform, string assemblyName)
            => Path.Combine(projectRoot, "HybridCLRData", "HotUpdateDlls", platform, assemblyName + ".dll");
    }
}
