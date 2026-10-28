using Virtuademy.SDK.Core.ApplicationManagement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    [CreateAssetMenu(fileName = "AddressablesSceneList", menuName = "Virtuademy/SDK-Environments/AddressablesSceneList")]
    public class SceneListScriptableObject : ScriptableObject
    {
        [Serializable]
        public class SceneConfiguration
        {
            private const string alphanumeric_lowercase_string_pattern_negated = @"[^a-z0-9]";

            [SerializeField] private SceneAsset scene;
            [SerializeField] private bool includeInBuild = true;
            // A scene the list has not seen yet is built for every platform; a platform the creator turns off is saved
            // with the scene's entry, so it stays off. Entries saved before 2026.7 keep what they have: an entry with VR
            // and WebGL cannot tell the old default from a choice.
            [SerializeField] private ESupportedPlatform supportedPlatforms = ESupportedPlatform.VR | ESupportedPlatform.WebGL | ESupportedPlatform.Mobile;

            public SceneConfiguration() { }

            /// <summary>The entry the registry creates for a scene it has not seen yet: not in the build until someone ticks it.</summary>
            public SceneConfiguration(SceneAsset scene)
            {
                this.scene = scene;
                includeInBuild = false;
            }

            public SceneAsset Scene { get => scene; set => scene = value; }
            public bool IncludeInBuild { get => includeInBuild; set => includeInBuild = value; }
            public ESupportedPlatform SupportedPlatforms { get => supportedPlatforms; set => supportedPlatforms = value; }

            /// <summary>
            /// The scene's name as its folder, zip and environment file name carry it: lower-cased with
            /// the invariant culture (so a Turkish editor does not turn "I" into "ı", which the filter
            /// would then drop) and reduced to a-z and 0-9, like <c>PublishedEnvironmentsIndex.Key</c>.
            /// Empty when the scene has no letter or digit in its name, or no scene is assigned;
            /// <see cref="EnvironmentsBuild.BuildAndZip"/> refuses such scenes.
            /// </summary>
            public string SceneNameFiltered => scene == null
                ? string.Empty
                : Regex.Replace(scene.name.ToLowerInvariant(), alphanumeric_lowercase_string_pattern_negated, string.Empty);

            public HashSet<BuildTarget> GetRequiredBuildTargets()
            {
                var targets = new HashSet<BuildTarget> { BuildTarget.StandaloneWindows64 };

                if (supportedPlatforms.HasFlag(ESupportedPlatform.VR) || supportedPlatforms.HasFlag(ESupportedPlatform.Mobile))
                    targets.Add(BuildTarget.Android);

                if (supportedPlatforms.HasFlag(ESupportedPlatform.WebGL))
                    targets.Add(BuildTarget.WebGL);

                if (supportedPlatforms.HasFlag(ESupportedPlatform.Mobile))
                    targets.Add(BuildTarget.iOS);

                return targets;
            }

            /// <summary>
            /// Returns the subset of required BuildTargets whose Unity build module is NOT installed.
            /// Uses BuildPipeline.IsBuildTargetSupported — always empty for StandaloneWindows64
            /// because that module is bundled with the editor.
            /// </summary>
            public List<BuildTarget> GetMissingBuildTargets()
            {
                var missing = new List<BuildTarget>();
                foreach (BuildTarget target in GetRequiredBuildTargets())
                {
                    BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                    if (!BuildPipeline.IsBuildTargetSupported(group, target))
                        missing.Add(target);
                }
                return missing;
            }
        }

        [SerializeField] private List<SceneConfiguration> sceneConfigurations;

        [Tooltip("Folders the creator excluded: their scenes, and their subfolders', are not listed. Paths relative to the project, one folder per entry.")]
        [SerializeField] private List<string> excludedFolders = new();

        // 0 until the folders the list used to exclude by default have been taken out of excludedFolders (2026.7.0).
        [SerializeField, HideInInspector] private int exclusionsVersion;

        /// <summary>
        /// The folders the list excluded on its own up to 2026.6, which every list created then still carries. Since 2026.7.0
        /// nothing is excluded unless the creator excludes it: <see cref="SyncWithProject"/> takes these out of a list once.
        /// </summary>
        public static readonly string[] DefaultExcludedFolders =
        {
            "Assets/Samples", "Assets/Plugins", "Assets/_ThirdParty", "Assets/ThirdParty", "Assets/Third Party",
            "Assets/StreamingAssets", "Assets/TextMesh Pro"
        };

        public List<SceneConfiguration> SceneConfigurations => sceneConfigurations;

        /// <summary>The folders whose scenes are left out of the list, as stored (paths relative to the project).</summary>
        public IReadOnlyList<string> ExcludedFolders => excludedFolders ??= new List<string>();

        /// <summary>Leaves <paramref name="folder"/>, and its subfolders, out of the list. False when it already was.</summary>
        public bool ExcludeFolder(string folder)
        {
            string normalized = NormalizeFolder(folder);
            excludedFolders ??= new List<string>();
            if (string.IsNullOrEmpty(normalized) || excludedFolders.Any(f => string.Equals(NormalizeFolder(f), normalized, StringComparison.OrdinalIgnoreCase)))
                return false;

            excludedFolders.Add(normalized);
            excludedFolders.Sort(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        /// <summary>Lists <paramref name="folder"/>'s scenes again. False when it was not excluded.</summary>
        public bool IncludeFolder(string folder)
        {
            string normalized = NormalizeFolder(folder);
            return excludedFolders != null && excludedFolders.RemoveAll(f => string.Equals(NormalizeFolder(f), normalized, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        private static string NormalizeFolder(string folder) => folder?.Trim().Replace('\\', '/').TrimEnd('/');

        /// <summary>The folder the list's asset is created in when the project has none.</summary>
        public const string DefaultFolder = "Assets/Virtuademy/Editor/Settings";

        /// <summary>The file name of the asset created in <see cref="DefaultFolder"/>.</summary>
        public const string DefaultFileName = "AddressablesSceneList.asset";

        /// <summary>
        /// The project's scene list, or null when it has none yet. Creates and changes nothing: the
        /// list is only created by <see cref="LoadOrCreate"/>, which the Publish Environments
        /// window calls when it opens.
        /// </summary>
        public static SceneListScriptableObject Find()
        {
            string guid = AssetDatabase.FindAssets("t:" + nameof(SceneListScriptableObject)).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<SceneListScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>
        /// <see cref="Find"/>, and when the project has no list, a new one at <see cref="DefaultFolder"/>.
        /// It is not synchronised with the project's scenes here: <see cref="SyncWithProject"/> does that.
        /// </summary>
        public static SceneListScriptableObject LoadOrCreate()
        {
            SceneListScriptableObject existing = Find();
            if (existing != null)
                return existing;

            string current = "Assets";
            foreach (string folder in DefaultFolder.Split('/').Skip(1))
            {
                string next = current + "/" + folder;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, folder);
                current = next;
            }

            SceneListScriptableObject created = CreateInstance<SceneListScriptableObject>();
            AssetDatabase.CreateAsset(created, $"{DefaultFolder}/{DefaultFileName}");
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// Makes the list mirror the scenes of the project — every <c>.unity</c> under <c>Assets/</c> and in the packages,
        /// except Unity's own packages and the folders the creator excluded — so nothing gets published because it was
        /// added by hand, and nothing is forgotten because it was not. Scenes that appeared are added <b>not included in
        /// the build</b>; entries whose scene is gone are dropped; duplicates collapse to the first; the order is by folder,
        /// then by scene name. The settings of the scenes already listed (include in build, platforms) are kept: an entry
        /// holds the <see cref="SceneAsset"/> reference, which follows renames and moves. Returns true when the asset
        /// changed and should be saved.
        /// </summary>
        public bool SyncWithProject()
        {
            sceneConfigurations ??= new List<SceneConfiguration>();
            excludedFolders ??= new List<string>();

            // Up to 2026.6 the list excluded some folders on its own; since 2026.7.0 only the creator excludes.
            bool migrated = false;
            if (exclusionsVersion < 1)
            {
                excludedFolders.RemoveAll(f => DefaultExcludedFolders.Contains(NormalizeFolder(f), StringComparer.OrdinalIgnoreCase));
                exclusionsVersion = 1;
                migrated = true;
            }

            HashSet<SceneAsset> present = new();
            foreach (string guid in AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets", "Packages" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsExcluded(path)) continue;
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (scene != null) present.Add(scene);
            }

            int before = sceneConfigurations.Count;
            HashSet<SceneAsset> seen = new();
            sceneConfigurations.RemoveAll(c => c.Scene == null || !present.Contains(c.Scene) || !seen.Add(c.Scene));
            foreach (SceneAsset scene in present.Where(sc => !seen.Contains(sc)))
            {
                sceneConfigurations.Add(new SceneConfiguration(scene));
            }

            List<SceneConfiguration> ordered = sceneConfigurations
                .OrderBy(c => System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(c.Scene))?.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Scene.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            bool changed = migrated || before != sceneConfigurations.Count || !ordered.SequenceEqual(sceneConfigurations);
            sceneConfigurations.Clear();
            sceneConfigurations.AddRange(ordered);
            return changed;
        }

        /// <summary>
        /// A scene the list leaves out: under one of <see cref="excludedFolders"/>, which only the creator adds to, or in one
        /// of Unity's own packages (<c>com.unity.*</c>), whose scenes are scene templates and test scenes, never content.
        /// Scenes of every other package are listed, unticked: some are meant as examples to build.
        /// </summary>
        private bool IsExcluded(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return true;
            string normalized = assetPath.Replace('\\', '/');
            if (normalized.StartsWith("Packages/com.unity.", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string excluded in excludedFolders ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(excluded)) continue;
                string prefix = NormalizeFolder(excluded) + "/";
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

    }
}
