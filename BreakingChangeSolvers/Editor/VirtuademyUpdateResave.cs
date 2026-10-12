using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Virtuademy.BreakingChangeSolvers
{
    /// <summary>
    /// The last step of the v2026.5 -> v2026.6 update routine: re-saves, through Unity, every scene,
    /// prefab and asset the routine rewrote as text — and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rewrite leaves files that load correctly but are not what Unity would write: a POI page
    /// marker whose script changed under it, Visual Scripting graphs whose type names were edited
    /// inside their JSON. Re-serializing them normalizes that data. It used to be a manual step —
    /// open every world scene and save it — and this class does it instead, on the files the
    /// routine actually changed. A scene the routine did not touch is not opened, reimported or
    /// written.
    /// </para>
    /// <para>
    /// <b>When</b> is the whole difficulty. Saving before the renamed types resolve is the one way
    /// to make a loss permanent: Visual Scripting writes the units it could not resolve as
    /// <c>MissingType</c>. So <see cref="Schedule"/> only records the work, by GUID (the folder
    /// consolidation that follows the rewrite moves files, and a GUID survives the move), in a file
    /// under <c>Library/</c> that outlives domain reloads and editor restarts. The work runs:
    /// </para>
    /// <list type="bullet">
    /// <item>after the next domain reload, when the rewrite touched scripts or assembly definitions
    /// (those always recompile, and until they do the old domain is the wrong one to save from);</item>
    /// <item>otherwise in the current domain, once the editor has been idle for a few seconds;</item>
    /// <item>in both cases only once the Package Manager has resolved again, if the routine unpinned
    /// packages in <c>Packages/packages-lock.json</c> — every unpinned package the manifest still
    /// names is back in the lock (<see cref="VirtuademyPackageLock.GitPackagesResolved"/>) — and never
    /// while compilation has failed — it then waits for the next successful compilation.</item>
    /// </list>
    /// <para>
    /// <b>How</b>: every file is reimported first (a Visual Scripting failure can be cached in the
    /// import artifact, and only a forced reimport clears it), then re-serialized one at a time
    /// with <see cref="AssetDatabase.ForceReserializeAssets(IEnumerable{string}, ForceReserializeAssetsOptions)"/>,
    /// which needs no scene to be opened. The original bytes are kept, and a file is put back as it
    /// was when its re-serialization logged an error, gained a <c>MissingType</c>, or lost
    /// Visual Scripting content. Those files are listed for the creator to open and check by hand;
    /// the rest is done.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class VirtuademyUpdateResave
    {
        private const string LogTag = "[Update v2026.5 -> v2026.6]";
        private const string PendingPath = "Library/Virtuademy/update-resave-pending.json";
        private const string MissingTypeToken = "Unity.VisualScripting.MissingType";
        private const string GraphTypeToken = "$type";
        private const double IdleSeconds = 3;
        private const double SlowWaitSeconds = 60;

        [Serializable]
        private class Pending
        {
            public List<string> Guids = new();
            public List<string> ReopenSceneGuids = new();
            public bool WaitForLockFile;

            // The ids the routine unpinned. Empty in a pending file written before they were
            // recorded, which then waits for every git package of ours, as it did.
            public List<string> UnpinnedIds = new();
        }

        /// <summary>True in the domain that ran the rewrite when the rewrite needs a recompile
        /// first: the work then belongs to the next domain, never to this one.</summary>
        private static bool waitingForReload;

        private static bool running;
        private static double idleSince = -1;
        private static double waitingSince;
        private static bool warnedSlow;

        static VirtuademyUpdateResave()
        {
            if (File.Exists(PendingPath))
            {
                StartWaiting();
            }
        }

        public static bool IsPending => File.Exists(PendingPath);

        /// <summary>True when the pending re-save is waiting for Unity to recompile, so it cannot be
        /// run by hand either.</summary>
        public static bool IsWaitingForReload => waitingForReload;

        /// <summary>
        /// Records the files to re-save and when to do it. Call it after the rewrite and after
        /// <see cref="AssetDatabase.Refresh()"/>, but before anything moves the files.
        /// </summary>
        /// <param name="paths">The scenes, prefabs and assets the rewrite changed.</param>
        /// <param name="reopenScenes">Scenes to open again once the re-save is done.</param>
        /// <param name="unpinned">The packages whose manifest entry the routine rewrote: wait until UPM has resolved them again.</param>
        /// <param name="waitForReload">The rewrite changed code: wait for the next domain.</param>
        public static void Schedule(IEnumerable<string> paths, IEnumerable<string> reopenScenes,
                                    IReadOnlyCollection<string> unpinned, bool waitForReload)
        {
            Pending pending = Load() ?? new Pending();

            pending.Guids = pending.Guids.Concat(ToGuids(paths)).Distinct().ToList();
            pending.ReopenSceneGuids = pending.ReopenSceneGuids.Count > 0
                ? pending.ReopenSceneGuids
                : ToGuids(reopenScenes).ToList();
            pending.WaitForLockFile |= unpinned.Count > 0;
            pending.UnpinnedIds = pending.UnpinnedIds.Concat(unpinned).Distinct().ToList();

            if (pending.Guids.Count == 0 && pending.ReopenSceneGuids.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
            File.WriteAllText(PendingPath, JsonUtility.ToJson(pending, true));

            if (waitForReload)
            {
                waitingForReload = true;
                StopWaiting();
            }
            else if (!waitingForReload)
            {
                StartWaiting();
            }
        }

        #region Waiting

        private static void StartWaiting()
        {
            idleSince = -1;
            waitingSince = EditorApplication.timeSinceStartup;
            warnedSlow = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void StopWaiting() => EditorApplication.update -= Tick;

        private static void Tick()
        {
            Pending pending = Load();
            if (pending == null || waitingForReload)
            {
                StopWaiting();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            // Also covers a pending file written by the previous version of the routine, which deleted
            // the lock: a missing lock is not resolved yet.
            bool lockReady = !pending.WaitForLockFile || VirtuademyPackageLock.GitPackagesResolved(pending.UnpinnedIds);
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating
                        || EditorApplication.isPlayingOrWillChangePlaymode;

            if (busy || !lockReady)
            {
                idleSince = -1;
                if (!warnedSlow && now - waitingSince > SlowWaitSeconds)
                {
                    warnedSlow = true;
                    Debug.LogWarning($"{LogTag} The re-save of the migrated files is still waiting for " +
                                     (lockReady ? "Unity to finish compiling and importing." : "the Package Manager to resolve the packages again.") +
                                     " It runs by itself once that is done.");
                }
                return;
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                StopWaiting();
                Debug.LogWarning($"{LogTag} The re-save of the migrated files is postponed: the project has compile errors. " +
                                 "Nothing has been saved. Fix the errors; the re-save runs after the next successful compilation.");
                return;
            }

            // Idle for a moment, not for one frame: a package resolve or an import can start right
            // after another one ends.
            if (idleSince < 0)
            {
                idleSince = now;
                return;
            }
            if (now - idleSince < IdleSeconds)
            {
                return;
            }

            StopWaiting();
            Run();
        }

        #endregion

        #region Run

        /// <summary>Runs the pending re-save now. Does nothing while it must wait for a recompile.</summary>
        public static void Run()
        {
            Pending pending = Load();
            if (running || waitingForReload || pending == null)
            {
                return;
            }

            running = true;

            int resaved = 0, unchanged = 0, missing = 0;
            List<(string Path, string Reason)> restored = new();
            List<string> skipped = new();

            try
            {
                List<string> paths = new();
                foreach (string guid in pending.Guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        missing++;
                        continue;
                    }
                    paths.Add(path);
                }

                // A scene open with unsaved changes stays out: re-serializing the file under it would
                // either be undone by its next save or throw away what has not been saved.
                HashSet<string> dirtyOpenScenes = new(
                    Enumerable.Range(0, SceneManager.sceneCount)
                        .Select(SceneManager.GetSceneAt)
                        .Where(s => s.isDirty && !string.IsNullOrEmpty(s.path))
                        .Select(s => s.path),
                    StringComparer.OrdinalIgnoreCase);

                skipped.AddRange(paths.Where(dirtyOpenScenes.Contains));
                paths.RemoveAll(dirtyOpenScenes.Contains);

                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string path in paths)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar($"{LogTag} Re-saving migrated files", path, (float)i / paths.Count);

                    switch (Resave(path, out string reason))
                    {
                        case Outcome.Resaved:
                            resaved++;
                            break;
                        case Outcome.Unchanged:
                            unchanged++;
                            break;
                        default:
                            restored.Add((path, reason));
                            break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                File.Delete(PendingPath);
                running = false;
            }

            ReopenScenes(pending.ReopenSceneGuids);
            Report(resaved, unchanged, missing, restored, skipped);
        }

        private enum Outcome { Resaved, Unchanged, Restored }

        private static Outcome Resave(string path, out string reason)
        {
            reason = null;
            byte[] before = File.ReadAllBytes(path);
            string beforeText = Decode(before);

            List<string> errors = new();
            void Capture(string message, string stackTrace, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    errors.Add(message);
                }
            }

            Application.logMessageReceived += Capture;
            try
            {
                AssetDatabase.ForceReserializeAssets(new[] { path }, ForceReserializeAssetsOptions.ReserializeAssets);
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            string afterText = Decode(File.ReadAllBytes(path));

            if (errors.Count > 0)
            {
                reason = "errors were logged while it was re-serialized: " + FirstLine(errors[0]);
            }
            else if (Count(afterText, MissingTypeToken) > Count(beforeText, MissingTypeToken))
            {
                reason = "a Visual Scripting unit did not resolve and would have been saved as MissingType";
            }
            else if (Count(afterText, GraphTypeToken) < Count(beforeText, GraphTypeToken))
            {
                reason = "a Visual Scripting graph came back smaller than it was";
            }

            if (reason != null)
            {
                File.WriteAllBytes(path, before);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return Outcome.Restored;
            }

            return string.Equals(beforeText, afterText, StringComparison.Ordinal) ? Outcome.Unchanged : Outcome.Resaved;
        }

        /// <summary>Opens the scenes the routine closed before rewriting them, unless the creator
        /// has opened something else in the meantime.</summary>
        private static void ReopenScenes(List<string> guids)
        {
            List<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .ToList();

            Scene active = SceneManager.GetActiveScene();
            bool untouched = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty;
            if (paths.Count == 0 || !untouched || Application.isBatchMode)
            {
                return;
            }

            EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            foreach (string path in paths.Skip(1))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
        }

        private static void Report(int resaved, int unchanged, int missing,
                                   List<(string Path, string Reason)> restored, List<string> skipped)
        {
            foreach ((string path, string reason) in restored)
            {
                Debug.LogWarning($"{LogTag} Not re-saved, left as the rewrite wrote it: {path} — {reason}. " +
                                 "Right-click it > Reimport, open it, check the Visual Scripting units are real nodes, then save it.",
                                 AssetDatabase.LoadMainAssetAtPath(path));
            }
            foreach (string path in skipped)
            {
                Debug.LogWarning($"{LogTag} Not re-saved: {path} is open with unsaved changes. Save it yourself.",
                                 AssetDatabase.LoadMainAssetAtPath(path));
            }

            string summary = $"Re-saved: {resaved}\nAlready as Unity writes them: {unchanged}";
            if (missing > 0)
            {
                summary += $"\nNo longer in the project: {missing}";
            }
            if (restored.Count + skipped.Count > 0)
            {
                summary += $"\nTo check and save by hand: {restored.Count + skipped.Count} (listed in the Console)";
            }

            Debug.Log($"{LogTag} Migrated files re-saved. {summary.Replace('\n', ' ')}");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Update v2026.5 -> v2026.6",
                    "The files changed by the update have been re-saved.\n\n" + summary, "OK");
            }
        }

        #endregion

        #region Helpers

        private static IEnumerable<string> ToGuids(IEnumerable<string> paths)
            => (paths ?? Enumerable.Empty<string>())
                .Select(p => AssetDatabase.AssetPathToGUID(p.Replace('\\', '/')))
                .Where(g => !string.IsNullOrEmpty(g));

        private static Pending Load()
        {
            if (!File.Exists(PendingPath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<Pending>(File.ReadAllText(PendingPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Could not read {PendingPath}, discarding it: {e.Message}");
                File.Delete(PendingPath);
                return null;
            }
        }

        private static string Decode(byte[] bytes) => new UTF8Encoding(false).GetString(bytes);

        private static int Count(string text, string token)
        {
            int count = 0;
            for (int index = 0; (index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length)
            {
                count++;
            }
            return count;
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOfAny(new[] { '\r', '\n' });
            return newline < 0 ? message : message.Substring(0, newline);
        }

        #endregion
    }
}
