using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.Build;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// Keeps the scripting define symbols of the package's five modules in the PlayerSettings of every build target group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One class for all five, because each of them used to run its own pass on every domain reload and every pass walked
    /// every build target. This one works out the whole set first and writes a group only when its symbols are not already
    /// right, so a reload that has nothing to change writes nothing (and does not ask Unity to recompile).
    /// </para>
    /// <para>
    /// The symbols were <c>REFLECTIS_CREATOR_KIT_WORLDS_*</c> until 2026-09-21; a project updating the package still
    /// carries the old ones in its PlayerSettings, so they are retired here, in the same write that adds the new ones.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class EnvironmentsScriptDefineSymbols
    {
        internal const string AnalyticsSymbol = "VIRTUADEMY_ENVIRONMENTS_ANALYTICS";
        internal const string VisualScriptingSymbol = "VIRTUADEMY_ENVIRONMENTS_VISUAL_SCRIPTING";
        internal const string DialogsSymbol = "VIRTUADEMY_ENVIRONMENTS_DIALOGS";
        internal const string PlaceholdersSymbol = "VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS";
        internal const string TasksSymbol = "VIRTUADEMY_ENVIRONMENTS_TASKS";

        private const string Prefix = "VIRTUADEMY_ENVIRONMENTS_";
        private const string RetiredPrefix = "REFLECTIS_CREATOR_KIT_WORLDS_";

        private static readonly string[] Required = { AnalyticsSymbol, VisualScriptingSymbol, DialogsSymbol, PlaceholdersSymbol, TasksSymbol };

        static EnvironmentsScriptDefineSymbols()
        {
            Apply();
        }

        private static void Apply()
        {
            HashSet<string> retired = new(Required.Select(symbol => symbol.Replace(Prefix, RetiredPrefix)));

            HashSet<BuildTargetGroup> groups = new();
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                if (group != BuildTargetGroup.Unknown)
                {
                    groups.Add(group);
                }
            }

            foreach (BuildTargetGroup group in groups)
            {
                NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                List<string> current = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget)
                    .Split(';').Select(symbol => symbol.Trim()).ToList();

                if (!current.Any(retired.Contains) && Required.All(current.Contains))
                {
                    continue;
                }

                List<string> updated = current.Where(symbol => symbol.Length > 0 && !retired.Contains(symbol)).ToList();
                updated.AddRange(Required.Where(symbol => !updated.Contains(symbol)));

                try
                {
                    PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, string.Join(";", updated));
                }
                catch (Exception e)
                {
                    Debug.Log("Could not set the Virtuademy Environments defines for build target group: " + group +
                              " named build target: " + namedBuildTarget + " error: " + e);
                }
            }
        }
    }
}
