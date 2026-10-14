#if VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS
using Virtuademy.Environments.ScriptingApi.Placeholders;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
#endif
#if VIRTUADEMY_ENVIRONMENTS_TASKS && VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS
using Virtuademy.SDK.Environments.Tasks;
#endif
#if VIRTUADEMY_ENVIRONMENTS_VISUAL_SCRIPTING && VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS
using Virtuademy.SDK.Environments.VisualScripting;
#endif
using UnityEditor;

using Virtuademy.Environments.ScriptingApi.Interaction;


using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.Installer.Editor
{
    public static class BreakingChangesSolver2025_4
    {
        [MenuItem("Virtuademy/Update routines/v2025.3 -> v2025.4")]
        public static void SolveBreakingChanges()
        {
#if VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS
            ReplaceInteractablePlaceholder();
#endif
        }
#if VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS
        private const string progressTitle = "Update v2025.3 -> v2025.4";

        private class OperationCanceledByUserException : Exception { }

        private static void ReplaceInteractablePlaceholder()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string activeScenePath = "" + EditorSceneManager.GetActiveScene().path;

            // Only the assets that actually use the obsolete placeholder (or a detector pointing to it) are processed.
            // Prefabs are sorted so that nested prefabs and variant bases come before the prefabs that contain them:
            // this way each source prefab is already migrated when its instances are processed, and it is loaded and saved only once.
            HashSet<string> trackedScripts = GetTrackedScriptPaths();
            List<string> prefabPaths = FindAssetsUsingScripts("t:Prefab", ".prefab", trackedScripts)
                .Where(IsWritable)
                .ToList();
            HashSet<string> prefabSet = new(prefabPaths);
            prefabPaths = prefabPaths
                .OrderBy(path => AssetDatabase.GetDependencies(path, true).Count(prefabSet.Contains))
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList();
            List<string> scenePaths = FindAssetsUsingScripts("t:Scene", ".unity", trackedScripts)
                .Where(path => !path.StartsWith("Packages/"))
                .ToList();

            List<string> failures = new();
            bool canceled = false;
            int total = (prefabPaths.Count + scenePaths.Count) * 2;
            int step = 0;

            try
            {
                // Replace the obsolete placeholder with the new ones and fix the detectors referencing it.
                // The obsolete component is removed only afterwards, so that references from other assets are still valid here.
                foreach (string prefabPath in prefabPaths)
                {
                    ShowProgress("Migrating prefab", prefabPath, step++, total);
                    ProcessPrefab(prefabPath, prefab =>
                    {
                        bool replaced = ReplaceComponentsInPrefab(prefab);
                        bool fixedDetectors = FixDetector(prefab);
                        return replaced || fixedDetectors;
                    }, failures);
                }

                foreach (string scenePath in scenePaths)
                {
                    ShowProgress("Migrating scene", scenePath, step++, total);
                    ProcessScene(scenePath, () =>
                    {
                        bool replaced = ReplaceComponentsInScene();
                        bool fixedDetectors = FixDetectorsInScene();
                        return replaced || fixedDetectors;
                    }, failures);
                }

                foreach (string prefabPath in prefabPaths)
                {
                    ShowProgress("Removing old components from prefab", prefabPath, step++, total);
                    ProcessPrefab(prefabPath, DestroyOldComponentInPrefab, failures);
                }

                foreach (string scenePath in scenePaths)
                {
                    ShowProgress("Removing old components from scene", scenePath, step++, total);
                    ProcessScene(scenePath, () =>
                    {
                        if (DestroyOldComponentInScene())
                        {
                            Debug.LogWarning("Removed old components in scene: " + scenePath);
                            return true;
                        }
                        return false;
                    }, failures);
                }
            }
            catch (OperationCanceledByUserException)
            {
                canceled = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!string.IsNullOrEmpty(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            if (canceled)
            {
                EditorUtility.DisplayDialog("Canceled", "Update canceled: the project has been only partially migrated. Run the update routine again to complete it.", "OK");
            }
            else if (failures.Count > 0)
            {
                EditorUtility.DisplayDialog("Warning", $"Update completed with {failures.Count} error(s):\n\n{string.Join("\n", failures)}\n\nSee the console for details.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Success", "Update completed!", "OK");
            }
        }

        private static void ShowProgress(string operation, string path, int step, int total)
        {
            if (EditorUtility.DisplayCancelableProgressBar(progressTitle, $"{operation}: {path}", total == 0 ? 1f : (float)step / total))
            {
                throw new OperationCanceledByUserException();
            }
        }

        private static void ProcessPrefab(string prefabPath, Func<GameObject, bool> process, List<string> failures)
        {
            GameObject prefab = null;
            try
            {
                prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                if (process(prefab))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
            }
            catch (Exception e)
            {
                failures.Add(prefabPath);
                Debug.LogError($"Error while updating prefab {prefabPath}: {e}");
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
        }

        private static void ProcessScene(string scenePath, Func<bool> process, List<string> failures)
        {
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (process())
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
            }
            catch (Exception e)
            {
                failures.Add(scenePath);
                Debug.LogError($"Error while updating scene {scenePath}: {e}");
            }
        }

        private static HashSet<string> GetTrackedScriptPaths()
        {
            List<Type> types = new() { typeof(InteractablePlaceholderObsolete) };
#if VIRTUADEMY_ENVIRONMENTS_TASKS
            types.Add(typeof(ManipulableGrabberDetector));
            types.Add(typeof(VisualScriptingInteractableHoverDetector));
#endif
            HashSet<string> scriptPaths = new();
            foreach (Type type in types)
            {
                foreach (string guid in AssetDatabase.FindAssets($"t:MonoScript {type.Name}"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null && script.GetClass() == type)
                    {
                        scriptPaths.Add(path);
                    }
                }
            }
            return scriptPaths;
        }

        private static IEnumerable<string> FindAssetsUsingScripts(string filter, string extension, HashSet<string> scriptPaths)
        {
            return AssetDatabase.FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Where(path => AssetDatabase.GetDependencies(path, true).Any(scriptPaths.Contains));
        }

        private static bool IsWritable(string assetPath)
        {
            if (!assetPath.StartsWith("Packages/"))
            {
                return true;
            }
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return packageInfo == null
                || packageInfo.source == UnityEditor.PackageManager.PackageSource.Embedded
                || packageInfo.source == UnityEditor.PackageManager.PackageSource.Local;
        }

        // Components coming from a source prefab are handled when the source prefab itself is processed.
        private static bool IsInheritedFromSourcePrefab(Component component)
        {
            return PrefabUtility.IsPartOfPrefabInstance(component) && !PrefabUtility.IsAddedComponentOverride(component);
        }

        private static bool FixDetector(GameObject prefab)
        {
#if VIRTUADEMY_ENVIRONMENTS_TASKS
            var grabs = prefab.GetComponentsInChildren<ManipulableGrabberDetector>(true);
            var hovers = prefab.GetComponentsInChildren<VisualScriptingInteractableHoverDetector>(true);
            bool modified = false;
            foreach (var grab in grabs)
            {
                if (grab.interactablePlaceholder != null)
                {
                    var manipulablePlaceholder = grab.interactablePlaceholder.GetComponentInChildren<ManipulablePlaceholder>(true);
                    if (grab.manipulablePlaceholder != manipulablePlaceholder)
                    {
                        grab.manipulablePlaceholder = manipulablePlaceholder;
                        EditorUtility.SetDirty(grab);
                        EditorUtility.SetDirty(grab.gameObject);
                        modified = true;
                    }
                }
            }
            foreach (var hover in hovers)
            {
                if (hover.interactablePlaceholder != null)
                {
                    var visualScriptingPlaceholder = hover.interactablePlaceholder.GetComponentInChildren<VisualScriptingInteractablePlaceholder>(true);
                    if (hover.visualscriptingPlaceholder != visualScriptingPlaceholder)
                    {
                        hover.visualscriptingPlaceholder = visualScriptingPlaceholder;
                        EditorUtility.SetDirty(hover);
                        EditorUtility.SetDirty(hover.gameObject);
                        modified = true;
                    }
                }
            }
            return modified;
#else
            return false;
#endif
        }

        private static bool FixDetectorsInScene()
        {
#if VIRTUADEMY_ENVIRONMENTS_TASKS
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool change = false;
            foreach (GameObject gameObject in gameObjects)
            {
                if (FixDetector(gameObject))
                {
                    change = true;
                    Debug.Log("Fixed detectors in " + gameObject.name + " in scene " + gameObject.scene.name, gameObject);
                }
            }
            return change;
#else
            return false;
#endif
        }


        private static bool ReplaceComponentsInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                RemoveMissingScripts(gameObject);

                var isModified = ReplaceComponentRecursive(gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        private static bool ReplaceComponentsInPrefab(GameObject prefab)
        {
            RemoveMissingScripts(prefab);

            var replaced = ReplaceComponentRecursive(prefab);

            return replaced;
        }

        // Source prefabs are not loaded from here: they are processed before the assets that contain them (see ReplaceInteractablePlaceholder),
        // so the new components already exist on the instance and only the instance values are copied over.
        private static bool ReplaceComponentRecursive(GameObject gameObject)
        {
            InteractablePlaceholderObsolete[] interactables = gameObject.GetComponents<InteractablePlaceholderObsolete>();
            bool modified = false;
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    ReplaceInteractablePlaceholder(interactable);
                    modified = true;
                }
            }
            foreach (Transform child in gameObject.transform)
            {
                var isModified = ReplaceComponentRecursive(child.gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        private static void ReplaceInteractablePlaceholder(InteractablePlaceholderObsolete interactable)
        {
            Debug.Log($"Replacing component in {interactable.gameObject.name} in scene {interactable.gameObject.scene.name}", interactable.gameObject);

            UnityEditor.Undo.RecordObject(interactable.gameObject, "Replace Component");

            InteractablePlaceholder interactionPlaceholder = interactable.gameObject.GetOrAddComponent<InteractablePlaceholder>();

            interactionPlaceholder.LockHoverDuringInteraction = interactable.LockHoverDuringInteraction;
            interactionPlaceholder.InteractionColliders = interactable.InteractionColliders;
            interactionPlaceholder.IsNetworked = interactable.IsNetworked;

            EditorUtility.SetDirty(interactionPlaceholder);

            if (interactable.InteractionModes.HasFlag(Virtuademy.Environments.ScriptingApi.Interaction.IInteractable.EInteractableType.ContextualMenuInteractable))
            {
                ContextualMenuPlaceholder contextualMenuPlaceholder = interactable.gameObject.GetOrAddComponent<ContextualMenuPlaceholder>();
                contextualMenuPlaceholder.ContextualMenuOptions = interactable.ContextualMenuOptions;
                EditorUtility.SetDirty(contextualMenuPlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<ContextualMenuPlaceholder>(out var cmp) && !IsInheritedFromSourcePrefab(cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }

            if (interactable.InteractionModes.HasFlag(Virtuademy.Environments.ScriptingApi.Interaction.IInteractable.EInteractableType.Manipulable))
            {
                ManipulablePlaceholder manipulablePlaceholder = interactable.gameObject.GetOrAddComponent<ManipulablePlaceholder>();
                manipulablePlaceholder.ManipulationMode = interactable.ManipulationMode;
                manipulablePlaceholder.DynamicAttach = interactable.DynamicAttach;
                manipulablePlaceholder.AdjustRotationOnRelease = interactable.AdjustRotationOnRelease;
                manipulablePlaceholder.MouseLookAtCamera = interactable.MouseLookAtCamera;
                manipulablePlaceholder.RealignAxisX = interactable.RealignAxisX;
                manipulablePlaceholder.RealignAxisY = interactable.RealignAxisY;
                manipulablePlaceholder.RealignAxisZ = interactable.RealignAxisZ;
                manipulablePlaceholder.RealignDurationTimeInSeconds = interactable.RealignDurationTimeInSeconds;
                manipulablePlaceholder.VrInteraction = interactable.VRInteraction;
                manipulablePlaceholder.AttachTransform = interactable.AttachTransform;

                EditorUtility.SetDirty(manipulablePlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<ManipulablePlaceholder>(out var cmp) && !IsInheritedFromSourcePrefab(cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }
#if VIRTUADEMY_ENVIRONMENTS_VISUAL_SCRIPTING
            if (interactable.InteractionModes.HasFlag(Virtuademy.Environments.ScriptingApi.Interaction.IInteractable.EInteractableType.VisualScriptingInteractable))
            {
                VisualScriptingInteractablePlaceholder vsPlaceholder = interactable.gameObject.GetOrAddComponent<VisualScriptingInteractablePlaceholder>();
                vsPlaceholder.DesktopAllowedStates = interactable.DesktopAllowedStates;
                vsPlaceholder.VRAllowedStates = interactable.VRAllowedStates;
                vsPlaceholder.InteractionsScriptMachine = interactable.InteractionsScriptMachine;
                vsPlaceholder.VrVisualScriptingInteraction = interactable.VrVisualScriptingInteraction;

                EditorUtility.SetDirty(vsPlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<VisualScriptingInteractablePlaceholder>(out var cmp) && !IsInheritedFromSourcePrefab(cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }
#endif
            EditorUtility.SetDirty(interactionPlaceholder.gameObject);

        }

        private static bool DestroyOldComponentInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                var isModified = DestroyComponentRecursive(gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        private static bool DestroyOldComponentInPrefab(GameObject prefab)
        {
            var replaced = DestroyComponentRecursive(prefab);
            return replaced;
        }

        private static bool DestroyComponentRecursive(GameObject gameObject)
        {
            InteractablePlaceholderObsolete[] interactables = gameObject.GetComponents<InteractablePlaceholderObsolete>();
            bool modified = false;
            foreach (var interactable in interactables)
            {
                if (interactable == null)
                {
                    continue;
                }
                // Still here only if its source prefab could not be updated (e.g. it belongs to a read-only package).
                if (IsInheritedFromSourcePrefab(interactable))
                {
                    Debug.LogWarning($"Obsolete component on {gameObject.name} comes from a prefab that could not be updated, it has not been removed", gameObject);
                    continue;
                }
                UnityEngine.Object.DestroyImmediate(interactable, true);
                EditorUtility.SetDirty(gameObject);
                modified = true;
            }
            foreach (Transform child in gameObject.transform)
            {
                var isModified = DestroyComponentRecursive(child.gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        /// <summary>
        /// Removes all "Missing Script" components from a GameObject and its children.
        /// This method must be called from an Editor context (e.g., a custom Editor window or menu item).
        /// </summary>
        /// <param name="targetGameObject">The GameObject to clean.</param>

        public static void RemoveMissingScripts(GameObject targetGameObject)
        {
            if (targetGameObject == null)
            {
                Debug.LogWarning("Target GameObject is null. Cannot remove missing scripts.");
                return;
            }

            foreach (var component in targetGameObject.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(component.gameObject);

                if (missingCount > 0)
                {
                    Debug.Log($"Removed {missingCount} missing scripts from {component.name} in {targetGameObject.name}", component);
                }
            }

        }

#endif
    }

}
