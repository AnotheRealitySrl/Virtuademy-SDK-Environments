using System.Collections.Generic;
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
    /// </remarks>
    public class VirtuademyUpdateSaveGuard : AssetModificationProcessor
    {
        private const string Title = "Update v2026.5 -> v2026.6";

        private static string[] OnWillSaveAssets(string[] paths)
        {
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
