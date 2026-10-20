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
            [SerializeField] private ESupportedPlatform supportedPlatforms = ESupportedPlatform.VR | ESupportedPlatform.WebGL;

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

        [Tooltip("Folders whose scenes are not the project's own and never appear in the list: imported package samples, third-party assets, plugins. Paths relative to the project, one folder per entry.")]
        [SerializeField] private List<string> excludedFolders = new(DefaultExcludedFolders);

        /// <summary>Where package samples and third-party assets usually land; the starting value of <see cref="excludedFolders"/>.</summary>
        public static readonly string[] DefaultExcludedFolders =
        {
            "Assets/Samples", "Assets/Plugins", "Assets/_ThirdParty", "Assets/ThirdParty", "Assets/Third Party",
            "Assets/StreamingAssets", "Assets/TextMesh Pro"
        };

        public List<SceneConfiguration> SceneConfigurations => sceneConfigurations;

        /// <summary>The folder the list's asset is created in when the project has none.</summary>
        public const string DefaultFolder = "Assets/Virtuademy/Editor/Settings";

        /// <summary>The file name of the asset created in <see cref="DefaultFolder"/>.</summary>
        public const string DefaultFileName = "AddressablesSceneList.asset";

        /// <summary>
        /// The project's scene list, or null when it has none yet. Creates and changes nothing: the
        /// list is only created by <see cref="LoadOrCreate"/>, which the Addressables management
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
        /// Makes the list mirror the scenes of the project — every <c>.unity</c> under <c>Assets/</c>, packages
        /// excluded — so nothing gets published because it was added by hand, and nothing is forgotten because
        /// it was not. Scenes that appeared are added <b>not included in the build</b>; entries whose scene is
        /// gone are dropped; duplicates collapse to the first; the order is by scene name. The settings of
        /// the scenes already listed (include in build, platforms) are kept: an entry holds the
        /// <see cref="SceneAsset"/> reference, which follows renames and moves. Returns true when the asset
        /// changed and should be saved.
        /// </summary>
        public bool SyncWithProject()
        {
            sceneConfigurations ??= new List<SceneConfiguration>();

            HashSet<SceneAsset> present = new();
            foreach (string guid in AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" }))
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

            List<SceneConfiguration> ordered = sceneConfigurations.OrderBy(c => c.Scene.name, StringComparer.OrdinalIgnoreCase).ToList();
            bool changed = before != sceneConfigurations.Count || !ordered.SequenceEqual(sceneConfigurations);
            sceneConfigurations.Clear();
            sceneConfigurations.AddRange(ordered);
            return changed;
        }

        /// <summary>
        /// A scene that is not the project's own: under one of <see cref="excludedFolders"/> (package
        /// samples, third-party assets, plugins), inside a package Unity resolves (an embedded or local
        /// package), or in a folder that carries a <c>package.json</c> — a package dropped under
        /// <c>Assets/</c> by hand. These are demos, not environments to publish.
        /// </summary>
        private bool IsExcluded(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return true;
            string normalized = assetPath.Replace('\\', '/');
            foreach (string excluded in excludedFolders ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(excluded)) continue;
                string prefix = excluded.Trim().Replace('\\', '/').TrimEnd('/') + "/";
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath(normalized) != null) return true;

            string folder = System.IO.Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(folder) && folder.Length > "Assets".Length)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(folder, "package.json"))) return true;
                folder = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }
            return false;
        }

    }
}
