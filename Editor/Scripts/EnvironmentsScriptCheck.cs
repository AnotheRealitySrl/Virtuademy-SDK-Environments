using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>A component whose script compiles into a <c>*-firstpass</c> assembly, which the Virtuademy app does not have.</summary>
    public sealed class FirstpassComponent
    {
        /// <summary>The published scene whose content reaches <see cref="Asset"/>: the scene itself, or one that uses the prefab.</summary>
        public string SceneName;

        /// <summary>The scene, prefab or asset (a project path) that holds the component.</summary>
        public string Asset;

        /// <summary>The component's GameObject, as <c>Root/Child/Leaf</c>. Null for a ScriptableObject asset.</summary>
        public string GameObjectPath;

        /// <summary>The component's type, with its namespace.</summary>
        public string ComponentType;

        /// <summary>The script's path in the project.</summary>
        public string ScriptPath;

        /// <summary>The assembly Unity compiles the script into, such as <c>Assembly-CSharp-firstpass</c>.</summary>
        public string AssemblyName;

        internal FirstpassComponent ForScene(string sceneName) => new()
        {
            SceneName = sceneName,
            Asset = Asset,
            GameObjectPath = GameObjectPath,
            ComponentType = ComponentType,
            ScriptPath = ScriptPath,
            AssemblyName = AssemblyName,
        };
    }

    /// <summary>
    /// The check <see cref="EnvironmentsBuild.BuildAndZip"/> makes before it builds anything: a component
    /// whose script Unity compiles into <c>Assembly-CSharp-firstpass</c> would arrive in the Virtuademy app
    /// as a missing script.
    /// </summary>
    /// <remarks>
    /// An environment's content is built as Addressables and loaded by the app, and a component binds there
    /// only when the app has a class with the same assembly, namespace and name. Unity compiles the scripts
    /// under <c>Assets/Plugins</c> and <c>Assets/Standard Assets</c> into <c>Assembly-CSharp-firstpass</c>
    /// (their <c>Editor</c> sub-folders into <c>Assembly-CSharp-Editor-firstpass</c>); the app has no such assembly. The plugins it supports sit in <c>Assembly-CSharp</c>, so the same
    /// script moved to a normal folder binds.
    /// <para>
    /// The check reads each published scene's dependency closure, which holds its prefabs, the assets they
    /// use and every script they reference, and looks at the components only of the assets that reference
    /// a script of a firstpass assembly. A published scene whose closure has none is not opened.
    /// </para>
    /// </remarks>
    public static class EnvironmentsScriptCheck
    {
        private static readonly string[] firstpass_assemblies =
        {
            "Assembly-CSharp-firstpass",
            "Assembly-UnityScript-firstpass",
            "Assembly-CSharp-Editor-firstpass",
        };

        private const string log_prefix = "[AddressablesManagement] ";

        /// <summary>How many components <see cref="Describe"/> lists by default; the dialog of the window shows the text as it is.</summary>
        public const int DefaultListed = 15;

        /// <summary>The components of the scenes' content whose script compiles into a firstpass assembly. Opens no scene that has none.</summary>
        /// <param name="scenes">The scenes the build publishes.</param>
        public static List<FirstpassComponent> FindFirstpassComponents(IReadOnlyList<SceneConfiguration> scenes)
        {
            List<FirstpassComponent> found = new();
            Dictionary<string, string> assemblyByScript = new();
            Dictionary<string, List<FirstpassComponent>> scannedAssets = new();

            foreach (SceneConfiguration configuration in scenes)
            {
                string scenePath = AssetDatabase.GetAssetPath(configuration.Scene);
                string[] closure = AssetDatabase.GetDependencies(scenePath, true);

                HashSet<string> firstpassScripts = new(closure.Where(path => IsScript(path) && IsFirstpass(AssemblyOf(path, assemblyByScript))));
                if (firstpassScripts.Count == 0)
                    continue;

                foreach (string path in closure)
                {
                    if (IsScript(path))
                        continue;

                    // Only the assets that name a firstpass script themselves: the others hold none of its components.
                    if (!AssetDatabase.GetDependencies(path, false).Any(firstpassScripts.Contains))
                        continue;

                    if (!scannedAssets.TryGetValue(path, out List<FirstpassComponent> components))
                        scannedAssets[path] = components = ScanAsset(path, assemblyByScript);

                    found.AddRange(components.Select(c => c.ForScene(configuration.Scene.name)));
                }
            }

            return found;
        }

        /// <summary>
        /// What is wrong and how to fix it, with the components listed. At most <paramref name="maxListed"/>
        /// are named, the others counted.
        /// </summary>
        public static string Describe(IReadOnlyList<FirstpassComponent> components, int maxListed = DefaultListed)
        {
            StringBuilder text = new();

            text.Append($"{components.Count} component(s) of the environment use a script that Unity compiles into Assembly-CSharp-firstpass, ");
            text.Append("because it sits under Assets/Plugins or Assets/Standard Assets. The Virtuademy app has no such assembly, ");
            text.Append("so these components would be missing scripts in the app. Nothing was built.\n\n");

            foreach (FirstpassComponent c in components.Take(maxListed))
            {
                string where = c.GameObjectPath == null ? c.Asset : $"{c.Asset} > {c.GameObjectPath}";
                string viaScene = c.Asset.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" [scene {c.SceneName}]";
                text.Append($"• {where}{viaScene}: {c.ComponentType} (script {c.ScriptPath}, assembly {c.AssemblyName})\n");
            }

            if (components.Count > maxListed)
                text.Append($"• ... and {components.Count - maxListed} more (the Console lists all of them).\n");

            text.Append("\nTo fix it, move the plugin's runtime scripts out of Assets/Plugins and Assets/Standard Assets to a normal folder, ");
            text.Append("for example the vendor's own folder, such as Assets/Crazy Minnow Studio/... Move the .meta files with them ");
            text.Append("(in Unity's Project window, or on disk) so the GUIDs, and with them the references in your scenes and prefabs, stay. ");
            text.Append("Unity then compiles the scripts into Assembly-CSharp, the assembly in which the app holds the plugins it supports. ");
            text.Append("A plugin the app does not contain stays missing there.");

            return text.ToString();
        }

        #region Scan

        private static List<FirstpassComponent> ScanAsset(string path, Dictionary<string, string> assemblyByScript)
        {
            List<FirstpassComponent> found = new();

            try
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    ScanScene(path, assemblyByScript, found);
                else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    ScanPrefab(path, assemblyByScript, found);
                else
                    ScanAssetObjects(path, assemblyByScript, found);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{log_prefix}Could not check {path} for scripts of a firstpass assembly: {ex.Message}");
            }

            return found;
        }

        private static void ScanScene(string path, Dictionary<string, string> assemblyByScript, List<FirstpassComponent> found)
        {
            // A scene the editor has open is read as it is; any other is opened beside it and closed again.
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;

            if (opened)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    ScanHierarchy(root, path, assemblyByScript, found);
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ScanPrefab(string path, Dictionary<string, string> assemblyByScript, List<FirstpassComponent> found)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null)
                ScanHierarchy(root, path, assemblyByScript, found);
        }

        private static void ScanHierarchy(GameObject root, string asset, Dictionary<string, string> assemblyByScript, List<FirstpassComponent> found)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // A missing script is not this check's business; a component that comes from a prefab is
                // reported once, with the prefab, not again with every scene or prefab that uses it.
                if (behaviour == null || PrefabUtility.GetCorrespondingObjectFromSource(behaviour) != null)
                    continue;

                Add(found, asset, GameObjectPath(behaviour.transform), behaviour, MonoScript.FromMonoBehaviour(behaviour), assemblyByScript);
            }
        }

        // ScriptableObjects (and the other assets with a script) that a prefab or scene uses.
        private static void ScanAssetObjects(string path, Dictionary<string, string> assemblyByScript, List<FirstpassComponent> found)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is ScriptableObject scriptable)
                    Add(found, path, null, scriptable, MonoScript.FromScriptableObject(scriptable), assemblyByScript);
            }
        }

        private static void Add(List<FirstpassComponent> found, string asset, string gameObjectPath, UnityEngine.Object component,
            MonoScript script, Dictionary<string, string> assemblyByScript)
        {
            if (script == null)
                return;

            string scriptPath = AssetDatabase.GetAssetPath(script);
            string assembly = AssemblyOf(scriptPath, assemblyByScript);
            if (!IsFirstpass(assembly))
                return;

            found.Add(new FirstpassComponent
            {
                Asset = asset,
                GameObjectPath = gameObjectPath,
                ComponentType = component.GetType().FullName,
                ScriptPath = scriptPath,
                AssemblyName = assembly,
            });
        }

        private static string GameObjectPath(Transform transform)
        {
            StringBuilder path = new(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path.Insert(0, parent.name + "/");

            return path.ToString();
        }

        #endregion

        #region Assemblies

        private static bool IsScript(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        private static bool IsFirstpass(string assemblyName) => assemblyName != null && firstpass_assemblies.Contains(assemblyName);

        /// <summary>The assembly Unity compiles a script into, without the <c>.dll</c>; null when Unity does not say.</summary>
        private static string AssemblyOf(string scriptPath, Dictionary<string, string> cache)
        {
            if (cache.TryGetValue(scriptPath, out string assembly))
                return assembly;

            string name = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
            if (!string.IsNullOrEmpty(name) && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            return cache[scriptPath] = string.IsNullOrEmpty(name) ? null : name;
        }

        #endregion
    }
}
