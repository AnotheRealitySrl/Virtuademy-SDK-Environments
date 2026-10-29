using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

using Virtuademy.SDK.Environments.Installer.Editor;

namespace Virtuademy.BreakingChangeSolvers
{
    /// <summary>
    /// Runs the update routines of the SDK (the menu items under <b>Virtuademy &gt; Update routines</b>) from the
    /// command line, for a machine with nobody at the windows: a project moved to a new SDK version by
    /// <c>SetupCli</c> with its <c>update</c> step, CI, an assistant. The command line of the update routines is to the
    /// windows what <c>EnvironmentsCli</c> is to the Publish Environments window and <c>SetupCli</c> to the setup
    /// window: the same scan and the same apply, driven by arguments.
    /// <code>
    /// Unity -batchmode -nographics -projectPath &lt;project&gt; -logFile -
    ///       -executeMethod Virtuademy.BreakingChangeSolvers.UpdateRoutinesCli.Run
    ///       -vdRoutine (list | 2026.6 | 2026.5) [options]
    /// </code>
    /// <para>
    /// <b>Routines</b> (<c>-vdRoutine</c>; the menu name is accepted too, whitespace ignored). They are listed in the
    /// order to run them when a project needs both:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>list</c>: the routines this project has, their menu names and whether each finds
    /// something to change (a dry scan). Changes nothing; no options.</description></item>
    /// <item><description><c>2026.6</c>, <b>v2026.5 -&gt; v2026.6</b> (<see cref="VirtuademyRenameMigrator"/>): the old
    /// names in the project's files, the POI page markers, the assets that share a GUID with a package, the legacy
    /// folders, with the window's defaults: every file the scan finds, every option ticked.
    /// Options: <c>-vdDryRun</c>, <c>-vdExclude</c>, <c>-vdSkip</c>.</description></item>
    /// <item><description><c>2026.5</c>, <b>v2026.4.x -&gt; v2026.5.0</b>
    /// (<c>CheckPlatformUnitMobilePortUpdater</c>): connects the Mobile output of the Platform: Switch nodes to the
    /// WebGL destination, in every graph asset, prefab and scene whose Mobile output is not connected. Options:
    /// <c>-vdDryRun</c>, <c>-vdExclude</c>.</description></item>
    /// </list>
    /// <para>
    /// <b>Options.</b> <c>-vdDryRun</c> scans and reports and changes nothing. <c>-vdExclude a,b</c> leaves files out,
    /// as unticking them in the window does: asset paths, or a folder when the path ends with <c>/</c>; for
    /// <c>2026.6</c> it concerns the files of the first step (the text rewrite), as in the window. <c>-vdSkip</c>
    /// (<c>2026.6</c> only) turns steps off, as unticking their option does: <c>unpin</c> (release the git packages
    /// in the lock file), <c>pages</c> (POI page markers), <c>guids</c> (GUID collisions), <c>folders</c> (gather
    /// <c>Assets/CreatorKit</c> and <c>Assets/ReflectisSettings</c> into <c>Assets/Virtuademy</c>).
    /// <c>-vdTimeoutMinutes</c> (60 by default) ends a run that waits for the editor for too long. Arguments are strict:
    /// any <c>-vd...</c> argument (with a <c>-</c>, <c>--</c> or <c>/</c> prefix, in any case) that is not exactly one of
    /// the routine's options is refused with <see cref="ExitBadArguments"/>, as is an option given twice and a value that
    /// no option claims (<c>-vdExclude a b</c>), so that a typo cannot run the routine with a default the caller did not
    /// choose. Unity's own arguments are not touched.
    /// </para>
    /// <para>
    /// <b>One launch is one round.</b> <c>2026.6</c> cannot finish in one editor session: it rewrites scripts, assembly
    /// definitions and the package manifest, so Unity recompiles and the editor reloads its scripts, and only after that
    /// can the files it changed be re-saved (<see cref="VirtuademyUpdateResave"/>, which would otherwise write graphs
    /// that cannot be read). So a launch that changes the project exits with <see cref="ExitRelaunch"/> and the caller
    /// launches again, with the same arguments, until the code is <see cref="ExitDone"/>: the second launch waits for the
    /// re-save, reports what it did and what is left, and finds nothing more to change. A launch that finds nothing to
    /// change exits with <see cref="ExitDone"/> at once. <c>2026.5</c> and a dry run finish in their launch.
    /// </para>
    /// <para>
    /// <b>Nothing is saved before the routine.</b> Between the package update and <c>2026.6</c>, saving a scene, a
    /// prefab or an asset writes its Visual Scripting graphs out unreadable (<see cref="VirtuademyUpdateSaveGuard"/>).
    /// This class saves nothing itself: <c>2026.6</c> rewrites text, closes the open scenes without saving them, and the
    /// re-save runs after the recompile. <c>2026.5</c> saves the graphs it changes, so it refuses to start
    /// (<see cref="ExitRenameFirst"/>) while <c>2026.6</c> still has graph data to migrate. <c>list</c> and the dry runs
    /// save nothing.
    /// </para>
    /// <para>
    /// The method exits the editor itself, so <c>-quit</c> is not needed and must not be used: a launch that waits for
    /// the re-save would be cut short. Every launch ends with a <c>[UpdateRoutinesCli] REPORT</c> line, one JSON object,
    /// followed by <c>[UpdateRoutinesCli] EXIT &lt;code&gt;</c>.
    /// </para>
    /// <para>
    /// <b>It runs in a project that does not compile.</b> That is the state the update leaves a project in when its own
    /// scripts or assembly definitions name the old assemblies, and the one the routines exist to repair. The routines
    /// (this class, the windows, the migrators, the re-save and the save guard) are an assembly of their own,
    /// <c>Virtuademy.SDK.Environments.UpdateRoutines</c>, that references nothing but Unity: a compile error in the project's
    /// assemblies cannot stop it from being built and loaded. The one thing it needs that can be missing is the SDK's editor
    /// assembly, for <c>2026.5</c> (see <see cref="ExitRoutineUnavailable"/>).
    /// </para>
    /// </summary>
    public static class UpdateRoutinesCli
    {
        /// <summary>Nothing left to do: the routine found nothing, or did what it found and a rescan finds nothing more.</summary>
        public const int ExitDone = 0;

        /// <summary>An unexpected exception: the log has the stack trace.</summary>
        public const int ExitUnexpected = 1;

        /// <summary>The routine changed the project and Unity has to recompile or reload before the next step: launch again with the same arguments.</summary>
        public const int ExitRelaunch = 2;

        /// <summary>A dry run (or <c>-vdRoutine</c> checked without changing) found something to change.</summary>
        public const int ExitChangesPending = 3;

        public const int ExitBadArguments = 10;

        /// <summary>The routine's code is not loaded: the SDK's editor assembly did not compile.</summary>
        public const int ExitRoutineUnavailable = 11;

        /// <summary>The project's scripts do not compile, so the re-save of the changed files cannot run.</summary>
        public const int ExitCompileErrors = 12;

        /// <summary>The routine finished with problems: files it could not change, files the re-save left for a manual check, or work that is still left. The report names them.</summary>
        public const int ExitProblems = 13;

        /// <summary>The run did not finish within <c>-vdTimeoutMinutes</c>.</summary>
        public const int ExitTimeout = 14;

        /// <summary><c>2026.5</c> saves graphs, and <c>2026.6</c> still has Visual Scripting data to migrate: run it first.</summary>
        public const int ExitRenameFirst = 15;

        private const string log_prefix = "[UpdateRoutinesCli] ";
        private const int default_timeout_minutes = 60;
        private const int max_listed = 200;

        private const string rename_id = "2026.6";
        private const string mobile_id = "2026.5";
        private const string rename_menu = "Virtuademy/Update routines/v2026.5 -> v2026.6";
        private const string mobile_menu = "Virtuademy/Update routines/v2026.4.x -> v2026.5.0";
        private const string mobile_type = "Virtuademy.SDK.Environments.VisualScripting.Editor.CheckPlatformUnitMobilePortRoutine";

        private static readonly string[] skippable = { "unpin", "pages", "guids", "folders" };

        private static readonly string[] flag_options = { "-vdDryRun" };
        private static readonly string[] value_options = { "-vdRoutine", "-vdTimeoutMinutes", "-vdExclude", "-vdSkip" };

        // Unity's own options that take a value: the token after one is Unity's, not a stray value of ours.
        private static readonly string[] unity_value_options = { "-projectPath", "-logFile", "-executeMethod", "-buildTarget", "-activeBuildProfile" };

        // What an option of ours looks like, in any prefix and case: anything matching it has to be one exactly.
        private static readonly Regex vd_like = new("^[-/]+vd", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, object> report = new();
        private static string routine;
        private static bool finished;
        private static DateTime deadlineUtc;

        #region Entry

        /// <summary>The <c>-executeMethod</c> entry point. It does not return: it exits the editor.</summary>
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();

            int minutes = default_timeout_minutes;
            string timeout = GetArgument(args, "-vdTimeoutMinutes");
            if (timeout != null && int.TryParse(timeout, out int parsed) && parsed > 0)
            {
                minutes = parsed;
            }
            deadlineUtc = DateTime.UtcNow.AddMinutes(minutes);

            // A reload nobody planned for (the Package Manager finishing a resolve, say) ends this editor's
            // scripts and, with them, this run: say so and ask for another launch instead of hanging.
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;

            try
            {
                if (!TryParseArguments(args, out Options options, out string error))
                {
                    Debug.LogError(log_prefix + error);
                    report["error"] = error;
                    Finish(ExitBadArguments);
                    return;
                }

                report["unityVersion"] = Application.unityVersion;

                switch (options.Routine)
                {
                    case "list": ListRoutines(); break;
                    case rename_id: RunRename(options); break;
                    default: RunMobile(options); break;
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private static void OnBeforeReload()
        {
            if (finished)
            {
                return;
            }

            report["interrupted"] = "The editor reloaded its scripts before the routine could finish.";
            Finish(ExitRelaunch);
        }

        private static void Fail(Exception ex)
        {
            Debug.LogError(log_prefix + "Unexpected failure: " + ex);
            report["error"] = ex.Message;
            Finish(ExitUnexpected);
        }

        private static void Finish(int code)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;

            report["routine"] = routine;
            report["exitCode"] = code;
            report["exit"] = Describe(code);

            try
            {
                Debug.Log(log_prefix + "REPORT " + ToJson(report));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(log_prefix + "The report could not be produced: " + ex.Message);
            }

            Debug.Log($"{log_prefix}EXIT {code} ({Describe(code)})");
            EditorApplication.Exit(code);
        }

        private static string Describe(int code) => code switch
        {
            ExitDone => "done",
            ExitUnexpected => "unexpected failure",
            ExitRelaunch => "launch again to continue",
            ExitChangesPending => "there is something to change",
            ExitBadArguments => "bad arguments",
            ExitRoutineUnavailable => "the routine's code is not loaded",
            ExitCompileErrors => "the project has compile errors",
            ExitProblems => "finished with problems",
            ExitTimeout => "timeout",
            ExitRenameFirst => "run routine 2026.6 first",
            _ => "unknown",
        };

        #endregion

        #region Arguments

        private sealed class Options
        {
            public string Routine;
            public bool DryRun;
            public List<string> Exclude = new();
            public HashSet<string> Skip = new();
        }

        private static string GetArgument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            return args[index + 1];
        }

        private static string Squash(string value) => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

        /// <summary>
        /// Reads every <c>-vd</c> argument of the command line, strictly: one that is not exactly a known option
        /// (<c>--vdDryRun</c>, <c>/vdDryRun</c>, <c>-vddryrun</c> are not <c>-vdDryRun</c>), one given twice, an option with
        /// no value, and a token that no option claims after an option (<c>-vdExclude a b</c>) are all refused. Unity's
        /// own arguments (and the value of the ones that take one) are left alone.
        /// </summary>
        private static bool TryScanArguments(string[] args, out Dictionary<string, string> values, out HashSet<string> flags, out string error)
        {
            values = new Dictionary<string, string>();
            flags = new HashSet<string>();
            error = null;

            // Index 0 is the editor's own path.
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                if (unity_value_options.Contains(arg, StringComparer.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }

                if (!vd_like.IsMatch(arg))
                {
                    continue;
                }

                bool isFlag = flag_options.Contains(arg);
                bool isValue = value_options.Contains(arg);
                if (!isFlag && !isValue)
                {
                    string near = flag_options.Concat(value_options).FirstOrDefault(o =>
                        string.Equals(o, "-" + arg.TrimStart('-', '/'), StringComparison.OrdinalIgnoreCase));
                    error = $"'{arg}' is not an option of this tool." +
                            (near != null ? $" Did you mean {near}? Options are exact, with a single leading dash." : " Options start with a single dash, like -vdRoutine.");
                    return false;
                }

                if (flags.Contains(arg) || values.ContainsKey(arg))
                {
                    error = $"{arg} is given twice.";
                    return false;
                }

                if (isFlag)
                {
                    flags.Add(arg);
                }
                else
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal) || vd_like.IsMatch(args[i + 1]))
                    {
                        error = $"{arg} needs a value.";
                        return false;
                    }

                    values[arg] = args[++i];
                }

                // The one place a stray value can hide: right behind an option and its value. Whatever Unity's
                // options carry comes after an option of theirs, which starts with a dash.
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) && !vd_like.IsMatch(args[i + 1]))
                {
                    error = $"'{args[i + 1]}' follows {arg}{(isFlag ? "" : " and its value")} and belongs to no option " +
                            "(a list takes commas, as in -vdExclude a,b).";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseArguments(string[] args, out Options options, out string error)
        {
            options = new Options();

            if (!TryScanArguments(args, out Dictionary<string, string> values, out HashSet<string> flags, out error))
            {
                return false;
            }

            string Value(string name) => values.TryGetValue(name, out string v) ? v : null;

            string named = Value("-vdRoutine");
            if (string.IsNullOrEmpty(named))
            {
                error = "-vdRoutine is missing. Routines: list, " + rename_id + " (" + rename_menu + "), " + mobile_id + " (" + mobile_menu + ").";
                return false;
            }

            string squashed = Squash(named);
            if (squashed == "list")
            {
                options.Routine = "list";
            }
            else if (squashed == rename_id || squashed == Squash(rename_menu.Substring(rename_menu.LastIndexOf('/') + 1)))
            {
                options.Routine = rename_id;
            }
            else if (squashed == mobile_id || squashed == Squash(mobile_menu.Substring(mobile_menu.LastIndexOf('/') + 1)))
            {
                options.Routine = mobile_id;
            }
            else
            {
                error = $"Unknown routine '{named}'. Routines: list, {rename_id} ({rename_menu}), {mobile_id} ({mobile_menu}).";
                return false;
            }

            routine = options.Routine;

            string[] allowed = options.Routine switch
            {
                "list" => new string[0],
                rename_id => new[] { "-vdDryRun", "-vdExclude", "-vdSkip" },
                _ => new[] { "-vdDryRun", "-vdExclude" },
            };

            // Every -vd argument has to be one this routine reads: a typo must not run it with a default
            // the caller did not choose.
            foreach (string arg in flags.Concat(values.Keys))
            {
                if (arg == "-vdRoutine" || arg == "-vdTimeoutMinutes")
                {
                    continue;
                }

                if (!allowed.Contains(arg))
                {
                    error = $"{arg} is not an option of '{options.Routine}'." +
                            (allowed.Length > 0 ? $" Options: {string.Join(", ", allowed)}." : " It takes no options.");
                    return false;
                }
            }

            if (values.TryGetValue("-vdTimeoutMinutes", out string timeout) && !(int.TryParse(timeout, out int minutes) && minutes > 0))
            {
                error = $"-vdTimeoutMinutes takes a positive number of minutes: '{timeout}' is not one.";
                return false;
            }

            options.DryRun = flags.Contains("-vdDryRun");

            string exclude = Value("-vdExclude");
            if (exclude != null)
            {
                options.Exclude = exclude.Split(',').Select(x => x.Trim().Replace('\\', '/')).Where(x => x.Length > 0).ToList();
            }

            string skip = Value("-vdSkip");
            if (skip != null)
            {
                foreach (string part in skip.Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0))
                {
                    if (!skippable.Contains(part))
                    {
                        error = $"-vdSkip takes {string.Join(", ", skippable)}: '{part}' is not one.";
                        return false;
                    }
                    options.Skip.Add(part);
                }
            }

            return true;
        }

        private static bool IsExcluded(string path, List<string> exclude)
            => exclude.Any(e => e.EndsWith("/", StringComparison.Ordinal)
                ? path.StartsWith(e, StringComparison.OrdinalIgnoreCase)
                : string.Equals(path, e, StringComparison.OrdinalIgnoreCase));

        #endregion

        #region list

        private static void ListRoutines()
        {
            List<object> routines = new();

            VirtuademyRenameRoutine.ScanResult rename = VirtuademyRenameRoutine.ScanProject();
            Dictionary<string, object> renameEntry = new()
            {
                { "id", rename_id },
                { "menu", rename_menu },
                { "pending", HasWork(rename, new Options()) },
            };
            foreach (KeyValuePair<string, object> pair in DescribeRename(rename, new Options()))
            {
                renameEntry[pair.Key] = pair.Value;
            }
            routines.Add(renameEntry);

            Dictionary<string, object> mobileEntry = new() { { "id", mobile_id }, { "menu", mobile_menu } };
            List<string> blockers = VirtuademyRenameRoutine.FilesWithUnmigratedGraphData();
            if (!TryInvoke("Scan", new object[0], out Dictionary<string, object> mobile, out string unavailable))
            {
                mobileEntry["pending"] = null;
                mobileEntry["unavailable"] = unavailable;
            }
            else
            {
                mobileEntry["pending"] = blockers.Count > 0 ? null : (object)(Convert.ToInt32(mobile["notConnected"]) > 0);
                foreach (KeyValuePair<string, object> pair in mobile.Where(p => p.Key != "occurrences"))
                {
                    mobileEntry[pair.Key] = pair.Value;
                }
                if (blockers.Count > 0)
                {
                    mobileEntry["blockedBy"] = rename_id;
                    mobileEntry["blockedByFiles"] = Paths(blockers);
                }
            }
            routines.Add(mobileEntry);

            report["routines"] = routines;
            report["resavePending"] = VirtuademyUpdateResave.IsPending;
            Debug.Log(log_prefix + "list: " + string.Join(", ", routines.Cast<Dictionary<string, object>>()
                .Select(r => $"{r["id"]} pending={r["pending"] ?? "unknown"}")));
            Finish(ExitDone);
        }

        #endregion

        #region 2026.6

        private static void RunRename(Options options)
        {
            if (options.DryRun)
            {
                RenameDryRun(options);
                return;
            }

            // Left by the launch that applied the routine: the changed files are re-saved by the editor once it
            // has recompiled and settled. This launch is the one that waits for that and reports it.
            if (VirtuademyUpdateResave.IsPending)
            {
                report["resavePending"] = true;
                WaitForResave(() =>
                {
                    RecordResave();
                    RenameApply(options);
                });
                return;
            }

            RenameApply(options);
        }

        private static bool HasWork(VirtuademyRenameRoutine.ScanResult scan, Options options)
            => scan.Files.Any(f => f.Selected)
               || (scan.Pages.Count > 0 && !options.Skip.Contains("pages"))
               || (scan.Guids.Count > 0 && !options.Skip.Contains("guids"))
               || (scan.HasLegacyFolders && !options.Skip.Contains("folders"));

        /// <summary>Unticks the files <c>-vdExclude</c> names, as the window's checkboxes do; returns them.</summary>
        private static List<string> ApplyExclusions(VirtuademyRenameRoutine.ScanResult scan, Options options)
        {
            List<string> excluded = new();
            foreach (VirtuademyRenameRoutine.FileEntry file in scan.Files.Where(f => IsExcluded(f.Path, options.Exclude)))
            {
                file.Selected = false;
                excluded.Add(file.Path);
            }
            return excluded;
        }

        private static Dictionary<string, object> DescribeRename(VirtuademyRenameRoutine.ScanResult scan, Options options)
        {
            List<string> excluded = ApplyExclusions(scan, options);
            Dictionary<string, object> found = new()
            {
                { "files", scan.Files.Count(f => f.Selected) },
                { "occurrences", scan.Files.Where(f => f.Selected).Sum(f => f.Hits) },
                { "poiPages", scan.Pages.Count },
                { "guidCollisions", scan.Guids.Count },
                { "legacyFolders", scan.HasLegacyFolders },
            };
            if (excluded.Count > 0)
            {
                found["excluded"] = Paths(excluded);
            }
            if (options.Skip.Count > 0)
            {
                // What a skipped step found is still counted above, so that it can be seen; it does not count as work.
                found["skipped"] = options.Skip.OrderBy(s => s).ToList();
            }
            if (scan.UnmappedReferences.Count > 0)
            {
                found["unmappedAssemblyReferences"] = Findings(scan.UnmappedReferences);
            }
            if (scan.PartialReferences.Count > 0)
            {
                found["partialAssemblyReferences"] = Findings(scan.PartialReferences);
            }
            return found;
        }

        private static List<object> Findings(IEnumerable<VirtuademyAssemblyReferenceMigrator.Finding> findings)
            => findings.Take(max_listed)
                .Select(f => (object)new Dictionary<string, object> { { "path", f.Path }, { "reference", f.Reference }, { "detail", f.Detail } })
                .ToList();

        private static void RenameDryRun(Options options)
        {
            VirtuademyRenameRoutine.ScanResult scan = VirtuademyRenameRoutine.ScanProject();
            Dictionary<string, object> found = DescribeRename(scan, options);

            found["fileList"] = scan.Files.Where(f => f.Selected).Take(max_listed)
                .Select(f => (object)new Dictionary<string, object> { { "path", f.Path }, { "hits", f.Hits } }).ToList();
            found["poiPageFiles"] = scan.Pages.Take(max_listed)
                .Select(p => (object)new Dictionary<string, object> { { "path", p.Path }, { "pages", p.Pages }, { "deadHooks", p.DeadHooks } }).ToList();
            found["guidCollisionList"] = scan.Guids.Take(max_listed)
                .Select(g => (object)new Dictionary<string, object>
                {
                    { "path", g.Path },
                    { "packagePath", g.PackagePath },
                    { "identical", g.Identical },
                    { "isFolder", g.IsFolder },
                    { "resolution", g.IsFolder ? "KeepProjectCopy" : g.Resolution.ToString() },
                    { "referencedBy", g.ReferencingFiles.Count },
                }).ToList();

            bool pending = HasWork(scan, options) || VirtuademyUpdateResave.IsPending;
            report["dryRun"] = true;
            report["found"] = found;
            report["resavePending"] = VirtuademyUpdateResave.IsPending;
            report["unmigratedGraphFiles"] = VirtuademyRenameRoutine.FilesWithUnmigratedGraphData().Count;
            Finish(pending ? ExitChangesPending : ExitDone);
        }

        private static void RenameApply(Options options)
        {
            VirtuademyRenameRoutine.ScanResult scan = VirtuademyRenameRoutine.ScanProject();
            Dictionary<string, object> found = DescribeRename(scan, options);
            report["found"] = found;

            if (!HasWork(scan, options))
            {
                Debug.Log(log_prefix + rename_id + ": nothing to change.");
                FinishRename(options, false);
                return;
            }

            VirtuademyRenameRoutine.Options apply = new()
            {
                RewriteFiles = scan.Files.Any(f => f.Selected),
                MigratePOIPages = scan.Pages.Count > 0 && !options.Skip.Contains("pages"),
                ResolveGuidCollisions = scan.Guids.Count > 0 && !options.Skip.Contains("guids"),
                ConsolidateFolders = scan.HasLegacyFolders && !options.Skip.Contains("folders"),
                UnpinLockFile = !options.Skip.Contains("unpin"),
            };

            // The apply refreshes the AssetDatabase, which queues a recompilation. Keep the reload from
            // starting inside it: every step has to have run, in this editor, before the launch ends.
            EditorApplication.LockReloadAssemblies();

            VirtuademyRenameRoutine.Result result = VirtuademyRenameRoutine.Apply(scan, apply);

            Dictionary<string, object> applied = new()
            {
                { "rewritten", Paths(result.RewrittenPaths) },
                { "failedFiles", result.FailedFiles },
                { "poiPages", new Dictionary<string, object> { { "files", Paths(result.PagePaths) }, { "failed", result.FailedPages } } },
                {
                    "guidCollisions", new Dictionary<string, object>
                    {
                        { "resolved", result.ResolvedGuids },
                        { "failed", result.FailedGuids },
                        { "referencesRewrittenIn", Paths(result.GuidPaths) },
                    }
                },
                { "lockReleased", result.LockUnpinned },
                { "unpinned", result.Unpinned },
                {
                    "folders", new Dictionary<string, object>
                    {
                        { "moved", result.Folders.Moved },
                        { "pruned", result.Folders.Pruned },
                        { "merged", result.Folders.Merges },
                        { "refused", result.Folders.Refused },
                        { "left", result.Folders.Refusals },
                    }
                },
                { "toResave", result.Resaving.Count },
                { "nodeLibrary", result.NodeLibrary.ToString() },
            };
            if (result.NodeLibrary == VirtuademyRenameRoutine.NodeLibraryState.NotBuilt)
            {
                applied["nodeLibraryNote"] = "The project never built its Visual Scripting node library, so there was none to rebuild. " +
                                             "Build it after the last launch: SetupCli -vdSetupSteps vslibrary, or Virtuademy > Advanced > Update Visual Scripting library.";
            }
            if (result.UnmappedReferences.Count > 0)
            {
                applied["unmappedAssemblyReferences"] = Findings(result.UnmappedReferences);
            }
            if (result.PartialReferences.Count > 0)
            {
                applied["partialAssemblyReferences"] = Findings(result.PartialReferences);
            }
            if (result.DiscardedScenes.Count > 0)
            {
                applied["discardedUnsavedScenes"] = result.DiscardedScenes;
            }
            report["applied"] = applied;

            if (result.FailedFiles > 0 || result.FailedPages > 0 || result.FailedGuids > 0 || result.Folders.Refused > 0)
            {
                report["problems"] = true;
            }

            if (result.NeedsReload)
            {
                // Never unlocked: the editor exits, and the next launch compiles what was rewritten. The
                // re-save is scheduled in Library/ and waits for that launch.
                report["next"] = "Launch again with the same arguments: Unity recompiles, then the changed files are re-saved and reported.";
                Finish(ExitRelaunch);
                return;
            }

            EditorApplication.UnlockReloadAssemblies();

            // A launch whose apply moved nothing and rewrote nothing (a folder the routine cannot empty, left
            // again) is not progress: asking for another launch would go on for ever.
            bool changed = result.RewrittenPaths.Count + result.PagePaths.Count + result.GuidPaths.Count
                           + result.ResolvedGuids + result.Folders.Moved + result.Folders.Merged + result.Folders.Pruned > 0;

            if (VirtuademyUpdateResave.IsPending)
            {
                WaitForResave(() =>
                {
                    RecordResave();
                    FinishRename(options, changed);
                });
                return;
            }

            FinishRename(options, changed);
        }

        /// <summary>The end of a launch that did not stop for a reload: what the re-save did, and a rescan for what is left.</summary>
        private static void FinishRename(Options options, bool changedSomething)
        {
            // What the routine would still do. Files the caller excluded and steps the caller skipped do not count.
            VirtuademyRenameRoutine.ScanResult left = VirtuademyRenameRoutine.ScanProject();
            report["left"] = DescribeRename(left, options);

            bool leftWork = HasWork(left, options);

            // A reference to an assembly with no successor is not work the routine can do: it is left as it was, the project
            // does not compile, and a person has to say what replaces it.
            if (left.UnmappedReferences.Count > 0)
            {
                report["problems"] = true;
            }
            bool problems = report.ContainsKey("problems");

            // The routine is complete when nothing is left: that includes a launch that found nothing to change in a
            // project migrated before, which never reaches the end of Apply where the save guard is retired.
            if (!leftWork && !problems && !VirtuademyUpdateResave.IsPending)
            {
                VirtuademyUpdateSaveGuard.RetireIfMigrated();
            }

            int code = problems ? ExitProblems
                : !leftWork ? ExitDone
                : changedSomething ? ExitRelaunch
                : ExitProblems;
            if (leftWork && !changedSomething)
            {
                report["error"] = "Work is left and this launch changed nothing: see 'left' and the Console.";
            }
            Finish(code);
        }

        #endregion

        #region 2026.5

        private static void RunMobile(Options options)
        {
            List<string> blockers = VirtuademyRenameRoutine.FilesWithUnmigratedGraphData();
            if (blockers.Count > 0)
            {
                report["blockedBy"] = rename_id;
                report["blockedByFiles"] = Paths(blockers);
                report["error"] = $"{blockers.Count} file(s) still have Visual Scripting data the {rename_id} routine renames. Run it first: " +
                                  "until then their graphs cannot be read nor saved, and this routine saves graphs.";
                Debug.LogError(log_prefix + report["error"]);
                Finish(ExitRenameFirst);
                return;
            }

            if (!TryInvoke("Scan", new object[0], out Dictionary<string, object> scan, out string unavailable))
            {
                report["error"] = unavailable;
                Debug.LogError(log_prefix + unavailable);
                Finish(ExitRoutineUnavailable);
                return;
            }

            List<Dictionary<string, object>> occurrences = ((List<object>)scan["occurrences"]).Cast<Dictionary<string, object>>().ToList();
            List<string> excludedPaths = occurrences.Select(o => (string)o["path"]).Distinct().Where(p => IsExcluded(p, options.Exclude)).ToList();
            int pending = occurrences.Count(o => (string)o["status"] == "NotConnected" && !excludedPaths.Contains((string)o["path"]));

            Dictionary<string, object> found = scan.Where(p => p.Key != "occurrences").ToDictionary(p => p.Key, p => p.Value);
            found["selected"] = pending;
            if (excludedPaths.Count > 0)
            {
                found["excluded"] = Paths(excludedPaths);
            }
            found["nodeList"] = occurrences.Take(max_listed).Cast<object>().ToList();
            report["found"] = found;

            if (options.DryRun)
            {
                report["dryRun"] = true;
                Finish(pending > 0 ? ExitChangesPending : ExitDone);
                return;
            }

            if (pending == 0)
            {
                Debug.Log(log_prefix + mobile_id + ": nothing to change.");
                Finish(Convert.ToBoolean(scan["scenesSkipped"]) ? ExitProblems : ExitDone);
                return;
            }

            if (!TryInvoke("Apply", new object[] { excludedPaths }, out Dictionary<string, object> applied, out unavailable))
            {
                report["error"] = unavailable;
                Finish(ExitRoutineUnavailable);
                return;
            }
            report["applied"] = applied;

            // The same scan again: running the routine twice must find nothing.
            TryInvoke("Scan", new object[0], out Dictionary<string, object> after, out _);
            int stillPending = after == null ? -1
                : ((List<object>)after["occurrences"]).Cast<Dictionary<string, object>>()
                    .Count(o => (string)o["status"] == "NotConnected" && !excludedPaths.Contains((string)o["path"]));
            report["left"] = new Dictionary<string, object> { { "notConnected", stillPending } };

            bool problems = Convert.ToInt32(applied["failed"]) > 0 || stillPending != 0 || Convert.ToBoolean(applied["scenesSkipped"]);
            Finish(problems ? ExitProblems : ExitDone);
        }

        /// <summary>Calls a method of <c>CheckPlatformUnitMobilePortRoutine</c>, in the SDK's editor assembly, by reflection (see its remarks).</summary>
        private static bool TryInvoke(string method, object[] arguments, out Dictionary<string, object> result, out string unavailable)
        {
            result = null;
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(mobile_type)).FirstOrDefault(t => t != null);
            MethodInfo info = type?.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (info == null)
            {
                unavailable = $"{mobile_type}.{method} is not loaded: the SDK's editor assembly (Virtuademy.SDK.Environments.Editor) did not compile.";
                return false;
            }

            try
            {
                result = (Dictionary<string, object>)info.Invoke(null, arguments);
                unavailable = null;
                return true;
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError(log_prefix + $"{method} failed: {ex.InnerException}");
                unavailable = $"{method} failed: {ex.InnerException?.Message}";
                return false;
            }
        }

        #endregion

        #region Waiting for the re-save

        /// <summary>
        /// Calls <paramref name="whenDone"/> once the re-save the previous launch scheduled has run. The editor runs it
        /// by itself when it has recompiled, resolved its packages and been idle for a few seconds
        /// (<see cref="VirtuademyUpdateResave"/>); this only waits, and gives up on compile errors (it would wait for ever)
        /// or on the timeout.
        /// </summary>
        private static void WaitForResave(Action whenDone)
        {
            void Poll()
            {
                if (finished)
                {
                    EditorApplication.update -= Poll;
                    return;
                }

                try
                {
                    if (DateTime.UtcNow >= deadlineUtc)
                    {
                        EditorApplication.update -= Poll;
                        report["error"] = "The re-save did not run in time: the editor never settled.";
                        Finish(ExitTimeout);
                    }
                    else if (!VirtuademyUpdateResave.IsPending)
                    {
                        EditorApplication.update -= Poll;
                        whenDone();
                    }
                    else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && EditorUtility.scriptCompilationFailed)
                    {
                        EditorApplication.update -= Poll;
                        report["error"] = "The project has compile errors: the re-save is postponed, nothing was saved. Fix them and launch again.";
                        Finish(ExitCompileErrors);
                    }
                }
                catch (Exception ex)
                {
                    EditorApplication.update -= Poll;
                    Fail(ex);
                }
            }

            EditorApplication.update += Poll;
        }

        private static void RecordResave()
        {
            VirtuademyUpdateResave.Summary summary = VirtuademyUpdateResave.LastRun;
            if (summary == null)
            {
                return;
            }

            report["resave"] = new Dictionary<string, object>
            {
                { "resaved", summary.Resaved },
                { "alreadyAsUnityWritesThem", summary.Unchanged },
                { "noLongerInProject", summary.Missing },
                {
                    "toCheckByHand", summary.ToCheck.Select(c => (object)new Dictionary<string, object>
                    {
                        { "path", c.Path },
                        { "reason", c.Reason },
                    }).ToList()
                },
            };

            if (summary.ToCheck.Count > 0)
            {
                report["problems"] = true;
            }
        }

        #endregion

        #region Report

        private static Dictionary<string, object> Paths(IEnumerable<string> paths)
        {
            List<string> all = paths.ToList();
            return new Dictionary<string, object>
            {
                { "count", all.Count },
                { "paths", all.Take(max_listed).ToList() },
                { "truncated", all.Count > max_listed },
            };
        }

        // A small writer rather than a JSON library: this file compiles into the project's own editor assembly,
        // which is not guaranteed to reference one (see VirtuademyPackageLock). Dictionaries, lists, strings,
        // numbers and booleans are all the report holds.
        private static string ToJson(object value)
        {
            StringBuilder builder = new();
            WriteJson(builder, value);
            return builder.ToString();
        }

        private static void WriteJson(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case int or long or short or byte or uint or ulong:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case float or double:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case IDictionary dictionary:
                    builder.Append('{');
                    bool firstMember = true;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (!firstMember)
                        {
                            builder.Append(',');
                        }
                        firstMember = false;
                        WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        builder.Append(':');
                        WriteJson(builder, entry.Value);
                    }
                    builder.Append('}');
                    break;
                case IEnumerable sequence:
                    builder.Append('[');
                    bool firstItem = true;
                    foreach (object item in sequence)
                    {
                        if (!firstItem)
                        {
                            builder.Append(',');
                        }
                        firstItem = false;
                        WriteJson(builder, item);
                    }
                    builder.Append(']');
                    break;
                default:
                    WriteString(builder, value.ToString());
                    break;
            }
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }

        #endregion
    }
}
