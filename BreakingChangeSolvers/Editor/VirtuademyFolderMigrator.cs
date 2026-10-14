using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.BreakingChangeSolvers
{
    /// <summary>
    /// Gathers what the SDK generates in a project into the single <c>Assets/Virtuademy</c>
    /// folder it is now supposed to have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A project used to accumulate three: <c>Assets/CreatorKit</c>, written by the setup
    /// window; <c>Assets/ReflectisSettings</c>, written at runtime by the tasks video
    /// controller; and <c>Assets/Virtuademy</c>, written by the tenant switch. The names came
    /// from two brands and one product name that no longer exists, and nothing but habit put
    /// them at the root of the project.
    /// </para>
    /// <para>
    /// <b>Nothing breaks if this is never run.</b> Both settings assets are located with
    /// <c>AssetDatabase.FindAssets("t:…")</c>, which searches the whole project, so the code
    /// finds them wherever they sit. What running it changes is the folder list a creator sees,
    /// and it stops the next generate from writing a second copy in the new place while the old
    /// one lingers.
    /// </para>
    /// <para>
    /// Moves go through <see cref="AssetDatabase.MoveAsset"/>, which carries the <c>.meta</c>
    /// and therefore the GUID: every reference to a moved asset survives. A move that Unity
    /// refuses is reported and skipped rather than forced, and the source folder is removed only
    /// once it holds nothing.
    /// </para>
    /// <para>
    /// This has no menu entry of its own: it is the second half of the v2026.5 -> v2026.6 update
    /// routine, driven by <c>VirtuademyRenameMigrator</c>, and runs after the text rewrite so the
    /// paths that pass records are still the ones on disk while it writes.
    /// </para>
    /// </remarks>
    public static class VirtuademyFolderMigrator
    {
        /// <summary>What a <see cref="Consolidate"/> pass did, for the caller to report.</summary>
        public readonly struct Result
        {
            public Result(int moved, int refused, int pruned, IReadOnlyList<string> refusals = null)
            {
                Moved = moved;
                Refused = refused;
                Pruned = pruned;
                this.refusals = refusals;
            }

            private readonly IReadOnlyList<string> refusals;

            public int Moved { get; }
            public int Refused { get; }
            public int Pruned { get; }

            /// <summary>What was left where it was, one entry per asset: the path and why.</summary>
            public IReadOnlyList<string> Refusals => refusals ?? Array.Empty<string>();
        }

        private const string Root = "Assets/Virtuademy";

        /// <summary>Old folder, new folder. Order matters only for readability.</summary>
        private static readonly (string from, string to)[] Moves =
        {
            ("Assets/CreatorKit/Editor/Settings", Root + "/Editor/Settings"),
            ("Assets/CreatorKit/Editor/Scripts", Root + "/Editor/Scripts"),
            ("Assets/ReflectisSettings", Root + "/Settings"),
        };

        /// <summary>
        /// Assets whose file name carried a product name that was dropped. Keyed by the name as
        /// it is after the folder move.
        /// </summary>
        private static readonly (string oldName, string newName)[] Renames =
        {
            ("CreatorKitSetupConfiguration.asset", "SetupConfiguration.asset"),
        };

        /// <summary>Folders to delete afterwards, deepest first, and only when empty.</summary>
        private static readonly string[] Prune =
        {
            "Assets/CreatorKit/Editor/Settings",
            "Assets/CreatorKit/Editor/Scripts",
            "Assets/CreatorKit/Editor",
            "Assets/CreatorKit",
            "Assets/ReflectisSettings",
        };

        /// <summary>
        /// True while the project still carries one of the legacy folders — either with assets
        /// left to move, or empty and waiting to be pruned.
        /// </summary>
        public static bool HasLegacyFolders()
        {
            return Moves.Any(m => AssetDatabase.IsValidFolder(m.from))
                   || Prune.Any(AssetDatabase.IsValidFolder);
        }

        public static Result Consolidate()
        {
            List<string> moved = new();
            List<string> refused = new();

            // The destinations first, outside the editing batch: a folder created inside
            // StartAssetEditing is not in the asset database until StopAssetEditing, so every
            // MoveAsset into it was refused ("Parent directory is not in asset database") and the
            // assets only moved on a second run.
            foreach ((string from, string to) in Moves)
            {
                if (AssetDatabase.IsValidFolder(from))
                {
                    EnsureFolderExists(to);
                }
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach ((string from, string to) in Moves)
                {
                    if (!AssetDatabase.IsValidFolder(from))
                    {
                        continue;
                    }

                    // Direct children only. A nested folder moves as one asset, with everything
                    // under it, so recursing would move the same files twice.
                    foreach (string asset in ChildAssets(from))
                    {
                        string target = $"{to}/{Path.GetFileName(asset)}";

                        foreach ((string oldName, string newName) in Renames)
                        {
                            if (Path.GetFileName(asset) == oldName)
                            {
                                target = $"{to}/{newName}";
                            }
                        }

                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(target) != null)
                        {
                            refused.Add($"{asset} — something already sits at {target}");
                            continue;
                        }

                        string error = AssetDatabase.MoveAsset(asset, target);

                        if (string.IsNullOrEmpty(error))
                        {
                            moved.Add($"{asset} → {target}");
                        }
                        else
                        {
                            refused.Add($"{asset} — {error}");
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            List<string> pruned = new();

            foreach (string folder in Prune)
            {
                if (AssetDatabase.IsValidFolder(folder) && !ChildAssets(folder).Any()
                    && !AssetDatabase.GetSubFolders(folder).Any()
                    && AssetDatabase.DeleteAsset(folder))
                {
                    pruned.Add(folder);
                }
            }

            Report(moved, refused, pruned);

            return new Result(moved.Count, refused.Count, pruned.Count, refused);
        }

        /// <summary>Assets directly inside <paramref name="folder"/>, subfolders included.</summary>
        private static IEnumerable<string> ChildAssets(string folder)
        {
            return AssetDatabase.FindAssets(string.Empty, new[] { folder })
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Distinct()
                                .Where(p => !string.IsNullOrEmpty(p)
                                            && Path.GetDirectoryName(p)?.Replace('\\', '/') == folder);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            string current = string.Empty;

            foreach (string part in folderPath.Split('/'))
            {
                current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";

                if (!AssetDatabase.IsValidFolder(current))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(current)?.Replace('\\', '/'),
                                               Path.GetFileName(current));
                }
            }
        }

        private static void Report(List<string> moved, List<string> refused, List<string> pruned)
        {
            if (moved.Count == 0 && refused.Count == 0 && pruned.Count == 0)
            {
                Debug.Log("[Virtuademy] Nothing to consolidate: no legacy folder in this project.");
                return;
            }

            string message = $"[Virtuademy] Consolidated into {Root}: "
                             + $"{moved.Count} asset(s) moved, {pruned.Count} folder(s) removed."
                             + Environment.NewLine
                             + string.Join(Environment.NewLine, moved.Concat(pruned.Select(p => $"removed {p}")));

            if (refused.Count > 0)
            {
                Debug.LogWarning(message
                                 + Environment.NewLine
                                 + $"{refused.Count} left where they were:"
                                 + Environment.NewLine
                                 + string.Join(Environment.NewLine, refused));
                return;
            }

            Debug.Log(message);
        }
    }
}
