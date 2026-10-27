using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Virtuademy.SDK.TenantConfiguration;
using Virtuademy.SDK.TenantConfiguration.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

using static Virtuademy.SDK.Environments.Editor.SceneListScriptableObject;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// Builds and publishes an environment from the command line, for a machine with nobody at the
    /// Addressables management window: what the window's Build, Preview and Deploy buttons do, and
    /// what "Connect the SDK to your tenant" does by hand, driven by arguments.
    /// <code>
    /// Unity -batchmode -nographics -projectPath &lt;project&gt; -logFile -
    ///       -executeMethod Virtuademy.SDK.Environments.Editor.EnvironmentsCli.Run
    ///       -vdCommand &lt;command&gt; [options]
    /// </code>
    /// <para>
    /// <b>Commands</b> (options in brackets; every one is a <c>-vd...</c> argument):
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>status</c>: the session, whether the Addressables are configured, the scene
    /// list (name, included in the build, platforms) and the interpreter. Changes nothing and needs no
    /// network.</description></item>
    /// <item><description><c>session [-vdRenew]</c>: the session fields. With <c>-vdRenew</c> it tries to renew an
    /// expired session silently and exits with <see cref="ExitLoginRequired"/> when it cannot.</description></item>
    /// <item><description><c>tenant-config -vdConfigFile &lt;path&gt; [-vdEnv &lt;key&gt;] [-vdConfigFolder &lt;folder&gt;] [-vdAllowTracked]</c>:
    /// imports the file downloaded from the Back Office (<b>Download SDK Environments Config</b>) into the
    /// project (<c>Assets/Editor/TenantConfiguration</c> unless told otherwise), adds it to the App Assets of the
    /// App Configuration Settings asset and selects it (<see cref="AppConfigurationImporter"/>). It does not log
    /// in, and never prints the file's content. The file holds the app's secret: when the project is in a git work
    /// tree and git does not ignore where it would land, the command exits with <see cref="ExitSecretNotIgnored"/>
    /// and writes nothing, unless <c>-vdAllowTracked</c> says the file is meant to be tracked.</description></item>
    /// <item><description><c>login [-vdConnection &lt;connection&gt; | -vdApp &lt;app&gt; [-vdEnv &lt;key&gt;]] [-vdNoBrowser] [-vdLoginTimeoutSeconds &lt;n&gt;]</c>:
    /// logs in to a connection of the per-user store (ADR 0037), or, with <c>-vdApp</c>, to the connection of one of
    /// the project's configuration files (the app is the file's name up to its first dot, the environment a key
    /// inside it), which is added to the store when it is not there yet. It is the one command that wants a
    /// browser: it prints <c>AUTH_URL &lt;url&gt;</c> as soon as it has it, opens the system browser unless
    /// <c>-vdNoBrowser</c> says somebody else will (the URL is then the way in), and gives up
    /// <c>-vdLoginTimeoutSeconds</c> (180 by default) after the URL is issued, with <see cref="ExitLoginTimeout"/>.
    /// With <c>-vdResultFile</c> the URL is also written to the result file at once, as <c>state: awaiting-browser</c>.
    /// The editor must not have the project open: see "Batch or open editor" below.</description></item>
    /// <item><description><c>logout [-vdConnection &lt;connection&gt;]</c>: ends the connection's session. The Azure account
    /// stays in the token cache when another connection uses it.</description></item>
    /// <item><description><c>connections</c>: every connection of the store, with whether its session is valid and
    /// which one the project uses. Needs no network. The same list, without any secret, is in the store's status file
    /// (<c>%USERPROFILE%\.virtuademy\sdk\status.json</c>), which can be read without starting Unity.</description></item>
    /// <item><description><c>whoami [-vdConnection &lt;connection&gt;]</c>: one connection (the project's default one when none is
    /// named): tenant, environment, user, role and when the token expires, never the token. Exits with
    /// <see cref="ExitLoginRequired"/> when there is no session. Also readable from the status file.</description></item>
    /// <item><description><c>worlds [-vdConnection &lt;connection&gt;]</c>: the worlds the login can deploy to, and for every scene
    /// included in the build, where it is already published.</description></item>
    /// <item><description><c>build [-vdScenes a,b]</c>: what <b>Build Addressables</b> does, interpreted scripts
    /// included: refuses an unconfigured project, compiles and verifies the interpreted DLL, builds every platform
    /// each included scene needs and zips them into <c>ServerData</c>. Before anything is built it refuses a project
    /// whose scenes use a component with a script in a <c>*-firstpass</c> assembly (under <c>Assets/Plugins</c> or
    /// <c>Assets/Standard Assets</c>), which the app cannot bind: <see cref="ExitBuildFailed"/>, with the components in
    /// the report's <c>build.firstpassComponents</c>. <c>-vdScenes</c> limits it to some of the
    /// included scenes (by scene name).</description></item>
    /// <item><description><c>preview (-vdWorlds &lt;ids&gt; | -vdTenant) [-vdConnection &lt;connection&gt;] [-vdExpectTenant &lt;label&gt;] [-vdScenes a,b]</c>:
    /// what a deploy there would add and update, per scene. Read-only. <c>-vdExpectTenant</c> is checked when given.</description></item>
    /// <item><description><c>deploy (-vdWorlds &lt;ids&gt; | -vdTenant) [-vdConnection &lt;connection&gt;] -vdExpectTenant &lt;label&gt; [-vdBuild] [-vdDryRun] [-vdScenes a,b]</c>:
    /// what <b>Deploy</b> / <b>Build &amp; Deploy</b> (and the tenant buttons) do. The world ids are explicit; an id the
    /// login cannot deploy to stops the command before anything is built or sent. <c>-vdExpectTenant</c> is
    /// mandatory: the label of the tenant of the connection the command acts on (<c>whoami</c> prints it), compared
    /// case-insensitively; a missing one exits with <see cref="ExitTenantNotConfirmed"/> and a different one with
    /// <see cref="ExitTargetMismatch"/>, before anything is built or sent. <c>-vdBuild</c> builds first, as <c>build</c> does; without it the zips
    /// already in <c>ServerData</c> are uploaded. <c>-vdDryRun</c> does everything but the uploads and imports and
    /// reports what it would send. At tenant level the interpreted scripts are not uploaded: the environment runs
    /// without them.</description></item>
    /// </list>
    /// <para>
    /// <b>Where a command talks to.</b> The commands that call the platform (<c>worlds</c>, <c>preview</c>,
    /// <c>deploy</c>) act on the connection <c>-vdConnection &lt;connection&gt;</c> names, whether or not it is the project's
    /// default connection, and on the default one (chosen in Virtuademy &gt; Available Tenants, or the first login) when
    /// it names none. The connection is taken <b>once when the run starts</b> (<c>targetKey</c>): every request of the run
    /// goes to it, whatever the default connection becomes meanwhile. They take its Application API address from the tenant
    /// that connection reported at login, never from the project's generated <c>PlatformEndpoints</c> asset: a token is sent
    /// to the host of the connection it belongs to. A <c>-vdExpectTenant</c> that is not that connection's tenant label exits
    /// with <see cref="ExitTargetMismatch"/>. <c>build</c> verifies the interpreted scripts on the default connection. The
    /// REPORT of those three commands carries a <c>target</c> object: the connection, whether it is the default one, the
    /// tenant label, its environment and the API host.
    /// </para>
    /// <para>
    /// <b>Batch or open editor.</b> Unity refuses to open a project that another editor has open, so these commands
    /// run in batch mode only while the project is closed (<c>Temp/UnityLockfile</c> is absent or not held). With the
    /// editor open, a script drops a request file in <c>UserSettings/Virtuademy/requests/</c> instead and the
    /// editor answers it (<c>login</c>, <c>logout</c> and <c>status</c> only; see <c>EditorRequestPump</c> in
    /// <c>Virtuademy-SDK-Core</c>). The two run the same code and give the same answer.
    /// </para>
    /// <para>
    /// <b>Sessions.</b> Commands that call the platform renew an expired session silently, and when that is not
    /// possible they exit with <see cref="ExitLoginRequired"/>: <b>batch mode never starts an interactive login on
    /// its own</b> (<see cref="EditorSessionManager.InteractiveLoginAllowed"/>); the <c>login</c> command is how it is
    /// asked for. Log in once with it, or from <b>Virtuademy &gt; Available Tenants</b>; the session and its renewal
    /// are stored per OS user, outside the project, and shared by every project and process of that user.
    /// <c>build</c> needs a session only for a project that has interpreted scripts: the
    /// authoritative verification is the server's, and without a session it cannot run, so such a build is
    /// refused (fail-closed), as it is in the window — unless the scripts are unchanged since they were last
    /// published, which skips the compile and the verification. A project without interpreted scripts builds
    /// with no session at all.
    /// </para>
    /// <para>
    /// <b>One run, one editor.</b> The work is asynchronous (HTTP, the DLL verification), so the method keeps the
    /// editor alive until it finishes and exits it itself with one of the codes below: <c>-quit</c> is not needed
    /// and must not be used. The editor is kept from reloading its scripts for the whole run
    /// (<see cref="EditorApplication.LockReloadAssemblies"/>): a build switches the active build target, which
    /// queues a recompilation and a domain reload a few seconds later, and a reload in the middle of a deploy
    /// would end the awaiting code silently. An overall timeout (<c>-vdTimeoutMinutes</c>, 60 by default) ends a
    /// run that hangs, except in the middle of a deploy: when it passes during one, the scene step in flight
    /// (its upload and import) finishes, no further world, assembly, scene or link step starts, and the run exits
    /// with <see cref="ExitTimeout"/> and a REPORT whose <c>deploy</c> says what was uploaded, started and linked
    /// per world and what was not. A hard ceiling, ten minutes after the deadline, ends even that. A build leaves
    /// the project on the last platform it built, as the window does.
    /// </para>
    /// <para>
    /// <b>Arguments are strict.</b> Any <c>-vd...</c> argument (with a <c>-</c>, <c>--</c> or <c>/</c> prefix, in any
    /// case) that is not exactly one of the command's options is refused with <see cref="ExitBadArguments"/>, as is
    /// an option given twice and a value that no option claims (<c>-vdWorlds 12 13</c>): a typo must never run the
    /// command with a default nobody chose. Unity's own arguments are not touched.
    /// </para>
    /// <para>
    /// Every run ends with a <c>[EnvironmentsCli] REPORT</c> line: one JSON object with the command's result,
    /// followed by <c>[EnvironmentsCli] EXIT &lt;code&gt;</c>. Unity's standard output carries the whole editor log, so
    /// <c>-vdResultFile &lt;path&gt;</c> (any command) also writes the REPORT, and only it, to that file, with
    /// <c>state: done</c>; a script reads the file instead of the log. Nothing secret goes into it or into the log: no
    /// token, no upload link, nothing from the configuration file. In a deploy report, <c>importsStarted</c>
    /// counts the imports the platform accepted: it answers "started", and the import runs on the platform
    /// afterwards, so it is not a count of live environments.
    /// </para>
    /// </summary>
    public static class EnvironmentsCli
    {
        /// <summary>The command did what it was asked.</summary>
        public const int ExitDone = 0;

        /// <summary>An unexpected exception: the log has the stack trace.</summary>
        public const int ExitUnexpected = 1;

        public const int ExitBadArguments = 10;

        /// <summary>There is no session, or it expired and could not be renewed silently. Log in with the login command or from Virtuademy &gt; Available Tenants.</summary>
        public const int ExitLoginRequired = 11;

        /// <summary>The project's Addressables are not configured the way the SDK needs (a build would fail at the end).</summary>
        public const int ExitAddressablesNotConfigured = 12;

        /// <summary>No scene is included in the build, the project has no scene list, or <c>-vdScenes</c> names a scene that is not included.</summary>
        public const int ExitNothingToBuild = 13;

        /// <summary>The build failed: a missing build module, a script in a firstpass assembly the app does not have, a missing platform folder or catalog.</summary>
        public const int ExitBuildFailed = 14;

        /// <summary>The interpreted scripts did not pass the local or the server verification.</summary>
        public const int ExitScriptsRefused = 15;

        /// <summary>A world id is not one the login can deploy to.</summary>
        public const int ExitWorldNotDeployable = 16;

        /// <summary>The deploy finished with problems: <c>published</c> in the report says how much went through.</summary>
        public const int ExitDeployProblems = 17;

        /// <summary>A tenant deploy was asked and the login is not a TenantManager.</summary>
        public const int ExitNotTenantManager = 18;

        /// <summary>The configuration file was not imported.</summary>
        public const int ExitTenantConfigFailed = 19;

        /// <summary>The platform could not be read: no API URL, an error answer, or no list of published environments.</summary>
        public const int ExitPlatformUnreachable = 20;

        /// <summary>The run did not finish within <c>-vdTimeoutMinutes</c>.</summary>
        public const int ExitTimeout = 21;

        /// <summary><c>deploy</c> without <c>-vdBuild</c> found a scene with no zip in <c>ServerData</c>.</summary>
        public const int ExitZipsMissing = 22;

        /// <summary><c>tenant-config</c> was refused: git does not ignore where the file (it holds the app's secret) would sit. <c>-vdAllowTracked</c> overrides.</summary>
        public const int ExitSecretNotIgnored = 23;

        /// <summary><c>deploy</c> without <c>-vdExpectTenant</c>. A label that names another tenant is <see cref="ExitTargetMismatch"/> (it was 24 up to 2026.6).</summary>
        public const int ExitTenantNotConfirmed = 24;

        /// <summary>
        /// No longer returned, and reserved: the Application API address of the project's generated asset was on another host than the
        /// login's. The address comes from the connection since ADR 0037, so that case cannot happen. Scripts written for earlier versions
        /// read 25 with that meaning, so the number is not reused.
        /// </summary>
        public const int ExitApiHostMismatch = 25;

        /// <summary>The connection a command names is not in the store, or the name fits several. <c>connections</c> lists them.</summary>
        public const int ExitConnectionUnknown = 26;

        /// <summary><c>login</c> was not finished in the browser in time. The URL had been printed.</summary>
        public const int ExitLoginTimeout = 27;

        /// <summary><c>login</c> failed: the platform, Azure or the auth configuration refused (the log says which).</summary>
        public const int ExitLoginFailed = 28;

        /// <summary><c>login</c> in batch mode where the store cannot be written (no encryption on this OS yet): the session would die with the process.</summary>
        public const int ExitStoreNotPersistent = 29;

        /// <summary>Target mismatch: <c>-vdExpectTenant</c> names another tenant than the one of the connection the command acts on. Nothing was built or sent.</summary>
        public const int ExitTargetMismatch = 30;

        private const string log_prefix = "[EnvironmentsCli] ";
        private const int default_timeout_minutes = 60;

        /// <summary>How long after the deadline a deploy may still be finishing its step before the run is ended anyway.</summary>
        private const int hard_ceiling_extra_minutes = 10;

        private static readonly Dictionary<string, string[]> command_options = new()
        {
            { "status", new string[0] },
            { "session", new[] { "-vdRenew" } },
            { "login", new[] { "-vdConnection", "-vdApp", "-vdEnv", "-vdNoBrowser", "-vdLoginTimeoutSeconds" } },
            { "logout", new[] { "-vdConnection" } },
            { "connections", new string[0] },
            { "whoami", new[] { "-vdConnection" } },
            { "tenant-config", new[] { "-vdConfigFile", "-vdEnv", "-vdConfigFolder", "-vdAllowTracked" } },
            { "worlds", new[] { "-vdConnection" } },
            { "build", new[] { "-vdScenes" } },
            { "preview", new[] { "-vdWorlds", "-vdTenant", "-vdScenes", "-vdExpectTenant", "-vdConnection" } },
            { "deploy", new[] { "-vdWorlds", "-vdTenant", "-vdBuild", "-vdDryRun", "-vdScenes", "-vdExpectTenant", "-vdConnection" } },
        };

        private static readonly string[] flag_options = { "-vdRenew", "-vdTenant", "-vdBuild", "-vdDryRun", "-vdAllowTracked", "-vdNoBrowser" };

        // Every option that takes a value, the two every command reads included.
        private static readonly string[] value_options =
        {
            "-vdCommand", "-vdTimeoutMinutes", "-vdResultFile", "-vdConfigFile", "-vdEnv", "-vdConfigFolder", "-vdScenes", "-vdWorlds", "-vdExpectTenant",
            "-vdConnection", "-vdApp", "-vdLoginTimeoutSeconds",
        };

        // Unity's own options that take a value: the token after one is Unity's, not a stray value of ours.
        private static readonly string[] unity_value_options = { "-projectPath", "-logFile", "-executeMethod", "-buildTarget", "-activeBuildProfile" };

        // What an option of ours looks like, in any prefix and case: anything matching it has to be one exactly.
        private static readonly Regex vd_like = new("^[-/]+vd", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // camelCase members, like the keys the commands put in the report by hand; dictionary keys as they are.
        private static readonly JsonSerializerSettings report_settings = new()
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
        };

        private static readonly Dictionary<string, object> report = new();
        private static string command;
        private static bool finished;
        private static DateTime deadlineUtc;
        private static int timeoutMinutes;

        // The exit code the run ends with, set from any thread and acted on in EditorApplication.update: the editor is
        // asked to exit from its own loop, which is what a run without -quit has to do after an await.
        private static volatile int pendingExit = int.MinValue;
        private static string resultFile;

        // The connection this run works on: the one -vdConnection names, or the project's default one, taken once when the
        // run starts. Every request of the run goes to it, whatever the default connection becomes meanwhile (ADR 0037).
        private static string targetKey;

        // True from the moment a deploy starts sending: the deadline then stops it between steps instead of ending the run.
        private static bool deployActive;
        private static bool deployStopAnnounced;

        /// <summary>The <c>-executeMethod</c> entry point. It does not return: it exits the editor.</summary>
        public static void Run()
        {
            // See the class remarks: a build target switch queues a domain reload, and the code that
            // awaits the network after it would never resume. Never unlocked: the run ends in Exit.
            EditorApplication.LockReloadAssemblies();

            string[] args = Environment.GetCommandLineArgs();

            // Read leniently here: the strict parse comes next and refuses a value that is not a positive number.
            int minutes = default_timeout_minutes;
            string timeout = GetArgument(args, "-vdTimeoutMinutes");
            if (timeout != null && int.TryParse(timeout, out int parsed) && parsed > 0)
                minutes = parsed;

            timeoutMinutes = minutes;
            deadlineUtc = DateTime.UtcNow.AddMinutes(minutes);
            resultFile = GetArgument(args, "-vdResultFile");
            EditorApplication.update += CheckTimeout;
            EditorApplication.update += ExitWhenFinished;

            _ = RunAsync(args);
        }

        private static async Task RunAsync(string[] args)
        {
            int code;
            try
            {
                code = await ExecuteAsync(args);
            }
            catch (Exception ex)
            {
                Debug.LogError(log_prefix + "Unexpected failure: " + ex);
                report["error"] = ex.Message;
                code = ExitUnexpected;
            }

            Finish(code);
        }

        private static void CheckTimeout()
        {
            if (finished || DateTime.UtcNow < deadlineUtc)
                return;

            if (deployActive)
            {
                // Never in the middle of an upload, an import or a link: the deploy asks StopReason between
                // steps and ends itself with a report of what it did. The ceiling is the last resort for a
                // step that does not come back.
                if (!deployStopAnnounced)
                {
                    deployStopAnnounced = true;
                    Debug.LogWarning(log_prefix + $"The time limit ({timeoutMinutes} minute(s)) passed during the deploy: the step in flight " +
                                     "finishes, the next ones are not started.");
                }

                if (DateTime.UtcNow < deadlineUtc.AddMinutes(hard_ceiling_extra_minutes))
                    return;

                string ceiling = $"The deploy did not come back {hard_ceiling_extra_minutes} minute(s) after the time limit: the run was ended in the middle of a step. " +
                                 "The deploy section of this report says what had been done before it.";
                Debug.LogError(log_prefix + ceiling);
                report["error"] = ceiling;
                Finish(ExitTimeout);
                return;
            }

            Debug.LogError(log_prefix + "The run did not finish in time.");
            Finish(ExitTimeout);
        }

        private static void Finish(int code)
        {
            if (finished)
                return;

            finished = true;
            EditorApplication.update -= CheckTimeout;

            report["command"] = command;
            report["exitCode"] = code;
            report["exit"] = Describe(code);

            try
            {
                Debug.Log(log_prefix + "REPORT " + JsonConvert.SerializeObject(report, report_settings));

                if (!string.IsNullOrEmpty(resultFile))
                {
                    report["state"] = "done";
                    WriteResultFile(JsonConvert.SerializeObject(report, Formatting.Indented, report_settings));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(log_prefix + "The report could not be produced: " + ex.Message);
            }

            Debug.Log($"{log_prefix}EXIT {code} ({Describe(code)})");
            pendingExit = code;
        }

        /// <summary>Ends the editor from its update loop, once the run has finished (see <see cref="pendingExit"/>).</summary>
        private static void ExitWhenFinished()
        {
            if (pendingExit == int.MinValue)
                return;

            int code = pendingExit;
            pendingExit = int.MinValue;
            EditorApplication.update -= ExitWhenFinished;
            EditorApplication.Exit(code);
        }

        /// <summary>The result file is replaced whole, so a script that reads it never meets half of it.</summary>
        private static void WriteResultFile(string json)
        {
            string path = Path.GetFullPath(resultFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, json, new System.Text.UTF8Encoding(false));

            // Swapped in, never deleted first: a reader meets the old file or the new one.
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        #region Arguments

        private sealed class Options
        {
            public bool Renew;
            public bool Tenant;
            public bool Build;
            public bool DryRun;
            public bool AllowTracked;
            public string ConfigFile;
            public string ConfigFolder;
            public string Env;
            public string Scenes;
            public string ExpectTenant;
            public string Connection;
            public string App;
            public bool NoBrowser;
            public int? LoginTimeoutSeconds;
            public List<int> Worlds;
        }

        private static string GetArgument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                return null;

            return args[index + 1];
        }

        /// <summary>
        /// Reads every <c>-vd</c> argument of the command line, strictly: one that is not exactly a known
        /// option (<c>--vdDryRun</c>, <c>/vdDryRun</c>, <c>-vddryrun</c> are not <c>-vdDryRun</c>), one given twice,
        /// an option with no value, and a token that no option claims after an option (<c>-vdWorlds 12 13</c>)
        /// are all refused. Unity's own arguments (and the value of the ones that take one) are left alone.
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
                    continue;

                bool isFlag = flag_options.Contains(arg);
                bool isValue = value_options.Contains(arg);
                if (!isFlag && !isValue)
                {
                    string near = flag_options.Concat(value_options).FirstOrDefault(o =>
                        string.Equals(o, "-" + arg.TrimStart('-', '/'), StringComparison.OrdinalIgnoreCase));
                    error = $"'{arg}' is not an option of this tool." +
                            (near != null ? $" Did you mean {near}? Options are exact, with a single leading dash." : " Options start with a single dash, like -vdCommand.");
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
                            "(a list takes commas, as in -vdWorlds 12,13).";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseArguments(string[] args, out Options options, out string error)
        {
            options = new Options();

            if (!TryScanArguments(args, out Dictionary<string, string> values, out HashSet<string> flags, out error))
                return false;

            string Value(string name) => values.TryGetValue(name, out string v) ? v : null;

            command = Value("-vdCommand")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(command))
            {
                error = "-vdCommand is missing. Commands: " + string.Join(", ", command_options.Keys) + ".";
                return false;
            }

            if (!command_options.TryGetValue(command, out string[] allowed))
            {
                error = $"Unknown command '{command}'. Commands: {string.Join(", ", command_options.Keys)}.";
                return false;
            }

            // Every -vd argument has to be one this command reads: a typo must not run the command with
            // a default the caller did not choose (a deploy without -vdDryRun, say).
            foreach (string arg in flags.Concat(values.Keys))
            {
                if (arg == "-vdCommand" || arg == "-vdTimeoutMinutes" || arg == "-vdResultFile")
                    continue;

                if (!allowed.Contains(arg))
                {
                    error = $"{arg} is not an option of '{command}'." +
                            (allowed.Length > 0 ? $" Options: {string.Join(", ", allowed)}." : " It takes no options.");
                    return false;
                }
            }

            if (values.TryGetValue("-vdTimeoutMinutes", out string timeout) && !(int.TryParse(timeout, out int minutes) && minutes > 0))
            {
                error = $"-vdTimeoutMinutes takes a positive number of minutes: '{timeout}' is not one.";
                return false;
            }

            options.Renew = flags.Contains("-vdRenew");
            options.Tenant = flags.Contains("-vdTenant");
            options.Build = flags.Contains("-vdBuild");
            options.DryRun = flags.Contains("-vdDryRun");
            options.AllowTracked = flags.Contains("-vdAllowTracked");
            options.NoBrowser = flags.Contains("-vdNoBrowser");

            options.ConfigFile = Value("-vdConfigFile");
            options.ConfigFolder = Value("-vdConfigFolder");
            options.Env = Value("-vdEnv");
            options.Scenes = Value("-vdScenes");
            options.ExpectTenant = Value("-vdExpectTenant")?.Trim();
            options.Connection = Value("-vdConnection")?.Trim();
            options.App = Value("-vdApp")?.Trim();

            if (values.TryGetValue("-vdLoginTimeoutSeconds", out string loginTimeout))
            {
                if (!(int.TryParse(loginTimeout, out int seconds) && seconds > 0))
                {
                    error = $"-vdLoginTimeoutSeconds takes a positive number of seconds: '{loginTimeout}' is not one.";
                    return false;
                }

                options.LoginTimeoutSeconds = seconds;
            }

            if (command == "login" && options.Connection != null && options.App != null)
            {
                error = "login takes -vdConnection or -vdApp, not both.";
                return false;
            }

            if (values.ContainsKey("-vdExpectTenant") && string.IsNullOrEmpty(options.ExpectTenant))
            {
                error = "-vdExpectTenant needs a value.";
                return false;
            }

            string worlds = Value("-vdWorlds");
            if (worlds != null)
            {
                options.Worlds = new List<int>();
                foreach (string part in worlds.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                {
                    if (!int.TryParse(part, out int id))
                    {
                        error = $"-vdWorlds takes world ids, separated by commas: '{part}' is not one.";
                        return false;
                    }

                    if (!options.Worlds.Contains(id))
                        options.Worlds.Add(id);
                }

                if (options.Worlds.Count == 0)
                {
                    error = "-vdWorlds names no world.";
                    return false;
                }
            }

            if (command == "login" && options.Env != null && options.App == null)
            {
                error = "-vdEnv names an environment of a configuration file: give the file's app with -vdApp.";
                return false;
            }

            if (command == "tenant-config" && string.IsNullOrEmpty(options.ConfigFile))
            {
                error = "tenant-config needs -vdConfigFile <path>.";
                return false;
            }

            if (command == "preview" || command == "deploy")
            {
                if (options.Worlds == null && !options.Tenant)
                {
                    error = $"{command} needs -vdWorlds <ids> or -vdTenant: the target is always explicit.";
                    return false;
                }

                if (options.Worlds != null && options.Tenant)
                {
                    error = $"{command} takes -vdWorlds or -vdTenant, not both: run it once for each.";
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Commands

        private static async Task<int> ExecuteAsync(string[] args)
        {
            if (!TryParseArguments(args, out Options options, out string error))
            {
                Debug.LogError(log_prefix + error);
                report["error"] = error;
                return ExitBadArguments;
            }

            targetKey = EditorLoginState.DefaultConnectionKey;

            switch (command)
            {
                case "status": return Status();
                case "session": return await SessionAsync(options);
                case "login":
                case "logout":
                case "connections":
                case "whoami":
                    return await ConnectionCommandAsync(options);
                case "tenant-config": return TenantConfig(options);
                case "worlds": return await WorldsAsync(options);
                case "build": return await BuildAsync(options);
                case "preview": return await PreviewAsync(options);
                default: return await DeployAsync(options);
            }
        }

        private static int Status()
        {
            report["session"] = DescribeSession();
            report["unityVersion"] = Application.unityVersion;
            report["activeBuildTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString();

            report["addressables"] = new Dictionary<string, object>
            {
                { "configured", AddressablesConfiguration.IsConfigured },
                { "settingsPresent", AssetDatabase.LoadAssetAtPath<ScriptableObject>(AddressablesBuildScript.settings_asset) != null },
            };

            SceneListScriptableObject list = SceneListScriptableObject.Find();
            report["sceneList"] = new Dictionary<string, object>
            {
                { "present", list != null },
                { "path", list != null ? AssetDatabase.GetAssetPath(list) : null },
            };
            report["scenes"] = (list?.SceneConfigurations ?? new List<SceneConfiguration>())
                .Where(c => c.Scene != null)
                .Select(c => new Dictionary<string, object>
                {
                    { "name", c.Scene.name },
                    { "path", AssetDatabase.GetAssetPath(c.Scene) },
                    { "addressName", c.SceneNameFiltered },
                    { "includeInBuild", c.IncludeInBuild },
                    { "platforms", c.SupportedPlatforms.ToString() },
                    { "buildTargets", c.GetRequiredBuildTargets().Select(t => t.ToString()).ToList() },
                    { "missingModules", c.GetMissingBuildTargets().Select(t => t.ToString()).ToList() },
                }).ToList();

            report["interpreter"] = new Dictionary<string, object>
            {
                { "installed", EnvironmentsBuild.InterpreterInstalled },
                { "hasScripts", EnvironmentsBuild.HasInterpretedScripts },
                { "setupIssue", EnvironmentsBuild.InterpreterSetupIssue },
            };

            Debug.Log(log_prefix + "status: " + ((Dictionary<string, object>)report["session"])["description"]);
            return ExitDone;
        }

        private static async Task<int> SessionAsync(Options options)
        {
            report["session"] = DescribeSession();

            if (!options.Renew)
                return ExitDone;

            int code = await RequireSessionAsync();
            report["renewed"] = code == ExitDone;
            return code;
        }

        /// <summary>
        /// <c>login</c>, <c>logout</c>, <c>connections</c> and <c>whoami</c>: the same code the open editor runs for a
        /// request file (<see cref="ConnectionCommands"/>), with the REPORT as the answer.
        /// </summary>
        private static async Task<int> ConnectionCommandAsync(Options options)
        {
            ConnectionCommandRequest request = new()
            {
                Command = command,
                Connection = options.Connection,
                App = options.App,
                Env = options.Env,
                NoBrowser = options.NoBrowser,
                LoginTimeoutSeconds = options.LoginTimeoutSeconds,
            };

            ConnectionCommandResult result = await ConnectionCommands.ExecuteAsync(request, WriteProgress);

            foreach (KeyValuePair<string, object> field in result.Fields)
                report[field.Key] = field.Value;

            report["result"] = result.Status;
            if (result.Error != null)
            {
                report["error"] = result.Error;
                Debug.LogError(log_prefix + result.Error);
            }

            return result.Status switch
            {
                "done" => ExitDone,
                "login-required" => ExitLoginRequired,
                "connection-unknown" => ExitConnectionUnknown,
                "login-timeout" => ExitLoginTimeout,
                "bad-request" => ExitBadArguments,
                "store-not-persistent" => ExitStoreNotPersistent,
                _ => ExitLoginFailed,
            };
        }

        /// <summary>
        /// A login waiting for the browser tells whoever started it, before it ends: the URL goes to the log and,
        /// with <c>-vdResultFile</c>, to the file as <c>state: awaiting-browser</c>, which the final REPORT replaces.
        /// </summary>
        private static void WriteProgress(Dictionary<string, object> progress)
        {
            Debug.Log($"{log_prefix}AUTH_URL {progress["authUrl"]}");

            if (string.IsNullOrEmpty(resultFile))
                return;

            try
            {
                Dictionary<string, object> state = new() { { "command", command } };
                foreach (KeyValuePair<string, object> field in progress)
                    state[field.Key] = field.Value;

                WriteResultFile(JsonConvert.SerializeObject(state, Formatting.Indented, report_settings));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(log_prefix + "The progress could not be written to the result file: " + ex.Message);
            }
        }

        private static int TenantConfig(Options options)
        {
            try
            {
                AppConfigurationImporter.ImportResult result = AppConfigurationImporter.Import(
                    options.ConfigFile, options.Env, options.ConfigFolder ?? AppConfigurationImporter.DefaultFolder,
                    allowTracked: options.AllowTracked);

                // The file's path in the project and the names it carries: nothing else of its content.
                report["tenantConfig"] = new Dictionary<string, object>
                {
                    { "assetPath", result.AssetPath },
                    { "settingsPath", result.SettingsPath },
                    { "app", result.App },
                    { "env", result.Env },
                    { "envs", result.Envs },
                    { "addedToAppAssets", result.AddedToAppAssets },
                    { "replacedExisting", result.ReplacedExisting },
                };
                Debug.Log($"{log_prefix}tenant-config: {result.AssetPath} ({result.App} / {result.Env}) is selected. " +
                          "Log in with -vdCommand login (or from Virtuademy > Available Tenants) to use it.");
                return ExitDone;
            }
            catch (AppConfigurationImporter.SecretNotIgnoredException ex)
            {
                string message = ex.Message + " Nothing was written. -vdAllowTracked imports it anyway.";
                Debug.LogError(log_prefix + "tenant-config: " + message);
                report["error"] = message;
                report["notIgnoredPath"] = ex.AssetPath;
                return ExitSecretNotIgnored;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError(log_prefix + "tenant-config: " + ex.Message);
                report["error"] = ex.Message;
                return ExitTenantConfigFailed;
            }
        }

        private static async Task<int> WorldsAsync(Options options)
        {
            int code = UseNamedConnection(options.Connection);
            if (code == ExitDone)
                code = RequireConnection();
            if (code != ExitDone)
                return code;

            code = ConfirmTarget(options.Connection, null, required: false);
            if (code != ExitDone)
                return code;

            code = await RequireSessionAsync();
            if (code != ExitDone)
                return code;

            WorldListResult listing = await EnvironmentsDeploy.LoadWorldsAsync(connectionKey: targetKey);
            if (listing.Status != WorldListStatus.Ok)
                return WorldListFailed(listing);

            report["worlds"] = listing.Visible.Select(w => new Dictionary<string, object>
            {
                { "id", w.Id },
                { "label", w.Label },
                { "canDeploy", listing.Deployable.Any(d => d.Id == w.Id) },
            }).ToList();
            report["tenantManager"] = ConnectionStore.Get(targetKey)?.IsTenantManager ?? false;

            // Where each scene is already published: the index the window's rows read.
            await PublishedEnvironmentsIndex.RefreshAsync(listing.Visible.Select(w => (w.Id, w.Label)), targetKey);
            report["publishedKnown"] = PublishedEnvironmentsIndex.HasDataFor(targetKey);
            report["tenantLevelVisible"] = PublishedEnvironmentsIndex.HasDataFor(targetKey) && !PublishedEnvironmentsIndex.TenantUnknownFor(targetKey);

            SceneListScriptableObject list = SceneListScriptableObject.Find();
            report["scenes"] = EnvironmentsBuild.IncludedScenes(list).Select(c =>
            {
                IReadOnlyList<PublishedEnvironmentsIndex.Entry> entries = PublishedEnvironmentsIndex.Get(targetKey, c.SceneNameFiltered);
                return new Dictionary<string, object>
                {
                    { "name", c.Scene.name },
                    { "addressName", c.SceneNameFiltered },
                    { "publishedAtTenant", entries.Any(e => e.Tenant) },
                    { "publishedInWorlds", entries.Where(e => !e.Tenant).Select(e => new Dictionary<string, object>
                        { { "id", e.WorldId }, { "label", e.WorldLabel } }).ToList() },
                };
            }).ToList();

            return ExitDone;
        }

        private static async Task<int> BuildAsync(Options options)
        {
            int code = SelectScenes(options, out List<SceneConfiguration> scenes);
            if (code != ExitDone)
                return code;

            bool sessionUsable = await TryRenewSessionAsync();
            return await BuildStageAsync(scenes, sessionUsable);
        }

        private static async Task<int> PreviewAsync(Options options)
        {
            int code = SelectScenes(options, out List<SceneConfiguration> scenes);
            if (code != ExitDone)
                return code;

            code = UseNamedConnection(options.Connection);
            if (code == ExitDone)
                code = RequireConnection();
            if (code != ExitDone)
                return code;

            code = ConfirmTarget(options.Connection, options.ExpectTenant, required: false);
            if (code != ExitDone)
                return code;

            code = await RequireSessionAsync();
            if (code != ExitDone)
                return code;

            (int Code, WorldListResult Listing) targets = await LoadDeployTargetsAsync(options);
            if (targets.Code != ExitDone)
                return targets.Code;

            WorldListResult listing = targets.Listing;

            await PublishedEnvironmentsIndex.RefreshAsync(listing.Visible.Select(w => (w.Id, w.Label)), targetKey);
            if (!PublishedEnvironmentsIndex.HasDataFor(targetKey))
            {
                string message = "What is already published could not be read from the platform.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitPlatformUnreachable;
            }

            List<Dictionary<string, object>> previews = new();
            foreach (int? target in options.Tenant ? new List<int?> { null } : options.Worlds.Select(id => (int?)id))
            {
                DeployPreview preview = EnvironmentsDeploy.Preview(scenes, target, targetKey);
                previews.Add(new Dictionary<string, object>
                {
                    { "target", target == null ? "tenant" : "world" },
                    { "worldId", target },
                    { "label", target == null ? "Tenant level" : listing.Deployable.First(w => w.Id == target).Label },
                    { "new", preview.Added },
                    { "updated", preview.Updated },
                    { "note", target == null && PublishedEnvironmentsIndex.TenantUnknownFor(targetKey)
                        ? "Tenant-level environments are not visible to your account: every scene is listed as new." : null },
                });
            }

            report["previews"] = previews;
            return ExitDone;
        }

        private static async Task<int> DeployAsync(Options options)
        {
            // Everything that can be refused locally comes first, cheapest first: nobody should wait for a
            // build to be told the session is gone or the world is not theirs.
            int code = SelectScenes(options, out List<SceneConfiguration> scenes);
            if (code != ExitDone)
                return code;

            if (options.Build && !AddressablesConfiguration.IsConfigured)
                return AddressablesNotConfigured();

            code = UseNamedConnection(options.Connection);
            if (code == ExitDone)
                code = RequireConnection();
            if (code != ExitDone)
                return code;

            // Before the session, the build and any request that carries the token: which tenant, which platform. A
            // label that names another tenant is a target mismatch even when that connection has no session.
            code = ConfirmTarget(options.Connection, options.ExpectTenant, required: true);
            if (code != ExitDone)
                return code;

            code = await RequireSessionAsync();
            if (code != ExitDone)
                return code;

            (int Code, WorldListResult Listing) targets = await LoadDeployTargetsAsync(options);
            if (targets.Code != ExitDone)
                return targets.Code;

            WorldListResult listing = targets.Listing;

            if (options.Tenant)
            {
                Debug.LogWarning(log_prefix + "Tenant level: the interpreted scripts of this project are NOT uploaded. " +
                                 "An environment deployed at tenant level runs without scripts.");
                report["tenantLevelScripts"] = "not uploaded: environments deployed at tenant level run without scripts";
            }

            if (options.Build)
            {
                code = await BuildStageAsync(scenes, sessionUsable: true);
                if (code != ExitDone)
                    return code;
            }

            List<string> names = scenes.Select(s => s.SceneNameFiltered).ToList();
            List<string> missingZips = names.Where(n => !File.Exists(Path.Combine(EnvironmentsBuild.OutputFolder, n + ".zip"))).ToList();
            if (missingZips.Count > 0)
            {
                string message = $"Missing zip files for: {string.Join(", ", missingZips)}. Build first (-vdBuild, or the build command).";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitZipsMissing;
            }

            if (options.DryRun)
            {
                report["dryRun"] = true;
                report["plan"] = new Dictionary<string, object>
                {
                    { "targets", options.Tenant
                        ? new List<string> { "tenant" }
                        : options.Worlds.Select(id => $"{id} ({listing.Deployable.First(w => w.Id == id).Label})").ToList() },
                    { "scenes", names },
                    { "interpretedAssemblies", options.Tenant
                        ? new List<string>()
                        : names.Select(n => EnvironmentsBuild.ReadDeclaredAssembly(n, out _)).Where(a => a != null).Distinct().ToList() },
                };
                Debug.Log(log_prefix + "Dry run: nothing was uploaded or imported.");
                return ExitDone;
            }

            DeployObserver observer = new()
            {
                Error = message => Debug.LogError(log_prefix + message),
                Progress = (title, info, _) => Debug.Log($"{log_prefix}{title}: {info}"),

                // The live report goes into ours at once: whatever ends the run from here on (the ceiling, an
                // exception) prints it as it stands.
                Started = live => report["deploy"] = live,

                // Asked between steps, never inside one. A run that already ended (the timeout fired outside a
                // deploy and the editor is still shutting down) must not start a new step either.
                StopReason = () => finished
                    ? "The run had already ended."
                    : DateTime.UtcNow >= deadlineUtc ? $"The time limit ({timeoutMinutes} minute(s)) passed." : null,
            };

            DeployReport deployed;
            deployActive = true;
            try
            {
                deployed = options.Tenant
                    ? await EnvironmentsDeploy.DeployToTenantAsync(names, observer, targetKey)
                    : await EnvironmentsDeploy.DeployToWorldsAsync(options.Worlds, listing.Deployable, names, observer, targetKey);
            }
            finally
            {
                deployActive = false;
            }

            report["deploy"] = deployed;
            Debug.Log($"{log_prefix}deploy: the platform started {deployed.ImportsStarted} of {deployed.Expected} scene import(s) " +
                      $"(the imports run on the platform afterwards), {deployed.Problems} problem(s).");

            if (deployed.Stopped != null)
            {
                report["error"] = deployed.Stopped;
                return ExitTimeout;
            }

            return deployed.Succeeded ? ExitDone : ExitDeployProblems;
        }

        #endregion

        #region Shared steps

        /// <summary>
        /// The scenes the command works on: those included in the build, or the ones <c>-vdScenes</c> names
        /// among them. Reads the scene list; creates nothing.
        /// </summary>
        private static int SelectScenes(Options options, out List<SceneConfiguration> scenes)
        {
            scenes = new List<SceneConfiguration>();

            SceneListScriptableObject list = SceneListScriptableObject.Find();
            if (list == null)
            {
                string message = "The project has no scene list yet. Open Virtuademy > Addressables management once to create it, " +
                                 "then tick \"Include in build\" on the scenes to publish.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitNothingToBuild;
            }

            List<SceneConfiguration> included = EnvironmentsBuild.IncludedScenes(list);
            if (included.Count == 0)
            {
                string message = "No scene is included in the build: tick \"Include in build\" on the scenes to publish " +
                                 $"(scene list: {AssetDatabase.GetAssetPath(list)}).";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitNothingToBuild;
            }

            if (options.Scenes == null)
            {
                scenes = included;
            }
            else
            {
                List<string> unknown = new();
                foreach (string name in options.Scenes.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                {
                    SceneConfiguration match = included.FirstOrDefault(c =>
                        string.Equals(c.Scene.name, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.SceneNameFiltered, name, StringComparison.OrdinalIgnoreCase));

                    if (match == null)
                        unknown.Add(name);
                    else if (!scenes.Contains(match))
                        scenes.Add(match);
                }

                if (unknown.Count > 0 || scenes.Count == 0)
                {
                    string message = $"-vdScenes names scenes that are not included in the build: {string.Join(", ", unknown)}. " +
                                     $"Included: {string.Join(", ", included.Select(c => c.Scene.name))}.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    scenes = new List<SceneConfiguration>();
                    return ExitNothingToBuild;
                }
            }

            report["scenes"] = scenes.Select(c => new Dictionary<string, object>
            {
                { "name", c.Scene.name },
                { "addressName", c.SceneNameFiltered },
            }).ToList();
            return ExitDone;
        }

        private static int AddressablesNotConfigured()
        {
            string message = "The project's Addressables are not configured the way the SDK needs, so a build would fail at the end. " +
                             "Apply the configuration with \"Fix Addressables configurations\" in Virtuademy > Addressables management, " +
                             "or with the setup command line (SetupCli, -vdSetupSteps addressables); it resets the Addressables groups " +
                             "to a single default group. Nothing was built.";
            Debug.LogError(log_prefix + message);
            report["addressablesConfigured"] = false;
            report["error"] = message;
            return ExitAddressablesNotConfigured;
        }

        /// <summary>
        /// The window's <b>Build Addressables</b> after its checks: the Addressables configuration, the
        /// interpreted scripts' verification, the build and the zips.
        /// </summary>
        private static async Task<int> BuildStageAsync(List<SceneConfiguration> scenes, bool sessionUsable)
        {
            if (!AddressablesConfiguration.IsConfigured)
                return AddressablesNotConfigured();

            bool hasScripts = EnvironmentsBuild.HasInterpretedScripts;
            bool verified = await EnvironmentsBuild.VerifyInterpretedScriptsAsync(targetKey);

            report["interpreter"] = new Dictionary<string, object>
            {
                { "installed", EnvironmentsBuild.InterpreterInstalled },
                { "hasScripts", hasScripts },
                // Null when there was nothing to verify.
                { "verified", hasScripts ? verified : (bool?)null },
                { "bundleIsCurrent", EnvironmentsBuild.InterpretedBundleIsCurrent },
            };

            if (!verified)
            {
                // The server's verification needs a session, and without one it is refused rather than skipped.
                if (hasScripts && !sessionUsable)
                {
                    string message = "The interpreted scripts could not be verified: the server check needs a session, and there is none " +
                                     "that can be renewed silently. " + LoginInstruction;
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    return ExitLoginRequired;
                }

                string refused = "The interpreted scripts did not pass the security checks (see the log above). Nothing was built.";
                Debug.LogError(log_prefix + refused);
                report["error"] = refused;
                return ExitScriptsRefused;
            }

            EnvironmentsBuildResult result = EnvironmentsBuild.BuildAndZip(scenes);

            report["build"] = new Dictionary<string, object>
            {
                { "success", result.Success },
                { "failure", result.Failure.ToString() },
                { "message", result.Message },
                { "firstpassComponents", result.FirstpassComponents },
                { "scenes", result.Scenes },
                { "scriptsAssembly", result.ScriptsAssembly },
                { "scriptsArchive", result.ScriptsArchive },
                { "activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString() },
            };

            if (!result.Success)
            {
                Debug.LogError($"{log_prefix}The build failed ({result.Failure}). {result.Message}");

                // The window's help box says the same: Addressables wrote a binary catalog, which the platform
                // cannot import. The "Enable Json Catalog" setting is on (the configuration check passes) but
                // Addressables only acts on it once its ENABLE_JSON_CATALOG scripting define is there, and
                // switching the setting in its settings window is what adds the define.
                if (result.Failure == EnvironmentsBuildFailure.BinaryCatalog)
                    Debug.LogError(log_prefix + "Addressables built a binary catalog. In Window > Asset Management > Addressables > Settings, " +
                                   "untick \"Enable Json Catalog\", tick it again, save the project, restart the editor and build again.");

                return ExitBuildFailed;
            }

            return ExitDone;
        }

        private const string LoginInstruction = "Log in with -vdCommand login (or from Virtuademy > Available Tenants, the Login button), then run the command again.";

        /// <summary>The login instruction for the connection the run acts on, naming it when it is not the default one.</summary>
        private static string LoginInstructionFor(string connectionKey) =>
            connectionKey == null || string.Equals(connectionKey, ProjectConnections.DefaultKey, StringComparison.OrdinalIgnoreCase)
                ? LoginInstruction
                : $"Log in with -vdCommand login -vdConnection \"{connectionKey}\", then run the command again.";

        /// <summary>Silent renewal only; false when the session cannot be used. Says nothing.</summary>
        private static async Task<bool> TryRenewSessionAsync()
        {
            report["session"] = DescribeSession();
            if (ConnectionStore.Get(targetKey)?.HasSession != true)
                return false;

            bool usable = await EditorSessionManager.EnsureValidTokenAsync(targetKey, allowInteractive: false);
            report["session"] = DescribeSession();
            return usable;
        }

        /// <summary>
        /// <c>worlds</c>, <c>preview</c> and <c>deploy</c> act on the connection <c>-vdConnection</c> names, whether or not it
        /// is the project's default one; without it they keep the default one. The key carries the Configuration-API
        /// host, so naming it tells apart a tenant that two instances may both call "Acme". Run before the session: a
        /// connection that is not in the store, or a name that fits several, is <see cref="ExitConnectionUnknown"/>.
        /// </summary>
        private static int UseNamedConnection(string connection)
        {
            if (string.IsNullOrEmpty(connection))
                return ExitDone;

            ConnectionRecord named = ConnectionStore.Find(connection, out string findError);
            if (named == null)
            {
                Debug.LogError(log_prefix + findError);
                report["error"] = findError;
                return ExitConnectionUnknown;
            }

            targetKey = named.Key;
            return ExitDone;
        }

        /// <summary>
        /// A command that names no connection needs the project's default one. When there is none (the project uses
        /// several connections and none was chosen, or the chosen one was removed from the store) it is
        /// <see cref="ExitConnectionUnknown"/>, not a request to log in.
        /// </summary>
        private static int RequireConnection()
        {
            if (targetKey != null)
                return ExitDone;

            string message = "The project has no default connection: name one with -vdConnection (the connections command lists them), " +
                             "or choose one in Virtuademy > Available Tenants.";
            Debug.LogError(log_prefix + message);
            report["error"] = message;
            return ExitConnectionUnknown;
        }

        /// <summary>The session, renewed silently if it expired; <see cref="ExitLoginRequired"/> with the instruction when it cannot be.</summary>
        private static async Task<int> RequireSessionAsync()
        {
            if (await TryRenewSessionAsync())
                return ExitDone;

            string message = ConnectionStore.Get(targetKey)?.HasSession != true
                ? "Not logged in. " + LoginInstructionFor(targetKey)
                : "The session expired and could not be renewed silently. " + LoginInstructionFor(targetKey);
            Debug.LogError(log_prefix + message);
            report["error"] = message;
            return ExitLoginRequired;
        }

        /// <summary>
        /// Says which tenant, which environment and which platform a command that calls the API works
        /// against (in the REPORT, before anything is sent), and refuses when that is not what the caller
        /// named or what the login belongs to. <paramref name="expectTenant"/> is compared case-insensitively with
        /// the tenant label of the connection the run acts on, and a different one is <see cref="ExitTargetMismatch"/>;
        /// <paramref name="required"/> turns a missing one into <see cref="ExitTenantNotConfirmed"/> (deploy).
        /// The Application API the command calls is the one that connection's tenant reported at login
        /// (<see cref="EditorApiEndpoint"/>), so the token cannot go to another platform than its own.
        /// </summary>
        private static int ConfirmTarget(string connection, string expectTenant, bool required)
        {
            ConnectionRecord record = ConnectionStore.Get(targetKey);
            Tenant tenant = record?.Tenant;
            string label = tenant?.Label ?? record?.TenantLabel;
            string callHost = HostOf(EditorApiEndpoint.ApplicationApiUrlOf(targetKey));

            report["target"] = new Dictionary<string, object>
            {
                { "connection", targetKey },
                { "default", targetKey != null && string.Equals(targetKey, ProjectConnections.DefaultKey, StringComparison.OrdinalIgnoreCase) },
                { "configApiHost", HostOf(record?.ConfigApiBaseUrl) },
                { "tenant", label },
                { "env", tenant?.Env.ToString() },
                { "apiHost", callHost },
                { "expectedTenant", expectTenant },
                { "expectedConnection", connection },
            };

            string session = $"The connection this command acts on, '{targetKey ?? "none"}', is tenant '{label}' ({tenant?.Env}), API {callHost ?? "unknown"}.";

            if (expectTenant == null && required)
            {
                string message = $"{command} needs -vdExpectTenant <label>: the label of the tenant it deploys to, as whoami shows it. " +
                                 $"{session} Nothing was built or sent.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitTenantNotConfirmed;
            }

            // No connection at all: there is nothing to compare with, and the session check that follows says to log in.
            if (expectTenant != null && record != null && !string.Equals(expectTenant, label, StringComparison.OrdinalIgnoreCase))
            {
                string message = $"-vdExpectTenant says '{expectTenant}', but the connection this command acts on, '{targetKey}', is tenant '{label}'. " +
                                 "Nothing was built or sent. Name the connection you mean with -vdConnection (the connections command lists them), or correct the label.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                return ExitTargetMismatch;
            }

            Debug.Log($"{log_prefix}target: tenant '{label}' ({tenant?.Env}), API {callHost ?? "unknown"}.");
            return ExitDone;
        }

        /// <summary>The host of a URL, with its port when it is not the scheme's own; null for none. Never the path or credentials.</summary>
        private static string HostOf(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ? uri.Authority.ToLowerInvariant() : null;

        /// <summary>
        /// Reads the worlds and checks the command's target against them: every <c>-vdWorlds</c> id must be one the
        /// login can deploy to, and a tenant target needs a TenantManager. Refuses before anything is built or sent.
        /// </summary>
        private static async Task<(int Code, WorldListResult Listing)> LoadDeployTargetsAsync(Options options)
        {
            WorldListResult listing = new();

            if (options.Tenant)
            {
                if (ConnectionStore.Get(targetKey)?.IsTenantManager != true)
                {
                    string message = "Deploying at tenant level needs a TenantManager login; this one is not.";
                    Debug.LogError(log_prefix + message);
                    report["error"] = message;
                    return (ExitNotTenantManager, listing);
                }

                // Tenant level is not a world: there is no world to look up.
                return (ExitDone, listing);
            }

            listing = await EnvironmentsDeploy.LoadWorldsAsync(connectionKey: targetKey);
            if (listing.Status != WorldListStatus.Ok)
                return (WorldListFailed(listing), listing);

            List<int> notDeployable = options.Worlds.Where(id => listing.Deployable.All(w => w.Id != id)).ToList();
            if (notDeployable.Count > 0)
            {
                string message = $"This login cannot deploy to world(s) {string.Join(", ", notDeployable)}. " +
                                 $"Deployable: {(listing.Deployable.Count == 0 ? "none" : string.Join(", ", listing.Deployable.Select(w => $"{w.Id} ({w.Label})")))}. " +
                                 "Nothing was built or sent.";
                Debug.LogError(log_prefix + message);
                report["error"] = message;
                report["deployableWorlds"] = listing.Deployable.Select(w => new Dictionary<string, object> { { "id", w.Id }, { "label", w.Label } }).ToList();
                return (ExitWorldNotDeployable, listing);
            }

            return (ExitDone, listing);
        }

        private static int WorldListFailed(WorldListResult listing)
        {
            Debug.LogError(log_prefix + listing.Message);
            report["error"] = listing.Message;
            return listing.Status == WorldListStatus.SessionRequired ? ExitLoginRequired : ExitPlatformUnreachable;
        }

        /// <summary>
        /// The session of the connection the run acts on (the default one for <c>status</c>, <c>session</c> and
        /// <c>build</c>), as a status label would show it. Never the token.
        /// </summary>
        private static Dictionary<string, object> DescribeSession()
        {
            ConnectionRecord record = ConnectionStore.Get(targetKey);
            bool loggedIn = record?.HasSession ?? false;
            string description = loggedIn
                ? $"logged in{EditorSessionManager.DescribeSession(record, out _)}"
                : "not logged in";

            return new Dictionary<string, object>
            {
                { "connection", record?.Key },
                { "default", record != null && string.Equals(record.Key, ProjectConnections.DefaultKey, StringComparison.OrdinalIgnoreCase) },
                { "loggedIn", loggedIn },
                { "tenant", record?.Tenant?.Label },
                { "app", string.IsNullOrEmpty(record?.App) ? null : record.App },
                { "env", string.IsNullOrEmpty(record?.Env) ? null : record.Env },
                { "user", string.IsNullOrEmpty(record?.Username) ? null : record.Username },
                { "tenantManager", record?.IsTenantManager ?? false },
                { "tokenValid", record?.IsTokenValid ?? false },
                { "tokenRejected", record?.TokenRejected ?? false },
                { "tokenExpiresUtc", record?.ExpiryUtc?.ToString("o") },
                { "usableUntilUtc", record?.UsableUntilUtc?.ToString("o") },
                { "canRenew", record?.CanRenew ?? false },
                { "interactiveLoginAllowed", EditorSessionManager.InteractiveLoginAllowed },
                { "description", description },
            };
        }

        private static string Describe(int code) => code switch
        {
            ExitDone => "done",
            ExitUnexpected => "unexpected failure",
            ExitBadArguments => "bad arguments",
            ExitLoginRequired => "login required",
            ExitAddressablesNotConfigured => "addressables not configured",
            ExitNothingToBuild => "nothing to build",
            ExitBuildFailed => "build failed",
            ExitScriptsRefused => "interpreted scripts refused",
            ExitWorldNotDeployable => "world not deployable",
            ExitDeployProblems => "deploy finished with problems",
            ExitNotTenantManager => "not a tenant manager",
            ExitTenantConfigFailed => "configuration file not imported",
            ExitPlatformUnreachable => "platform unreachable",
            ExitTimeout => "timeout",
            ExitZipsMissing => "zips missing",
            ExitSecretNotIgnored => "configuration file would not be ignored by git",
            ExitTenantNotConfirmed => "tenant not confirmed: -vdExpectTenant missing",
            ExitApiHostMismatch => "API host does not match the logged-in tenant",
            ExitTargetMismatch => "target mismatch: -vdExpectTenant is not the tenant of the connection the command acts on",
            ExitConnectionUnknown => "connection unknown",
            ExitLoginTimeout => "login timed out",
            ExitLoginFailed => "login failed",
            ExitStoreNotPersistent => "connection store not persistent on this OS",
            _ => "unknown",
        };

        #endregion
    }
}
