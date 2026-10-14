using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

namespace Virtuademy.BreakingChangeSolvers
{
    /// <summary>
    /// Maps the assembly references of a project's <c>.asmdef</c> and <c>.asmref</c> files from the
    /// assemblies of the 2026.5 packages to the assemblies that replaced them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is not part of the namespace rules.</b> The text rewrite renames namespaces and types, and a
    /// reference in an assembly definition names an <i>assembly</i>. The two used to share one table, and where the
    /// assembly is not named after the namespace the rewrite wrote a name that exists nowhere: the placeholders'
    /// namespace is <c>Virtuademy.Environments.ScriptingApi.Placeholders</c>, their assembly is
    /// <c>Virtuademy.Environments.ScriptingApi</c>, and a project whose own assembly referenced the old placeholders
    /// assembly stopped compiling after the routine for good. So the references of an assembly definition are mapped
    /// here, from the real list of assemblies of the 2026.5 packages (read from the <c>.asmdef</c> files of their
    /// tags: Creator Kit Worlds Core, Placeholders, Visual Scripting, Tasks, Dialogs and Analytics; SDK Core, Dialogs,
    /// Graphs, Tasks, Tenant Configuration and ReflectisApi), and the rest of the file goes through the usual rules.
    /// </para>
    /// <para>
    /// <b>What it never does.</b> It does not write a name that is not an assembly of the project: every successor is
    /// looked up among the <c>.asmdef</c> files of the project and its packages, and a reference whose successors are
    /// none of them is left as it is and reported (<see cref="Finding"/>), so the project fails with the old name
    /// rather than a made-up one. It does not touch a reference to an assembly that exists now, whatever it is
    /// called: <c>Virtuademy.SDK.Core.Editor</c> was an assembly of the 2026.5 framework and is an assembly of the
    /// SDK today. A <c>GUID:</c> reference is left alone while the GUID resolves to an asset (the SDK kept the GUIDs of
    /// the Core, Core Editor, Tasks Editor and Tenant Configuration assemblies); a GUID that no longer resolves is looked up
    /// in the old assemblies' GUIDs and written back as the successor's name.
    /// </para>
    /// <para>
    /// Compiles outside any project assembly, in <c>Virtuademy.SDK.Environments.UpdateRoutines</c>, and reads the
    /// <c>.asmdef</c> files as text, so it works in a project that does not compile.
    /// </para>
    /// </remarks>
    public static class VirtuademyAssemblyReferenceMigrator
    {
        /// <summary>A reference the routine could not map, or mapped only in part.</summary>
        public sealed class Finding
        {
            public string Path;
            public string Reference;
            public string Detail;
        }

        private sealed class Entry
        {
            public string[] Successors;
            public string Partial;
        }

        // The old brand token is split, as in the rename routine: this file must not match its own table.
        private static readonly string OldBrand = "Reflec" + "tis";
        private const string NewBrand = "Virtuademy";

        private const string Environments = "Virtuademy.SDK.Environments";
        private const string EnvironmentsEditor = "Virtuademy.SDK.Environments.Editor";
        private const string ScriptingApi = "Virtuademy.Environments.ScriptingApi";
        private const string AnalyticsApi = "Virtuademy.ScriptingApi";

        private static readonly Dictionary<string, Entry> Table = BuildTable();

        /// <summary>The GUIDs of the assembly definitions of the 2026.5 packages, with the assembly each named
        /// (without the brand, as in <see cref="BuildTable"/>).</summary>
        private static readonly Dictionary<string, string> OldGuids = new(StringComparer.Ordinal)
        {
            { "c8004483caf3c6c4d8fc98a23fc6ae66", "CreatorKit.Worlds.Analytics" },
            { "01470f9b53eba9e4cb6fb8ffb31c0ce9", "CreatorKit.Worlds.CoreEditor" },
            { "e1e4f83f6c2796445b9ea45a4dca9090", "CreatorKit.Worlds.Core" },
            { "26a888660527f674caedb2f468115a19", "CreatorKit.Worlds.Dialogs.Editor" },
            { "2d30924443a0f164ea61cd70834d18d7", "CreatorKit.Worlds.Dialogs" },
            { "55643044a94b09b4caf454bc345e8c8b", "CreatorKit.Worlds.Placeholders.Editor" },
            { "d144e5d8e92d0bc419736e72debb4e2a", "CreatorKit.Worlds.Placeholders" },
            { "f018e8b2b07bb6c4eaaf49f6cdb90518", "CreatorKit.Worlds.Tasks.Editor" },
            { "213a20fc7dc3c964ea49c0cdd8d72aa5", "CreatorKit.Worlds.Tasks" },
            { "000312ae375444845a7929f91c717577", "CreatorKit.Worlds.VisualScripting.Editor" },
            { "9b30ce4289cbc5c4b93ce43cb6fd887c", "CreatorKit.Worlds.VisualScripting" },
            { "aa13245f3cf702a42b84f0cf5d0b0848", "SDK.Core.Editor" },
            { "ab59f74e483e1ee4595bfefea56493f2", "SDK.Core" },
            { "a50cc4a4ca58205428a6b3b1c6b8808b", "SDK.Core.I2Loc" },
            { "bffeb8d381d69c9418ff9dbd60c3e915", "SDK.Dialogs.Editor" },
            { "bbf1130388534284b8b57b29e3989dae", "SDK.Dialogs" },
            { "82f3f376d72968040afad30b1ef49190", "SDK.Graphs.Editor" },
            { "23c385269eb2e5044a0a2686aeeaf762", "SDK.Graphs" },
            { "408180972fa24c746abaefde90a2575f", "SDK.ReflectisApi" },
            { "40fa99dae01a17147ab87fa6da4d559c", "SDK.Tasks.Editor" },
            { "fe738984b0c28c342b0d3d49a82efa50", "SDK.Tasks" },
            { "2cf13d4ec281ded458a2543b244c1a5a", "SDK.TenantConfiguration.Editor" },
            { "fbebb77141d815e478836eb96f260865", "SDK.TenantConfiguration" },
        };

        /// <summary>The old assembly, under both brands the packages have carried, and what replaced it.</summary>
        private static Dictionary<string, Entry> BuildTable()
        {
            Dictionary<string, Entry> table = new(StringComparer.Ordinal);

            foreach (string brand in new[] { OldBrand, NewBrand })
            {
                void Add(string name, string partial, params string[] successors)
                    => table[brand + "." + name] = new Entry { Successors = successors, Partial = partial };

                // The Creator Kit Worlds packages: one package, Virtuademy-SDK-Environments, since 9.0.0. The
                // placeholders are in the scripting surface's assembly, the rest of the runtime in the main one.
                Add("CreatorKit.Worlds.Core", null, Environments);
                Add("CreatorKit.Worlds.CoreEditor", null, EnvironmentsEditor);
                Add("CreatorKit.Worlds.CoreHybridCLREditor", null, "Virtuademy.SDK.Environments.HybridCLREditor");
                Add("CreatorKit.Worlds.Placeholders", null, ScriptingApi);
                Add("CreatorKit.Worlds.Placeholders.Editor", null, EnvironmentsEditor);
                // Two of its types (SyncedObject, SyncedVariables) moved to the scripting surface.
                Add("CreatorKit.Worlds.VisualScripting", null, Environments, ScriptingApi);
                Add("CreatorKit.Worlds.VisualScripting.Editor", null, EnvironmentsEditor);
                Add("CreatorKit.Worlds.Tasks", null, Environments);
                Add("CreatorKit.Worlds.Tasks.Editor", null, EnvironmentsEditor);
                Add("CreatorKit.Worlds.Dialogs", null, Environments);
                Add("CreatorKit.Worlds.Dialogs.Editor", null, EnvironmentsEditor);
                // The analytics contract is in the main assembly; the statement types went to the contracts package.
                Add("CreatorKit.Worlds.Analytics", null, Environments, AnalyticsApi);

                // The framework: only what moved to SPACS-Utility has a successor in a creator project, because the
                // framework itself (SM, the systems, the UI kit) is not installed there any more.
                const string frameworkLeft = "only the utilities and the Visual Scripting node bases moved (to SPACS.Utility); " +
                                             "the framework's systems have no successor in a creator project";
                Add("SDK.Core", frameworkLeft, "SPACS.Utility", "SPACS.Utility.VisualScripting");
                Add("SDK.Core.Editor", frameworkLeft, "SPACS.Utility.Editor");
                Add("SDK.Core.I2Loc", null, "Virtuademy.SDK.Environments.I2Loc");

                // The engines took the SPACS prefix on 2026-09-24.
                Add("SDK.Dialogs", null, "SPACS.Dialogs");
                Add("SDK.Dialogs.Editor", null, "SPACS.Dialogs.Editor");
                Add("SDK.Graphs", null, "SPACS.Graphs");
                Add("SDK.Graphs.Editor", null, "SPACS.Graphs.Editor");
                Add("SDK.Tasks", null, "SPACS.Tasks");
                Add("SDK.Tasks.Editor", null, "SPACS.Tasks.Editor");

                // The tenant configuration went into the contracts package.
                Add("SDK.TenantConfiguration", null, "Virtuademy.SDK.TenantConfiguration", "Virtuademy.SDK.TenantConfiguration.Wire");
                Add("SDK.TenantConfiguration.Editor", null, "Virtuademy.SDK.Core.Editor", EnvironmentsEditor);

            }

            // The platform API client and its wire types: the SDK Library, which a creator project does not
            // install, and the analytics types, which are in the contracts package. It was named after the old
            // brand, and after the platform once the brand rename had taken the name.
            Entry client = new() { Successors = new[] { "Virtuademy.SDK.Library", AnalyticsApi } };
            table[OldBrand + ".SDK." + OldBrand + "Api"] = client;
            table[NewBrand + ".SDK.PlatformApi"] = client;

            // Names the brand rename and the retirement of the wire assemblies left behind.
            table[NewBrand + ".SDK.PlatformApi.Wire"] = new Entry { Successors = new[] { "Virtuademy.SDK.Library", AnalyticsApi } };
            table[NewBrand + ".SDK.ApiData.Wire"] = new Entry { Successors = new[] { "Virtuademy.SDK.Library", AnalyticsApi } };

            return table;
        }

        #region Project assemblies

        private static HashSet<string> assemblies;

        /// <summary>Forgets the assemblies found: call it when a scan or an apply starts.</summary>
        public static void ResetCache() => assemblies = null;

        private static readonly Regex AssemblyName = new(@"""name""\s*:\s*""(?<name>[^""]+)""", RegexOptions.Compiled);

        /// <summary>The names of the assembly definitions of the project and of its packages.</summary>
        private static HashSet<string> Assemblies()
        {
            if (assemblies != null)
            {
                return assemblies;
            }

            assemblies = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                // Through the AssetDatabase rather than the file: a package fetched by git is not on disk under Packages/.
                string text = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(AssetDatabase.GUIDToAssetPath(guid))?.text;
                Match name = text == null ? Match.Empty : AssemblyName.Match(text);
                if (name.Success)
                {
                    assemblies.Add(name.Groups["name"].Value);
                }
            }

            return assemblies;
        }

        #endregion

        #region Mapping

        /// <summary>
        /// The assemblies <paramref name="reference"/> is to be replaced with, or null when it is to be left as it is.
        /// A reference that cannot be mapped is added to <paramref name="unmapped"/>, one mapped in part to <paramref name="partial"/>.
        /// </summary>
        private static string[] MapReference(string path, string reference, List<Finding> unmapped, List<Finding> partial)
        {
            path = path.Replace('\\', '/');
            string key;
            if (reference.StartsWith("GUID:", StringComparison.Ordinal))
            {
                string guid = reference.Substring("GUID:".Length);
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)) || !OldGuids.TryGetValue(guid, out string suffix))
                {
                    return null;
                }
                key = OldBrand + "." + suffix;
            }
            else
            {
                if (Assemblies().Contains(reference) || !Table.ContainsKey(reference))
                {
                    return null;
                }
                key = reference;
            }

            Entry entry = Table[key];
            string[] existing = entry.Successors.Where(Assemblies().Contains).ToArray();
            if (existing.Length == 0)
            {
                unmapped?.Add(new Finding
                {
                    Path = path,
                    Reference = reference,
                    Detail = $"{key} has no successor in this project (it became {string.Join(" / ", entry.Successors)}, none of which is installed)",
                });
                return null;
            }

            if (entry.Partial != null)
            {
                partial?.Add(new Finding { Path = path, Reference = reference, Detail = $"{key}: {entry.Partial}" });
            }

            return existing;
        }

        private static readonly Regex ReferencesArray = new(@"""references""\s*:\s*\[(?<body>[^\]]*)\]", RegexOptions.Compiled);
        private static readonly Regex ReferenceValue = new(@"""reference""\s*:\s*""(?<value>[^""]*)""", RegexOptions.Compiled);
        private static readonly Regex QuotedString = new(@"""(?<s>(?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        /// <summary>True for the files this class maps the references of.</summary>
        public static bool AppliesTo(string path)
            => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Splits <paramref name="text"/> around the part that names the referenced assemblies (the <c>references</c> array of an
        /// assembly definition, the <c>reference</c> of an assembly definition reference): <paramref name="head"/>,
        /// <paramref name="segment"/> and <paramref name="tail"/> add up to the text. With no such part, everything is the head.
        /// </summary>
        public static void Split(string path, string text, out string head, out string segment, out string tail)
        {
            Match match = path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
                ? ReferenceValue.Match(text)
                : ReferencesArray.Match(text);

            if (!match.Success)
            {
                head = text;
                segment = tail = string.Empty;
                return;
            }

            head = text.Substring(0, match.Index);
            segment = match.Value;
            tail = text.Substring(match.Index + match.Length);
        }

        /// <summary>
        /// The <paramref name="segment"/> of <paramref name="path"/> with its references mapped; <paramref name="changes"/> is how
        /// many it replaced. Idempotent: a mapped reference names an assembly that is not in the table.
        /// </summary>
        public static string MapSegment(string path, string segment, List<Finding> unmapped, List<Finding> partial, out int changes)
        {
            changes = 0;
            if (segment.Length == 0)
            {
                return segment;
            }

            if (path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
            {
                Match single = ReferenceValue.Match(segment);
                string[] mapped = MapReference(path, single.Groups["value"].Value, unmapped, partial);
                if (mapped == null)
                {
                    return segment;
                }

                changes = 1;
                return segment.Substring(0, single.Groups["value"].Index) + mapped[0]
                       + segment.Substring(single.Groups["value"].Index + single.Groups["value"].Length);
            }

            Match array = ReferencesArray.Match(segment);
            Group body = array.Groups["body"];
            List<string> elements = QuotedString.Matches(body.Value).Cast<Match>().Select(m => m.Groups["s"].Value).ToList();

            List<string> result = new();
            foreach (string element in elements)
            {
                string[] mapped = MapReference(path, element, unmapped, partial);
                if (mapped == null)
                {
                    if (!result.Contains(element))
                    {
                        result.Add(element);
                    }
                    continue;
                }

                changes++;
                foreach (string successor in mapped.Where(s => !result.Contains(s)))
                {
                    result.Add(successor);
                }
            }

            if (changes == 0)
            {
                return segment;
            }

            // Written the way it was: one reference to a line with the indentation of the first one, or all on the line.
            string newline = body.Value.Contains("\r\n") ? "\r\n" : "\n";
            string items;
            if (body.Value.Contains("\n"))
            {
                Match firstLine = Regex.Match(body.Value, @"\n(?<indent>[ \t]*)""");
                string indent = firstLine.Success ? firstLine.Groups["indent"].Value : "    ";
                string closeIndent = body.Value.Substring(body.Value.LastIndexOf('\n') + 1);
                items = newline + string.Join("," + newline, result.Select(r => indent + "\"" + r + "\"")) + newline + closeIndent;
            }
            else
            {
                items = string.Join(", ", result.Select(r => "\"" + r + "\""));
            }

            return segment.Substring(0, body.Index - array.Index) + items + "]";
        }

        #endregion
    }
}
