using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEditor;
using UnityEditor.Compilation;

using UnityEditorInternal;

using UnityEngine;

namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    public static class HotUpdateSetupper
    {
        /// <summary>
        /// Every target a bundle must carry. The backend rejects an import that misses one, so
        /// this list and its RequiredPlatforms are one contract in two places: iOS is here
        /// because a world that loses its scripted behaviour on iPad only is worse than a
        /// publish that fails while the creator is watching.
        /// </summary>
        static readonly BuildTarget[] TARGETS = {
            BuildTarget.StandaloneWindows64,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL
        };

        /// <summary>
        /// The assembly name every creator project compiles its scripts under, and so the only one
        /// the scenes reference and the only one the published DLL carries in its metadata.
        /// </summary>
        /// <remarks>
        /// Unity binds a scene's script by the assembly name in its MonoScript, and only accepts
        /// names that were in the player's scripting-assembly list when the player was built. No
        /// player can list one name per environment, so a scene that referenced a name of its own
        /// came up with every scripted component missing although the DLL loaded fine. The player
        /// lists this one name, loads every environment's assembly under it, and its IL2CPP answers
        /// for it from the environment being loaded. Must equal Virtuademy.Core.ScriptAlias.Name in
        /// the player (Virtuademy-Unity): a creator project and a player that disagree on it bring
        /// the missing scripts back.
        /// </remarks>
        public const string ScriptAlias = "VirtuademyEnvironmentScripts";

        /// <summary>The folder the creator writes interpreted scripts in, named after the alias.</summary>
        const string HOTUPDATE_FOLDER = "Assets/" + ScriptAlias;

        /// <summary>
        /// Where the scripts lived before the folder took the alias's name. Only <see cref="RunSetup"/>
        /// looks at it, to move it; nothing else treats it as a hot-update folder.
        /// </summary>
        const string LEGACY_HOTUPDATE_FOLDER = "Assets/HotUpdate";

        /// <summary>Start of every published DLL's file and record name.</summary>
        const string PUBLISHED_PREFIX = "EnvironmentsDll_";

        /// <summary>
        /// The assemblies with an asmdef that the baseline whitelist (<c>policy.json</c>,
        /// <c>allowedAssemblies</c>) admits, and therefore the ones the hot-update asmdef should
        /// carry from the moment it exists.
        /// </summary>
        /// <remarks>
        /// A custom asmdef does not get the automatic references of Assembly-CSharp, so without
        /// these a script that the publish would accept does not compile, and the error names a
        /// missing assembly without saying which one is allowed. Every entry is guaranteed to be
        /// in the project by this package's own dependencies (spacs-dialogs, spacs-tasks,
        /// com.unity.ugui, which ships TextMeshPro), so none of them can dangle. The rest of the
        /// whitelist needs no asmdef reference: the BCL and the UnityEngine modules come with the
        /// engine references. Keep this list in step with the baseline policy: an assembly added
        /// there is one creators would otherwise have to reference by hand, and one removed there
        /// compiles scripts the publish rejects. Operator overrides (ADR 0019) are not followed.
        /// </remarks>
        static readonly string[] DEFAULT_REFERENCES = {
            "Virtuademy.ScriptingApi",
            "Virtuademy.Environments.ScriptingApi",
            "SPACS.Dialogs",
            "SPACS.Tasks",
            "Unity.TextMeshPro",
            "UnityEngine.UI",
        };

        // HybridCLR ships with its gitee mirrors as the default, which do not resolve outside
        // China. Every fresh creator project would fail its first install on a DNS error
        // that says nothing about the cause, so the known defaults are swapped for the GitHub
        // originals. A URL the team deliberately changed (a private mirror, say) is left alone.
        const string GITEE_HYBRIDCLR = "https://gitee.com/focus-creative-games/hybridclr";
        const string GITEE_IL2CPP_PLUS = "https://gitee.com/focus-creative-games/il2cpp_plus";
        const string GITHUB_HYBRIDCLR = "https://github.com/focus-creative-games/hybridclr";
        const string GITHUB_IL2CPP_PLUS = "https://github.com/focus-creative-games/il2cpp_plus";

        /// <summary>
        /// Session flag raised by the setup window (Virtuademy/Setup/Setup project) before it installs HybridCLR, so
        /// that <see cref="OnReloadAfterInstall"/> can finish the job on the domain reload that
        /// follows the package import. Must stay in sync with the window's own copy of the key.
        /// </summary>
        public const string PENDING_SETUP_KEY = "PENDING_HYBRIDCLR_SETUP";

        // A refusal, held for the next domain reload. With the Console's "Clear on Recompile"
        // enabled — its default — the recompilation that follows the creator's first edit wipes
        // the very violations they need in order to fix their script, leaving a dialog that says
        // the checks failed and no way to find out what failed. A verdict that cannot be read is
        // not a verdict.
        const string PENDING_REFUSAL_KEY = "PENDING_HOTUPDATE_REFUSAL";

        /// <summary>
        /// Prefix every assembly this project publishes shares: the project GUID keeps two
        /// different projects from ever publishing under the same file and record name.
        /// </summary>
        public static string ProjectAssemblyPrefix => PUBLISHED_PREFIX + PlayerSettings.productGUID + "_";

        /// <summary>
        /// Name this project's hot-update code is published under AS IT STANDS: prefix plus a
        /// digest of the source it compiles from. It names the DLL's file, its record on the
        /// platform and its storage folder; inside the DLL the assembly is
        /// <see cref="ScriptAlias"/>, for every project. It changes when the code changes —
        ///
        ///   * the backend keys its record on it, so republishing unchanged code is recognised
        ///     and stored once instead of overwriting anything;
        ///   * two worlds can carry two versions of this project and the player loads each one
        ///     once, keyed on this name;
        ///   * and the DLLs already on disk under this name are, by definition, current.
        ///
        /// Falls back to the prefix alone when the fingerprint cannot be computed (no hot-update
        /// folder yet), which the build gate refuses rather than publishes.
        /// </summary>
        public static string HotUpdateAssemblyName
        {
            get
            {
                string fingerprint = HotUpdateFingerprint.Compute(HOTUPDATE_FOLDER, TARGETS);

                return string.IsNullOrEmpty(fingerprint)
                    ? ProjectAssemblyPrefix
                    : ProjectAssemblyPrefix + fingerprint;
            }
        }

        /// <summary>Asset path the asmdef has: named after <see cref="ScriptAlias"/>.</summary>
        public static string HotUpdateAsmdefPath => $"{HOTUPDATE_FOLDER}/{ScriptAlias}.asmdef";

        /// <summary>
        /// <see cref="TARGETS"/> as the folder/platform names shared with the backend: the
        /// compiler writes each DLL under HotUpdateDlls/&lt;BuildTarget&gt;/ and the bundle keeps
        /// the same segment, so both sides speak one vocabulary.
        /// </summary>
        public static string[] TargetNames => TARGETS.Select(t => t.ToString()).ToArray();

        /// <summary>
        /// Asset path of the asmdef that is actually on disk at the root of the hot-update folder,
        /// or null when there is none — which may not be <see cref="HotUpdateAsmdefPath"/>, and
        /// <see cref="GetSetupIssue"/> has to be able to say so.
        /// </summary>
        public static string ExistingAsmdefPath
        {
            get
            {
                if (!Directory.Exists(HOTUPDATE_FOLDER))
                    return null;

                return Directory.GetFiles(HOTUPDATE_FOLDER, "*.asmdef", SearchOption.TopDirectoryOnly)
                    .Select(p => p.Replace('\\', '/'))
                    .OrderBy(p => p, System.StringComparer.Ordinal)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Re-logs the refusal the gate left behind. Separate from
        /// <see cref="OnReloadAfterInstall"/> because it has nothing to do with the install: the
        /// two just happen to need the same moment, the first frame after the domain is back.
        /// </summary>
        [InitializeOnLoadMethod]
        static void ReplayPendingRefusal()
        {
            string refusal = SessionState.GetString(PENDING_REFUSAL_KEY, string.Empty);
            if (!string.IsNullOrEmpty(refusal))
            {
                SessionState.EraseString(PENDING_REFUSAL_KEY);
                Debug.LogError(refusal);
            }
        }

        /// <summary>
        /// Records why the gate refused, so the reason outlives any recompilation that follows.
        /// Logged now for whoever is watching, and again on the other side of the next reload for
        /// whoever comes back to a cleared Console.
        /// </summary>
        static void Refuse(string reason)
        {
            Debug.LogError(reason);
            SessionState.SetString(PENDING_REFUSAL_KEY, reason);
        }

        /// <summary>How many domain reloads the post-install setup may take before it gives up.</summary>
        const int MAX_PENDING_SETUP_ATTEMPTS = 3;
        const string PENDING_SETUP_ATTEMPTS_KEY = "PENDING_HYBRIDCLR_SETUP_ATTEMPTS";

        /// <summary>
        /// Finishes the setup the window started by installing the HybridCLR package, so that one
        /// click on "Install interpreter" leaves the project ready to build.
        ///
        /// It used to call <see cref="Setup"/> right here, inside [InitializeOnLoadMethod]. Asset
        /// database work is deferred in that context: the asmdef was renamed but could not be
        /// loaded yet, so the HybridCLR registration failed — and the line after it announced a
        /// completed configuration anyway. The creator had to press the button a second time.
        /// Now the setup runs on the first editor tick after the reload, and the pending flag is
        /// only lowered when the setup actually passes: a rename recompiles, so a run that comes
        /// up short is retried on the reload it caused, up to <see cref="MAX_PENDING_SETUP_ATTEMPTS"/>.
        /// </summary>
        [InitializeOnLoadMethod]
        static void OnReloadAfterInstall()
        {
            if (!SessionState.GetBool(PENDING_SETUP_KEY, false))
                return;

            EditorApplication.delayCall += CompletePendingSetup;
        }

        static void CompletePendingSetup()
        {
            int attempt = SessionState.GetInt(PENDING_SETUP_ATTEMPTS_KEY, 0) + 1;
            SessionState.SetInt(PENDING_SETUP_ATTEMPTS_KEY, attempt);

            string issue = RunSetup();

            if (issue == null)
            {
                ClearPendingSetup();
                Debug.Log("[Setup] HybridCLR configured after the install. The project is ready to build interpreted scripts.");
                return;
            }

            if (attempt >= MAX_PENDING_SETUP_ATTEMPTS)
            {
                ClearPendingSetup();
                Debug.LogError($"[Setup] HybridCLR is installed but the configuration did not complete after {attempt} attempts: " +
                               $"{issue} Press \"Install interpreter\" in Virtuademy/Setup/Setup project to retry.");
                return;
            }

            // Kept raised: the next domain reload — the recompilation the rename just started —
            // runs the setup again with the asset imported.
            Debug.Log($"[Setup] HybridCLR configuration continues after the recompilation ({issue})");
        }

        static void ClearPendingSetup()
        {
            SessionState.SetBool(PENDING_SETUP_KEY, false);
            SessionState.EraseInt(PENDING_SETUP_ATTEMPTS_KEY);
        }

        // ============================================================
        //  SETUP — run once to prepare the project
        // ============================================================
        //[MenuItem("Virtuademy/Setup/Interpreted scripting")]
        public static void Setup() => RunSetup();

        /// <summary>Runs the setup and returns what the build gate would still refuse, or null
        /// when the project is ready.</summary>
        static string RunSetup()
        {
            // Step 1: the interpreter itself, inside this Editor's IL2CPP. Nothing below matters
            // if the player is going to be built without it.
            EnsureInterpreterInstalled();

            // Step 2: the hot-update assembly. A project whose scripts are still in the folder used
            // before it took the alias's name gets that folder moved, scripts and all.
            if (!Directory.Exists(HOTUPDATE_FOLDER) && AssetDatabase.IsValidFolder(LEGACY_HOTUPDATE_FOLDER))
            {
                string error = AssetDatabase.MoveAsset(LEGACY_HOTUPDATE_FOLDER, HOTUPDATE_FOLDER);
                if (!string.IsNullOrEmpty(error))
                    return $"{LEGACY_HOTUPDATE_FOLDER} could not be moved to {HOTUPDATE_FOLDER}: {error}";
                Debug.Log($"[Setup] Moved {LEGACY_HOTUPDATE_FOLDER} to {HOTUPDATE_FOLDER}.");
            }

            if (!Directory.Exists(HOTUPDATE_FOLDER))
            {
                Directory.CreateDirectory(HOTUPDATE_FOLDER);
                Debug.Log($"[Setup] Created folder {HOTUPDATE_FOLDER}");
            }

            if (!EnsureAsmdef(ScriptAlias))
                return GetSetupIssue() ?? "the hot-update assembly definition could not be prepared.";

            WarnAboutNestedAsmdefs();
            RegisterHotUpdateAssembly(ScriptAlias);

            // Report on what the build gate will actually check, not on the steps we just ran.
            string issue = GetSetupIssue();
            if (issue != null)
            {
                Debug.LogWarning($"[Setup] Setup incomplete: {issue}");
                return issue;
            }

            Debug.Log($"[Setup] Done. Write your scripts in {HOTUPDATE_FOLDER}; they compile into " +
                      $"'{ScriptAlias}' and are published as '{HotUpdateAssemblyName}'.");
            return null;
        }

        /// <summary>
        /// Brings the hot-update assembly definition to its expected state: one asmdef in
        /// <see cref="HOTUPDATE_FOLDER"/>, named <see cref="ScriptAlias"/> both as a file and in
        /// its "name" field. An asmdef left from an earlier setup is renamed and rewritten
        /// in place rather than replaced, so its GUID — and therefore its HybridCLR registration
        /// and any reference the creator added — survives. Returns false when it cannot proceed.
        /// </summary>
        /// <param name="assemblyName">The name to converge on: <see cref="ScriptAlias"/>.</param>
        static bool EnsureAsmdef(string assemblyName)
        {
            // Only the folder root: an asmdef in a subfolder is a separate assembly the creator
            // owns, not something this setup should rename.
            string[] existing = Directory.GetFiles(HOTUPDATE_FOLDER, "*.asmdef", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .ToArray();

            if (existing.Length > 1)
            {
                Debug.LogError($"[Setup] {HOTUPDATE_FOLDER} contains {existing.Length} assembly definitions " +
                               $"({string.Join(", ", existing.Select(Path.GetFileName))}). Unity allows one per " +
                               "folder: keep a single one and run the setup again.");
                return false;
            }

            string asmdefPath = $"{HOTUPDATE_FOLDER}/{assemblyName}.asmdef";

            if (existing.Length == 0)
            {
                File.WriteAllText(asmdefPath, BuildAsmdefContent(assemblyName));
                AssetDatabase.Refresh();
                Debug.Log($"[Setup] Created asmdef {asmdefPath}");
                return true;
            }

            // Rename the file first, so the "name" field is aligned on the final path.
            if (existing[0] != asmdefPath)
            {
                string error = AssetDatabase.RenameAsset(existing[0], assemblyName);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[Setup] Could not rename {existing[0]} to {assemblyName}: {error}");
                    return false;
                }
                Debug.Log($"[Setup] Renamed {Path.GetFileName(existing[0])} → {Path.GetFileName(asmdefPath)}");
            }

            AlignDeclaredAssemblyName(asmdefPath, assemblyName);
            EnsureDefaultReferences(asmdefPath);
            return true;
        }

        /// <summary>
        /// Adds whichever of <see cref="DEFAULT_REFERENCES"/> the asmdef does not already
        /// name. Additive only: a reference the creator added is never removed, because this
        /// cannot tell an experiment from a mistake and the publish check can.
        /// </summary>
        /// <remarks>
        /// Unity writes a reference either as the assembly name or as "GUID:...", and a project
        /// set up before this existed has the GUID form, so both are recognised before deciding
        /// something is missing. What gets written is the GUID when it resolves — matching what
        /// the inspector would write — and the plain name when it does not, which happens when
        /// the package is not installed yet and is worth leaving legible rather than failing on.
        /// </remarks>
        static void EnsureDefaultReferences(string asmdefPath)
        {
            try
            {
                JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
                JArray references = asmdef["references"] as JArray;
                if (references == null)
                {
                    references = new JArray();
                    asmdef["references"] = references;
                }

                bool changed = false;

                foreach (string assembly in DEFAULT_REFERENCES)
                {
                    string guid = GuidOfAssembly(assembly);
                    bool present = references.Values<string>()
                        .Any(r => r == assembly || (guid != null && r == "GUID:" + guid));

                    if (present)
                    {
                        continue;
                    }

                    references.Add(guid != null ? "GUID:" + guid : assembly);
                    changed = true;
                    Debug.Log($"[Setup] {Path.GetFileName(asmdefPath)}: added the reference to {assembly}.");
                }

                if (!changed)
                {
                    return;
                }

                File.WriteAllText(asmdefPath, asmdef.ToString(Formatting.Indented));
                AssetDatabase.ImportAsset(asmdefPath, ImportAssetOptions.ForceUpdate);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Setup] Could not check the references in {asmdefPath}: {e.Message}");
            }
        }

        static string GuidOfAssembly(string assemblyName)
        {
            string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>Renders the seed references for a freshly written asmdef.</summary>
        static string References(string[] assemblies)
            => string.Join(", ", assemblies.Select(a =>
            {
                string guid = GuidOfAssembly(a);

                return guid != null ? $"\"GUID:{guid}\"" : $"\"{a}\"";
            }));

        static string BuildAsmdefContent(string assemblyName) =>
$@"{{
    ""name"": ""{assemblyName}"",
    ""rootNamespace"": """",
    ""references"": [{References(DEFAULT_REFERENCES)}],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

        /// <summary>
        /// Rewrites the asmdef's "name" field to <paramref name="assemblyName"/>, leaving every
        /// other field untouched. Renaming the file is not enough on its own: the field is what
        /// Unity compiles the assembly as, and so the name the scenes reference.
        /// </summary>
        static void AlignDeclaredAssemblyName(string asmdefPath, string assemblyName)
        {
            try
            {
                JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
                string declared = (string)asmdef["name"];

                if (declared == assemblyName)
                {
                    Debug.Log($"[Setup] asmdef already declares '{assemblyName}', skipping.");
                    return;
                }

                asmdef["name"] = assemblyName;
                File.WriteAllText(asmdefPath, asmdef.ToString(Formatting.Indented));
                AssetDatabase.ImportAsset(asmdefPath, ImportAssetOptions.ForceUpdate);

                Debug.Log($"[Setup] {Path.GetFileName(asmdefPath)}: assembly name " +
                          $"'{declared}' → '{assemblyName}'.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Setup] Could not rewrite the assembly name in {asmdefPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Flags asmdefs nested under the hot-update folder. They are separate assemblies this
        /// setup does not manage, and each one ships under whatever name it declares — a generic
        /// one collides with the assemblies of other projects in the player.
        /// </summary>
        static void WarnAboutNestedAsmdefs()
        {
            foreach (string nested in Directory.GetFiles(HOTUPDATE_FOLDER, "*.asmdef", SearchOption.AllDirectories))
            {
                string normalized = nested.Replace('\\', '/');
                if (normalized == ExistingAsmdefPath)
                    continue;

                Debug.LogWarning($"[Setup] Assembly definition nested under {HOTUPDATE_FOLDER}: {normalized} " +
                                 $"(declares '{ReadDeclaredAssemblyName(nested)}'). The setup does not manage " +
                                 "it — make sure its name cannot collide with another project's assembly.");
            }
        }

        static string ReadDeclaredAssemblyName(string asmdefPath)
        {
            try { return (string)JObject.Parse(File.ReadAllText(asmdefPath))["name"]; }
            catch { return null; }
        }

        static bool RegisterHotUpdateAssembly(string assemblyName)
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;

            string asmdefPath = $"{HOTUPDATE_FOLDER}/{assemblyName}.asmdef";

            AssemblyDefinitionAsset asmdefAsset =
                AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPath);
            if (asmdefAsset == null)
            {
                // Just renamed or rewritten, and not imported yet: import it now rather than leave
                // the registration to a second run.
                AssetDatabase.ImportAsset(asmdefPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                asmdefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPath);
            }
            if (asmdefAsset == null)
            {
                Debug.LogWarning($"[Setup] Cannot load the asmdef to register at {asmdefPath} yet.");
                return false;
            }

            AssemblyDefinitionAsset[] current = settings.hotUpdateAssemblyDefinitions ?? new AssemblyDefinitionAsset[0];

            // Entries whose asmdef no longer exists. The list used to only grow: an asmdef deleted
            // or replaced (a hot-update folder removed and set up again, say) stayed registered as
            // a missing reference, beside the one in use. Only missing ones are dropped — an
            // assembly the creator registered by hand is theirs to keep.
            AssemblyDefinitionAsset[] kept = current.Where(a => a != null).ToArray();
            int pruned = current.Length - kept.Length;

            bool registered = kept.Any(a => a == asmdefAsset);
            if (!registered)
            {
                kept = kept.Append(asmdefAsset).ToArray();
            }

            if (registered && pruned == 0)
            {
                Debug.Log("[Setup] asmdef already registered in HybridCLR, skipping.");
                return true;
            }

            settings.hotUpdateAssemblyDefinitions = kept;
            SaveHybridCLRSettings(settings);

            if (pruned > 0)
                Debug.Log($"[Setup] Removed {pruned} missing asmdef reference(s) from the Hot Update Assembly Definitions.");
            if (!registered)
                Debug.Log("[Setup] asmdef registered in the Hot Update Assembly Definitions.");
            return true;
        }

        static void SaveHybridCLRSettings(HybridCLRSettings settings)
        {
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new Object[] { settings },
                "ProjectSettings/HybridCLRSettings.asset",
                true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // ============================================================
        //  SETUP VALIDATION
        // ============================================================
        /// <summary>
        /// Checks that the project can compile hot-update code safely: the asmdef exists at
        /// <see cref="HotUpdateAsmdefPath"/>, declares <see cref="ScriptAlias"/>, and is registered
        /// with HybridCLR. Returns <c>null</c> when everything is in place, otherwise the reason to
        /// show the user.
        /// </summary>
        public static string GetSetupIssue()
        {
            InstallerController installer = null;
            try { installer = new InstallerController(); } catch { /* manifest unreadable */ }

            if (installer == null || !installer.HasInstalledHybridCLR())
            {
                return "the HybridCLR interpreter is not installed into this Editor's IL2CPP — the " +
                       "UPM package alone does not install it, and a player built like this ships " +
                       "without an interpreter, so hot-update assemblies would load but never run. " +
                       "Re-run the interpreter configuration.";
            }

            string asmdefPath = ExistingAsmdefPath;

            if (asmdefPath == null || !File.Exists(asmdefPath))
            {
                if (Directory.Exists(LEGACY_HOTUPDATE_FOLDER))
                {
                    return $"the interpreted scripts are in {LEGACY_HOTUPDATE_FOLDER}, which this version " +
                           $"of the SDK no longer compiles. Re-run the interpreter configuration: it moves " +
                           $"them to {HOTUPDATE_FOLDER}.";
                }

                return $"the hot-update assembly definition is missing ({HOTUPDATE_FOLDER}). " +
                       "Open Virtuademy/Setup/Setup project and configure the interpreter.";
            }

            // Any other name compiles the scenes against an assembly the player cannot resolve.
            string declared = ReadDeclaredAssemblyName(asmdefPath);
            if (declared != ScriptAlias || asmdefPath != HotUpdateAsmdefPath)
            {
                return $"{asmdefPath} declares assembly name '{declared}' instead of '{ScriptAlias}'. " +
                       "Re-run the interpreter configuration to align it.";
            }

            AssemblyDefinitionAsset asmdefAsset =
                AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPath);
            if (asmdefAsset == null)
            {
                return $"{asmdefPath} exists on disk but Unity has not imported it yet. " +
                       "Let the editor refresh and retry.";
            }

            HybridCLRSettings settings = null;
            try { settings = HybridCLRSettings.Instance; } catch { /* HybridCLR not ready */ }

            AssemblyDefinitionAsset[] defs = settings?.hotUpdateAssemblyDefinitions;
            if (defs == null || !defs.Any(a => a == asmdefAsset))
            {
                return $"'{declared}' is not registered in HybridCLR's Hot Update " +
                       "Assembly Definitions, so it would be compiled into the player instead of " +
                       "being interpreted. Re-run the interpreter configuration.";
            }

            // A missing entry is not cosmetic: HybridCLR reads `.text` off every entry to collect
            // the hot-update assembly names (SettingsUtil.HotUpdateAssemblyNamesExcludePreserved),
            // so one null throws NullReferenceException in the DLL compile. Reported here so the
            // setup window shows the project as not ready and its button — which runs the setup,
            // which drops them — is enabled again.
            int missing = defs.Count(a => a == null);
            if (missing > 0)
            {
                return $"{missing} entr{(missing == 1 ? "y" : "ies")} in HybridCLR's Hot Update Assembly " +
                       "Definitions point at an asmdef that no longer exists, and HybridCLR fails on them " +
                       "when it compiles the hot-update DLLs. Re-run the interpreter configuration to remove them.";
            }

            return null;
        }

        /// <summary>True when <see cref="GetSetupIssue"/> finds nothing to complain about.</summary>
        public static bool IsHotUpdateReady() => GetSetupIssue() == null;

        // ============================================================
        //  INTERPRETER INSTALL
        // ============================================================
        /// <summary>
        /// Installs HybridCLR's patched libil2cpp into this Editor's IL2CPP when it is not there
        /// yet. Adding the UPM package is NOT enough: without this step the player is built with
        /// the stock IL2CPP and ships no interpreter, so hot-update assemblies load but never run.
        /// Clones two git repositories, so git has to be on PATH and the call takes a while.
        /// </summary>
        public static bool EnsureInterpreterInstalled()
        {
            InstallerController installer;
            try
            {
                installer = new InstallerController();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Setup] Cannot read the HybridCLR version manifest: " + e.Message);
                return false;
            }

            if (installer.HasInstalledHybridCLR())
            {
                Debug.Log("[Setup] HybridCLR interpreter already installed (libil2cpp " +
                          $"v{installer.InstalledLibil2cppVersion ?? "unknown"}).");
                return true;
            }

            if (installer.GetCompatibleType() == InstallerController.CompatibleType.Incompatible)
            {
                Debug.LogError("[Setup] HybridCLR is incompatible with this Unity version. Minimum: " +
                               installer.GetCurrentUnityVersionMinCompatibleVersionStr());
                return false;
            }

            RedirectGiteeMirrorsToGitHub();

            try
            {
                EditorUtility.DisplayProgressBar("Interpreted scripting",
                    "Installing the HybridCLR interpreter (cloning hybridclr and il2cpp_plus)...", 0.5f);
                installer.InstallDefaultHybridCLR();
            }
            catch (System.Exception e)
            {
                // The message carries the repository URL, which is usually the whole story:
                // git missing from PATH, or the host unreachable from this network.
                Debug.LogError("[Setup] HybridCLR interpreter install failed. Check that git is on " +
                               "PATH and that the repository below is reachable from here: " + e.Message);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // The installer logs and returns instead of throwing on some failures, so the state on
            // disk is the only trustworthy outcome.
            if (!installer.HasInstalledHybridCLR())
            {
                Debug.LogError("[Setup] HybridCLR interpreter install did not produce a patched " +
                               "libil2cpp. See the errors above.");
                return false;
            }

            Debug.Log("[Setup] HybridCLR interpreter installed into the local IL2CPP.");
            return true;
        }

        // ============================================================
        //  COMPILE DLL — recurring operation
        // ============================================================
        /// <summary>
        /// Path of the per-target hot-update DLL HybridCLR produces for a given assembly name.
        /// </summary>
        static string TargetDllPath(BuildTarget target, string assemblyName)
            => Path.Combine($"HybridCLRData/HotUpdateDlls/{target}", $"{assemblyName}.dll");

        /// <summary>
        /// Whether every target already has a DLL compiled for that assembly name. Since the name
        /// is a digest of the source, their presence means they were compiled from exactly this
        /// code — so there is nothing to recompile.
        /// </summary>
        public static bool CompiledDllsArePresent(string assemblyName)
            => !string.IsNullOrEmpty(assemblyName)
               && TARGETS.All(target => File.Exists(TargetDllPath(target, assemblyName)));

        /// <summary>
        /// Targets whose DLL is missing for that assembly name. Empty means the set is complete —
        /// which the build gate insists on, because the backend rejects a partial bundle.
        /// </summary>
        public static BuildTarget[] MissingTargets(string assemblyName)
            => TARGETS.Where(target => !File.Exists(TargetDllPath(target, assemblyName))).ToArray();

        /// <summary>
        /// Compiles the hot-update assembly for every target and copies each DLL to the file name
        /// <paramref name="assemblyName"/>, or does nothing when the DLLs for that name are already
        /// there — the name digests the source, so their presence means they came from exactly this
        /// code.
        /// </summary>
        /// <remarks>
        /// HybridCLR compiles under the name the asmdef declares, <see cref="ScriptAlias"/>, and
        /// overwrites that file on every compile. The copy is the DLL exactly as compiled — only
        /// its file is named after this publish — so what is bundled, verified and loaded is the
        /// same bytes.
        /// </remarks>
        /// <param name="assemblyName">
        /// Taken as a parameter rather than read from <see cref="HotUpdateAssemblyName"/>, which
        /// rescans the source on every access: the caller has already decided which name this run
        /// is about, and a file saved halfway through would otherwise have us compile under one
        /// name while the caller verifies another.
        /// </param>
        public static void CompileDll(string assemblyName)
        {
            if (CompiledDllsArePresent(assemblyName))
            {
                Debug.Log($"[Compile] {assemblyName} is already compiled for every target — " +
                          "the source has not changed, nothing to do.");
                return;
            }

            foreach (BuildTarget target in TARGETS)
            {
                Debug.Log($"[Compile] Compiling the DLL for target: {target} ...");

                try
                {
                    CompileDllCommand.CompileDll(target);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Compile] Error compiling for {target}: {e.Message}");
                    continue;
                }

                string compiled = TargetDllPath(target, ScriptAlias);
                if (!File.Exists(compiled))
                {
                    Debug.LogWarning($"[Compile] {target} compiled, but no DLL was found at: {compiled}");
                    continue;
                }

                string dllPath = TargetDllPath(target, assemblyName);
                File.Copy(compiled, dllPath, true);
                Debug.Log($"[Compile] DLL produced for {target}: {Path.GetFullPath(dllPath)}");
            }

            Debug.Log("[Compile] Compilation cycle completed.");
        }


        // ============================================================
        //  PUBLISH FINGERPRINT — skip work that would change nothing
        // ============================================================
        // Keyed by project GUID because EditorPrefs is global to the machine: a bare key would
        // have two creator projects reading each other's marker.
        const string FINGERPRINT_KEY_PREFIX = "Virtuademy_HotUpdate_PublishFingerprint_";

        static string FingerprintKey => FINGERPRINT_KEY_PREFIX + PlayerSettings.productGUID;

        /// <summary>
        /// True when the last <see cref="CompileVerifyAsync"/> found the published bundle already
        /// current, which is why it skipped compiling and verifying. The bundle is still built from
        /// the DLLs on disk and uploaded with the scenes: the marker is per project, not per tenant.
        /// Meaningful only within the run that set it — this is the verdict of that check, not
        /// stored state.
        /// </summary>
        public static bool BundleIsCurrent { get; private set; }

        /// <summary>Fingerprint of what was just compiled, persisted once the publish succeeds.</summary>
        static string pendingFingerprint;

        /// <summary>
        /// Records that the bundle now on the platform matches what is in this project. Called by
        /// the publisher after the import succeeds — never before, or a failed publish would be
        /// remembered as done and the next build would skip it.
        /// </summary>
        public static void MarkBundlePublished()
        {
            if (string.IsNullOrEmpty(pendingFingerprint))
                return;

            EditorPrefs.SetString(FingerprintKey, pendingFingerprint);
            pendingFingerprint = null;
        }

        /// <summary>
        /// What a publish would produce, plus the rules it must satisfy. Two inputs only:
        ///
        ///   * the assembly name, which already digests the source — the scripts, the assembly
        ///     definition minus its own name field, the Unity version, the per-target defines and
        ///     the resolved package set (see <see cref="HotUpdateFingerprint"/>). Re-scanning the
        ///     project here would be a second implementation of the same thing, free to drift from
        ///     the one the name is built from;
        ///   * the whitelist, so a policy that tightens forces a re-verify instead of leaving
        ///     already-published code accepted under the old rules until someone edits a script.
        ///
        /// The policy stays OUT of the assembly name for the same reason it belongs here: it
        /// decides whether the code is acceptable, not what the code compiles to. Folding it into
        /// the name would mint a new identity for bytes that did not change, and the backend would
        /// store a second copy of an assembly it already has.
        /// </summary>
        static string ComputePublishFingerprint(string assemblyName, string policyJson)
        {
            return HotUpdateDllLocator.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(
                (assemblyName ?? string.Empty) + "\n" + (policyJson ?? string.Empty)));
        }
        // ============================================================
        //  BUILD + VERIFY — used by the Addressables build gate
        // ============================================================
        /// <summary>
        /// Whether the project has any interpreted script to compile: a <c>.cs</c> file anywhere
        /// under the hot-update folder, the same files the assembly fingerprint digests.
        ///
        /// Without one there is no assembly at all — Unity compiles no DLL for an asmdef with no
        /// sources — so there is nothing to verify, nothing to bundle and nothing for the scenes
        /// to declare. The publisher asks this too, so the gate and the bundle agree on the answer.
        /// </summary>
        public static bool ProjectHasInterpretedScripts()
            => Directory.Exists(HOTUPDATE_FOLDER)
               && Directory.EnumerateFiles(HOTUPDATE_FOLDER, "*.cs", SearchOption.AllDirectories).Any();

        /// <summary>
        /// Builds the hot-update DLL, then runs the LOCAL whitelist check and the AUTHORITATIVE
        /// SERVER check. Returns true only if BOTH pass; otherwise logs the reason and returns
        /// false so the caller can block the addressables build (fail-closed). A project with no
        /// interpreted scripts passes straight through: there is no code to let in.
        /// </summary>
        public static async Task<bool> CompileVerifyAsync()
        {
            BundleIsCurrent = false;

            // Before the setup check: a project that interprets nothing does not need the
            // interpreter set up either. Without this, an empty hot-update folder compiled no DLL
            // and the gate below refused the build with "No DLL was produced", blaming missing
            // build-support modules — so a creator who had HybridCLR configured but no scripts
            // could not build their scenes at all.
            if (!ProjectHasInterpretedScripts())
            {
                // Scripts left in the folder used before it took the alias's name would otherwise
                // read as "no scripts", and the scenes would publish without them.
                if (Directory.Exists(LEGACY_HOTUPDATE_FOLDER)
                    && Directory.EnumerateFiles(LEGACY_HOTUPDATE_FOLDER, "*.cs", SearchOption.AllDirectories).Any())
                {
                    Debug.LogError($"[HotUpdateSecurity] Interpreted scripting is not set up: {GetSetupIssue()} Build blocked.");
                    return false;
                }

                pendingFingerprint = null;
                Debug.Log($"[HotUpdate] No interpreted scripts under {HOTUPDATE_FOLDER}: nothing to compile or " +
                          "verify. The scenes are built without an interpreted assembly.");
                return true;
            }

            // 0) The project has to be set up: the asmdef compiles as the alias, the one name the
            //    player resolves scene scripts by.
            string setupIssue = GetSetupIssue();
            if (setupIssue != null)
            {
                Debug.LogError($"[HotUpdateSecurity] Interpreted scripting is not set up: {setupIssue} Build blocked.");
                return false;
            }

            // 1) The whitelist comes first: it is needed to verify, and it takes part in the
            //    publish marker below, so a policy that changed has to be in hand before deciding
            //    whether there is anything to do.
            HotUpdatePolicyFetcher.FetchResult fetch = await HotUpdatePolicyFetcher.FetchAsync();
            if (!fetch.Ok)
            {
                Debug.LogError("[HotUpdateSecurity] Policy unavailable — build blocked (fail-closed). " + fetch.Error);
                return false;
            }

            // 2) Nothing to rebuild? Then nothing to verify either. The bundle is still built from
            //    the DLLs on disk and the scenes still declare it — a new scene against unchanged
            //    scripts is exactly the case that must not be skipped.
            //
            //    Read once and carried through the steps below: the property rescans the source on
            //    every access, and this run must not straddle two identities.
            string expected = HotUpdateAssemblyName;
            string fingerprint = ComputePublishFingerprint(expected, fetch.Json);

            if (fingerprint == EditorPrefs.GetString(FingerprintKey, string.Empty))
            {
                BundleIsCurrent = true;
                Debug.Log($"[HotUpdate] '{expected}' is unchanged since the last publish " +
                          "(scripts, assembly definition, build inputs and whitelist all match). Skipping " +
                          "compile and verification; the bundle is still built and declared by the scenes.");
                return true;
            }

            // 3) A name to publish under. The fallback shape, prefix with no digest, means the
            //    fingerprint could not be computed, and two publishes could share it.
            if (expected == ProjectAssemblyPrefix)
            {
                Debug.LogError("[HotUpdate] The source fingerprint could not be computed, so the assembly has no name to " +
                               "be published under. Build blocked.");
                return false;
            }

            // 4) Build the DLL(s) under the alias and copy them to the published name — a no-op when
            //    they already exist under that name, which is exactly the case where the source has
            //    not moved.
            CompileDll(expected);

            BuildTarget[] missing = MissingTargets(expected);
            if (missing.Length > 0)
            {
                // The backend rejects a bundle that misses a target, so failing here — where the
                // reason is still on screen — beats failing at import time.
                Refuse($"[HotUpdateSecurity] No DLL was produced for: {string.Join(", ", missing)}. " +
                       "The build support module for those targets may not be installed in this " +
                       "Editor. Build blocked.");
                return false;
            }

            // 5) Resolve the freshly compiled assembly (ScriptAssemblies → has a PDB for line info).
            string dllPath = HotUpdateDllLocator.ResolveDefaultDllPath(out _);
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                Debug.LogError("[HotUpdateSecurity] Compiled HotUpdate assembly not found after build. Build blocked.");
                return false;
            }

            // 6) LOCAL check against the policy fetched above. Block on fail.
            VerificationResult local = HotUpdateDllLocator.VerifyAndLog(dllPath, fetch.Policy);
            if (!local.Passed)
            {
                // The violations themselves are carried into the message rather than left to
                // "see above": above is exactly what a recompilation erases.
                Refuse("[HotUpdateSecurity] LOCAL check FAILED — build blocked.\n" + local.Summarize());
                return false;
            }

            // 7) SERVER check (authoritative). Block on rejection OR if it can't complete.
            byte[] bytes = File.ReadAllBytes(dllPath);
            HotUpdateServerVerifier.Result server = await HotUpdateServerVerifier.VerifyAsync(bytes, Path.GetFileName(dllPath));

            if (!server.Reachable)
            {
                Debug.LogError("[HotUpdateSecurity] SERVER check could not complete — build blocked. " + server.Error);
                return false;
            }
            if (!server.Passed)
            {
                LogServerViolations(server.Response);
                Refuse("[HotUpdateSecurity] SERVER check REJECTED the DLL — build blocked.\n"
                       + DescribeServerViolations(server.Response));
                return false;
            }

            // Held, not stored: the marker is written only once the publish itself succeeds.
            pendingFingerprint = fingerprint;

            Debug.Log("[HotUpdateSecurity] Local + server checks PASSED. Proceeding with the addressables build.");
            return true;
        }

        /// <summary>
        /// Points the installer at the GitHub originals when it is still on HybridCLR's gitee
        /// defaults, which do not resolve outside China — the clone fails with a bare DNS error
        /// that gives no hint of the cause. Only the known defaults are rewritten: a URL the team
        /// pointed somewhere else on purpose stays as it is.
        /// </summary>
        static void RedirectGiteeMirrorsToGitHub()
        {
            HybridCLRSettings settings = null;
            try { settings = HybridCLRSettings.Instance; } catch { /* HybridCLR not ready */ }

            if (settings == null)
                return;

            bool changed = false;

            if (settings.hybridclrRepoURL == GITEE_HYBRIDCLR)
            {
                settings.hybridclrRepoURL = GITHUB_HYBRIDCLR;
                changed = true;
            }

            if (settings.il2cppPlusRepoURL == GITEE_IL2CPP_PLUS)
            {
                settings.il2cppPlusRepoURL = GITHUB_IL2CPP_PLUS;
                changed = true;
            }

            if (!changed)
                return;

            SaveHybridCLRSettings(settings);
            Debug.Log("[Setup] HybridCLR source repositories switched from the gitee mirrors to GitHub.");
        }

        /// <summary>
        /// The server's violations as one string, for the refusal that has to survive a reload —
        /// <see cref="LogServerViolations"/> writes one Console entry each, which is better to read
        /// but does not outlive a recompilation.
        /// </summary>
        private static string DescribeServerViolations(HotUpdateServerVerifier.ServerResponse resp)
        {
            if (resp?.Violations == null || resp.Violations.Count == 0)
                return "  (the server reported no detail)";

            return string.Join(System.Environment.NewLine, resp.Violations
                .Select(v => $"  - [{v.Kind}] {v.Detail}  ({v.Location})"));
        }

        private static void LogServerViolations(HotUpdateServerVerifier.ServerResponse resp)
        {
            if (resp?.Violations == null)
                return;
            foreach (HotUpdateServerVerifier.ServerViolation v in resp.Violations)
                Debug.LogError($"[HotUpdateSecurity][server] [{v.Kind}] {v.Detail}  ({v.Location})");
        }
    }
}
