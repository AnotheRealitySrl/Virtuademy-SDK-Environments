using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

using UnityEngine;

using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>Why <see cref="EnvironmentsBuild.BuildAndZip"/> stopped.</summary>
    public enum EnvironmentsBuildFailure
    {
        /// <summary>The build ran to the end.</summary>
        None,

        /// <summary>A scene needs a Unity build module that is not installed.</summary>
        MissingModule,

        /// <summary>A platform folder is missing from the scene's <c>ServerData</c> output.</summary>
        FolderMissing,

        /// <summary>A platform folder has no catalog file.</summary>
        BinaryCatalog,

        /// <summary>
        /// A component of the scenes' content has a script that compiles into a <c>*-firstpass</c> assembly (it sits
        /// under <c>Assets/Plugins</c> or <c>Assets/Standard Assets</c>), which the app does not have: nothing was built.
        /// </summary>
        FirstpassScripts,
    }

    /// <summary>A scene as <see cref="EnvironmentsBuild.BuildAndZip"/> built it.</summary>
    public sealed class EnvironmentsBuiltScene
    {
        /// <summary>The scene's name.</summary>
        public string Name;

        /// <summary>The name its folder and zip carry in <c>ServerData</c>, and the environment's file name on the platform.</summary>
        public string AddressName;

        /// <summary>The build targets built for it: the sub-folders of its zip.</summary>
        public List<string> Platforms = new();

        /// <summary>The zip, relative to the project. Null when the build stopped before it.</summary>
        public string ZipPath;
    }

    /// <summary>The outcome of <see cref="EnvironmentsBuild.BuildAndZip"/>.</summary>
    public sealed class EnvironmentsBuildResult
    {
        public EnvironmentsBuildFailure Failure;

        /// <summary>For <see cref="EnvironmentsBuildFailure.MissingModule"/> and <see cref="EnvironmentsBuildFailure.FirstpassScripts"/>, the text the window's dialog shows.</summary>
        public string Message;

        /// <summary>For <see cref="EnvironmentsBuildFailure.MissingModule"/>, one entry per scene: its name and the modules it lacks.</summary>
        public List<string> MissingModules = new();

        /// <summary>For <see cref="EnvironmentsBuildFailure.FirstpassScripts"/>, every component found; <see cref="Message"/> explains and lists the first ones.</summary>
        public List<FirstpassComponent> FirstpassComponents = new();

        public List<EnvironmentsBuiltScene> Scenes = new();

        /// <summary>
        /// The interpreted assembly the scene zips declare, or null when the project has no
        /// interpreted scripts.
        /// </summary>
        public string ScriptsAssembly;

        /// <summary>The scripts bundle beside the scene zips (<c>ServerData/&lt;assembly&gt;.zip</c>), or null when it was not built.</summary>
        public string ScriptsArchive;

        public bool Success => Failure == EnvironmentsBuildFailure.None;
    }

    /// <summary>
    /// What <b>Build Addressables</b> in <see cref="AddressablesManagementWindow"/> does, without the
    /// window: verify the interpreted scripts, build the Addressables of every scene for every platform
    /// it needs, and zip each scene (and the scripts bundle) into <c>ServerData</c>. The window and
    /// <see cref="EnvironmentsCli"/> both call it, so they cannot drift.
    /// </summary>
    /// <remarks>
    /// The calls are synchronous where Unity needs them to be: a build switches the active build
    /// target, which queues a script recompilation and, when it finishes, a domain reload. The caller
    /// that awaits anything after <see cref="BuildAndZip"/> (a deploy) has to keep the reload from
    /// happening — <see cref="EnvironmentsCli"/> locks it for the whole run.
    /// </remarks>
    public static class EnvironmentsBuild
    {
        /// <summary>The folder, relative to the project, the bundles and zips are written to.</summary>
        public const string OutputFolder = AddressablesConfiguration.OutputFolder;

        /// <summary>
        /// Written into every scene folder before it is zipped, so the archive itself says which
        /// interpreted assembly its scenes were compiled against. The platform's archive import
        /// reads it: a name links the catalog to that assembly, null clears the link.
        /// </summary>
        internal const string EnvironmentDllDeclarationFile = "environment-dll.json";

        [Serializable]
        internal class EnvironmentDllDeclaration
        {
            public string assemblyName;
        }

        #region Interpreted scripts

        /// <summary>
        /// The setup class lives in the HYBRIDCLR_INSTALLED-gated assembly, which this one does not
        /// reference, so it is reached by reflection like the verifier is.
        /// </summary>
        internal static Type FindHotUpdateSetupperType() => FindEditorType("HotUpdateSetupper");

        internal static Type FindEditorType(string name)
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);

        private static T ReadSetupper<T>(string member, T fallback)
        {
            Type type = FindHotUpdateSetupperType();
            if (type == null)
                return fallback;

            MethodInfo method = type.GetMethod(member, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            object value = method != null
                ? method.Invoke(null, null)
                : type.GetProperty(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

            return value is T typed ? typed : fallback;
        }

        /// <summary>True when HybridCLR is installed, so the interpreted-scripts pipeline exists.</summary>
        public static bool InterpreterInstalled => FindHotUpdateSetupperType() != null;

        /// <summary>True when there is at least one interpreted script (a <c>.cs</c> under the hot-update folder).</summary>
        public static bool HasInterpretedScripts => ReadSetupper("ProjectHasInterpretedScripts", false);

        /// <summary>What still stops the interpreted scripts from compiling, or null when the setup is complete.</summary>
        public static string InterpreterSetupIssue => ReadSetupper<string>("GetSetupIssue", null);

        /// <summary>
        /// True when the last <see cref="VerifyInterpretedScriptsAsync"/> found the published bundle
        /// current and skipped compiling and verifying.
        /// </summary>
        public static bool InterpretedBundleIsCurrent => ReadSetupper("BundleIsCurrent", false);

        /// <summary>
        /// Builds the interpreted hot-update DLL and verifies it (local whitelist + authoritative
        /// server check). Returns true if the build may proceed. Called via reflection so this
        /// package stays decoupled from the HYBRIDCLR_INSTALLED-gated assembly; when HybridCLR is
        /// not installed the setupper is absent and we simply proceed (nothing to gate).
        /// </summary>
        /// <remarks>
        /// Without a session the server check cannot run, so a project that has interpreted scripts
        /// is refused (fail-closed): the policy comes from the platform or, failing that, from the
        /// copy cached by the last successful fetch; a project without scripts passes straight
        /// through. With an expired session the check renews it silently when it can.
        /// </remarks>
        public static async Task<bool> VerifyInterpretedScriptsAsync()
        {
            Type setupperType = FindHotUpdateSetupperType();

            var method = setupperType?.GetMethod("CompileVerifyAsync", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                // HybridCLR not installed → no interpreted-scripts pipeline to gate.
                Debug.LogWarning("[AddressablesManagement] HotUpdate verifier not found (HybridCLR not installed?). " +
                                 "Skipping interpreted DLL build/verify.");
                return true;
            }

            try
            {
                // CompileVerifyAsync returns Task<bool>, a BCL type this assembly can await directly.
                var task = (Task<bool>)method.Invoke(null, null);
                return await task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressablesManagement] Interpreted DLL verify failed to run: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Scenes

        /// <summary>The scenes marked <b>Include in build</b>, in the list's order.</summary>
        public static List<SceneConfiguration> IncludedScenes(SceneListScriptableObject list)
            => (list?.SceneConfigurations ?? new List<SceneConfiguration>())
                .Where(c => c != null && c.Scene != null && c.IncludeInBuild)
                .ToList();

        /// <summary>The zip a built scene is deployed from, relative to the project.</summary>
        public static string ZipPathOf(SceneConfiguration scene) => Path.Combine(OutputFolder, scene.SceneNameFiltered + ".zip");

        #endregion

        #region Build

        /// <summary>
        /// Builds addressables for all platforms and creates zip files. Does NOT upload — used by
        /// both "Build Addressables" and "Build &amp; Deploy" in the window, and by the command line.
        /// Refuses before anything is built when a scene needs a build module that is not installed, or when
        /// its content has a component whose script the app cannot bind (<see cref="EnvironmentsScriptCheck"/>).
        /// </summary>
        /// <param name="scenes">The scenes to build: normally <see cref="IncludedScenes"/>, or some of them.</param>
        public static EnvironmentsBuildResult BuildAndZip(IReadOnlyList<SceneConfiguration> scenes)
        {
            EnvironmentsBuildResult result = new();
            AddressableAssetSettings settings = AddressablesConfiguration.GetOrCreateSettings();

            // ── Pre-flight: check that all required build modules are installed ──────
            var scenesWithMissingModules = scenes
                .Where(s => s.GetMissingBuildTargets().Count > 0)
                .ToList();

            if (scenesWithMissingModules.Count > 0)
            {
                var lines = scenesWithMissingModules.Select(s =>
                {
                    var missing = s.GetMissingBuildTargets().Select(t => t switch
                    {
                        BuildTarget.Android => "Android (VR / Mobile)",
                        BuildTarget.iOS => "iOS (Mobile)",
                        BuildTarget.WebGL => "WebGL",
                        _ => t.ToString()
                    });
                    return $"• {s.SceneNameFiltered}: {string.Join(", ", missing)}";
                }).ToList();

                result.Message =
                    "The following scenes require build modules that are not installed:\n\n" +
                    string.Join("\n", lines) +
                    "\n\nInstall the missing modules via Unity Hub → Installs → Add modules, " +
                    "then retry.";
                result.MissingModules = lines;
                result.Failure = EnvironmentsBuildFailure.MissingModule;
                return result;
            }
            // ─────────────────────────────────────────────────────────────────────────

            // ── Pre-flight: scripts in a firstpass assembly, which the app does not have ─
            List<FirstpassComponent> firstpassComponents = EnvironmentsScriptCheck.FindFirstpassComponents(scenes);

            if (firstpassComponents.Count > 0)
            {
                result.FirstpassComponents = firstpassComponents;
                result.Message = EnvironmentsScriptCheck.Describe(firstpassComponents);
                result.Failure = EnvironmentsBuildFailure.FirstpassScripts;

                // The dialog and the report carry the first ones; the Console has every component.
                Debug.LogError("[AddressablesManagement] " + EnvironmentsScriptCheck.Describe(firstpassComponents, int.MaxValue));
                return result;
            }
            // ─────────────────────────────────────────────────────────────────────────

            // ── Clean up stale platform folders ──────────────────────────────────────
            // Delete any platform subfolder that is no longer required for a scene so
            // it does not end up in the zip and get imported on the server.
            foreach (var scene in scenes)
            {
                string sceneFolderPath = Path.Combine(OutputFolder, scene.SceneNameFiltered);
                if (!Directory.Exists(sceneFolderPath)) continue;

                var requiredFolderNames = new HashSet<string>(
                    scene.GetRequiredBuildTargets().Select(t => t.ToString()),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (string subfolder in Directory.GetDirectories(sceneFolderPath))
                {
                    string folderName = Path.GetFileName(subfolder);
                    if (!requiredFolderNames.Contains(folderName))
                    {
                        Directory.Delete(subfolder, true);
                        Debug.Log($"[AddressablesManagement] Removed stale platform folder: {subfolder}");
                    }
                }
            }
            // ─────────────────────────────────────────────────────────────────────────

            // Collect all required BuildTargets from the scenes
            var allTargets = new HashSet<BuildTarget>();
            foreach (var scene in scenes)
            {
                foreach (var target in scene.GetRequiredBuildTargets())
                    allTargets.Add(target);
            }

            // Build for each required platform
            foreach (BuildTarget target in allTargets)
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                BuildAddressablesForSelectedPlatform(settings, scenes);
            }

            result.Failure = CheckBuildResult(scenes);

            if (result.Failure == EnvironmentsBuildFailure.None)
            {
                Debug.Log("[AddressablesManagement] Build successful for all platforms.");
                result.ScriptsAssembly = PrepareInterpretedAssemblyBundle();
                ZipBuiltScenes(scenes, result.ScriptsAssembly);

                foreach (SceneConfiguration scene in scenes)
                {
                    result.Scenes.Add(new EnvironmentsBuiltScene
                    {
                        Name = scene.Scene.name,
                        AddressName = scene.SceneNameFiltered,
                        Platforms = scene.GetRequiredBuildTargets().Select(t => t.ToString()).ToList(),
                        ZipPath = ZipPathOf(scene).Replace('\\', '/'),
                    });
                }

                if (result.ScriptsAssembly != null)
                {
                    string archive = Path.Combine(OutputFolder, result.ScriptsAssembly + ".zip");
                    result.ScriptsArchive = File.Exists(archive) ? archive.Replace('\\', '/') : null;
                }
            }
            else
            {
                Debug.LogError("[AddressablesManagement] Build failed. Check errors above.");
            }

            return result;
        }

        private static void BuildAddressablesForSelectedPlatform(AddressableAssetSettings settings, IReadOnlyList<SceneConfiguration> scenes)
        {
            AddressableAssetGroup assetGroup = settings.DefaultGroup;
            foreach (AddressableAssetEntry entry in assetGroup.entries.Where(x => x.IsScene).ToList())
                settings.RemoveAssetEntry(entry.guid);

            foreach (var scene in scenes)
                BuildAddressablesForTargetGroup(settings, scene);
        }

        private static void BuildAddressablesForTargetGroup(AddressableAssetSettings settings, SceneConfiguration configuration)
        {
            AddressableAssetGroup assetGroup = settings.DefaultGroup;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(configuration.Scene, out string guid, out long _);
            AddressableAssetEntry addressableEntry = settings.CreateOrMoveEntry(guid, assetGroup);

            string sceneNameFiltered = configuration.SceneNameFiltered;
            addressableEntry.SetAddress(sceneNameFiltered);

            settings.OverridePlayerVersion = sceneNameFiltered;
            settings.BuiltInBundleCustomNaming = settings.OverridePlayerVersion;
            settings.MonoScriptBundleCustomNaming = settings.OverridePlayerVersion;

            AddressablesBuildScript.BuildAddressables();

            settings.RemoveAssetEntry(guid);
            settings.OverridePlayerVersion = string.Empty;
        }

        private static EnvironmentsBuildFailure CheckBuildResult(IReadOnlyList<SceneConfiguration> scenes)
        {
            foreach (var scene in scenes)
            {
                string sceneFolderPath = Path.Combine(OutputFolder, scene.SceneNameFiltered);
                var requiredTargets = scene.GetRequiredBuildTargets();

                foreach (BuildTarget target in requiredTargets)
                {
                    string subfolder = target.ToString();
                    string subfolderPath = Path.Combine(sceneFolderPath, subfolder);
                    if (!Directory.Exists(subfolderPath))
                    {
                        Debug.LogError($"Subfolder {subfolder} not found in {sceneFolderPath}");
                        return EnvironmentsBuildFailure.FolderMissing;
                    }

                    bool catalogFileExists = Directory.GetFiles(subfolderPath, "*catalog*.json").Any();
                    if (!catalogFileExists)
                    {
                        Debug.LogError($"Catalog file not found in {subfolderPath}");
                        return EnvironmentsBuildFailure.BinaryCatalog;
                    }
                }
            }

            return EnvironmentsBuildFailure.None;
        }

        /// <summary>
        /// Builds this project's interpreted-assembly bundle beside the scene zips, as
        /// <c>ServerData/&lt;AssemblyName&gt;.zip</c>, and returns the name the scenes are compiled
        /// against — null when the project has no interpreted scripts (HybridCLR not installed, or
        /// installed with no script under the hot-update folder).
        ///
        /// Built on every addressables build, not only on a deploy from the window: the scene zips
        /// and the bundle are what someone uploads by hand and imports from the Backoffice, and
        /// the import needs the bundle beside the scenes to register their scripts.
        /// </summary>
        private static string PrepareInterpretedAssemblyBundle()
        {
            Type setupperType = FindHotUpdateSetupperType();
            Type bundleType = FindEditorType("EnvironmentDllBundle");

            if (setupperType == null || bundleType == null)
                return null;

            // Bundles of earlier builds go: a stale one beside the new scene zips is the wrong file
            // to upload, and nothing else would ever remove it. The project prefix keeps this to
            // assemblies this project produced.
            string prefix = setupperType.GetProperty("ProjectAssemblyPrefix", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null) as string;

            if (!string.IsNullOrEmpty(prefix) && Directory.Exists(OutputFolder))
            {
                foreach (string stale in Directory.GetFiles(OutputFolder, prefix + "*.zip"))
                    File.Delete(stale);
            }

            // HybridCLR configured but no script written: no assembly exists, so the scenes declare
            // none and the import clears any link a previous build left on their catalog. The same
            // question the build gate asked, so the two cannot disagree about whether there is code.
            bool hasScripts = setupperType.GetMethod("ProjectHasInterpretedScripts", BindingFlags.Public | BindingFlags.Static)?
                .Invoke(null, null) as bool? ?? true;

            if (!hasScripts)
                return null;

            string zipPath = bundleType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)?
                .Invoke(null, null) as string;

            // The name comes from the file Build wrote, not from a second read of the assembly-name
            // property: that rescans the source on every access, and a script saved in between would
            // let the declaration name one build and the bundle carry another.
            if (!string.IsNullOrEmpty(zipPath))
                return Path.GetFileNameWithoutExtension(zipPath);

            string assemblyName = bundleType.GetProperty("AssemblyName", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null) as string;

            // The scenes still need the assembly, so they still declare it; the import will accept
            // them only where it is already registered.
            Debug.LogWarning($"[AddressablesManagement] The interpreted-assembly bundle could not be built (see above). " +
                             $"The scene zips declare '{assemblyName}', and an import accepts them only where that " +
                             "assembly is already registered.");

            return string.IsNullOrEmpty(assemblyName) ? null : assemblyName;
        }

        /// <summary>
        /// The assembly a built scene declares. <paramref name="declares"/> is false when the
        /// folder carries no declaration — a build made before the window wrote one.
        /// </summary>
        internal static string ReadDeclaredAssembly(string scene, out bool declares)
        {
            declares = false;

            string path = Path.Combine(OutputFolder, scene, EnvironmentDllDeclarationFile);
            if (!File.Exists(path))
                return null;

            try
            {
                EnvironmentDllDeclaration declaration = JsonConvert.DeserializeObject<EnvironmentDllDeclaration>(File.ReadAllText(path));
                declares = declaration != null;
                return string.IsNullOrEmpty(declaration?.assemblyName) ? null : declaration.assemblyName;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesManagement] Unreadable {path}: {ex.Message}. Build the scenes again.");
                return null;
            }
        }

        private static void ZipBuiltScenes(IReadOnlyList<SceneConfiguration> scenes, string assemblyName)
        {
            string declaration = JsonConvert.SerializeObject(new EnvironmentDllDeclaration { assemblyName = assemblyName });

            foreach (string scene in scenes.Select(s => s.SceneNameFiltered))
            {
                string fullBuildPath = Path.Combine(OutputFolder, scene);
                string fullZipPath = fullBuildPath + ".zip";

                if (!Directory.Exists(fullBuildPath))
                    throw new DirectoryNotFoundException($"Build folder not found: {fullBuildPath}");

                File.WriteAllText(Path.Combine(fullBuildPath, EnvironmentDllDeclarationFile), declaration);

                // Remove existing zip with the same name
                if (File.Exists(fullZipPath))
                {
                    File.Delete(fullZipPath);
                    Debug.Log($"[AddressablesManagement] Removed existing zip: {fullZipPath}");
                }

                ZipFile.CreateFromDirectory(fullBuildPath, fullZipPath, System.IO.Compression.CompressionLevel.Optimal, true);
                Debug.Log($"[AddressablesManagement] Created zip: {fullZipPath}");
            }
        }

        #endregion
    }
}
