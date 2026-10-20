using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Installer.Editor
{
    /// <summary>
    /// Refuses to save a scene, prefab or asset whose Visual Scripting data still carries the names the
    /// v2026.5 -> v2026.6 update routine renames, until the routine has rewritten it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Between updating the packages and applying the routine, the project holds files written under
    /// the old type names and an editor that only knows the new ones. Opening such a file is harmless:
    /// Visual Scripting swaps every unit it cannot resolve for <c>MissingType</c> in memory, and the
    /// file on disk is untouched. Saving it is not. The swap renumbers the graph's JSON ids, and once
    /// that is written out the graph never deserializes again ("Object definition has not been
    /// encountered for object with id=N") — the rename that follows fixes the names and cannot fix
    /// the ids. Reproduced on 2026-10-09 with a single save between the package update and the
    /// routine; the same files, left unsaved, came through the routine intact.
    /// </para>
    /// <para>
    /// So every save goes through here: Ctrl+S, the save prompts, Prefab Mode and
    /// <c>PrefabUtility.SaveAsPrefabAsset</c> (Unity logs "Prefab not saved due to OnWillSaveAssets
    /// callback"), and other tools' calls to save. A file is refused only while its <c>_json</c> values still name something
    /// the routine renames (<see cref="VirtuademyRenameMigrator.HasUnmigratedGraphData"/>), which is
    /// also why the routine's own re-save goes through: by then it has rewritten the file. Everything
    /// else saves as usual.
    /// </para>
    /// <para>
    /// A file the creator excluded from the rename stays refused for as long as its graphs keep the
    /// old names. That is deliberate: in this editor those graphs can only be saved broken.
    /// </para>
    /// <para>
    /// The guard retires itself. Once the routine has finished and no file with old graph data is left,
    /// it writes a marker, <see cref="MarkerPath"/>, and from then on every save goes through untouched.
    /// The marker is in <c>ProjectSettings/</c>, which is versioned: whoever pulls the migrated files
    /// pulls it too, a fresh clone keeps it (<c>Library/</c> would not), and checking out a branch that
    /// predates the routine takes it away again. A scan that finds old graph data
    /// (<see cref="VirtuademyRenameRoutine.ScanProject"/>) removes it, so running the routine again
    /// puts the guard back. Until the marker exists, a save costs one read of the saved file at most,
    /// and nothing for a file that has not changed since it was last checked
    /// (<see cref="VirtuademyRenameMigrator.HasUnmigratedGraphData"/>).
    /// </para>
    /// </remarks>
    public class VirtuademyUpdateSaveGuard : AssetModificationProcessor
    {
        private const string Title = "Update v2026.5 -> v2026.6";

        /// <summary>Written when the routine has finished and the project holds no old graph data.</summary>
        internal const string MarkerPath = "ProjectSettings/VirtuademyUpdateRoutine-v2026.6.txt";

        /// <summary>True once the routine has completed in this project: the guard has nothing left to protect.</summary>
        internal static bool IsRetired => File.Exists(MarkerPath);

        /// <summary>
        /// Retires the guard when no scene, prefab or asset under <c>Assets/</c> still carries old graph data.
        /// Called when the routine has finished (after the re-save, or after the rewrite when there is nothing to re-save).
        /// </summary>
        internal static void RetireIfMigrated()
        {
            if (IsRetired || VirtuademyRenameRoutine.FilesWithUnmigratedGraphData().Count > 0)
            {
                return;
            }

            try
            {
                File.WriteAllText(MarkerPath,
                    "The v2026.5 -> v2026.6 update routine completed in this project on " +
                    DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) + ".\r\n" +
                    "While this file exists, Virtuademy-SDK-Environments does not check saved scenes, prefabs and assets for the\r\n" +
                    "names the routine renames. Delete it to bring the check back; running the routine again does it when it finds any.\r\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[{Title}] Could not write {MarkerPath}: {e.Message}. The save guard stays on.");
            }
        }

        /// <summary>Puts the guard back: a scan found old graph data in a project that had retired it.</summary>
        internal static void Rearm()
        {
            try
            {
                File.Delete(MarkerPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[{Title}] Could not delete {MarkerPath}: {e.Message}. Delete it by hand to bring the save guard back.");
            }
        }

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (IsRetired)
            {
                return paths;
            }

            List<string> refused = paths.Where(VirtuademyRenameMigrator.HasUnmigratedGraphData).ToList();
            if (refused.Count == 0)
            {
                return paths;
            }

            string message =
                "Not saved, because their Visual Scripting graphs still use the names of the previous SDK " +
                "and saving them now would lose those graphs for good:\n\n  " + string.Join("\n  ", refused) +
                "\n\nRun Virtuademy > Update routines > v2026.5 -> v2026.6 with these files selected, without " +
                "saving them first. Until then, close them without saving.";

            Debug.LogError($"[{Title}] {message.Replace("\n\n", " ").Replace("\n  ", " ")}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(Title, message, "OK");
            }

            return paths.Except(refused).ToArray();
        }
    }
}
