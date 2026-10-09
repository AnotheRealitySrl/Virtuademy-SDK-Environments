using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using Virtuademy.BreakingChangeSolvers;


namespace Virtuademy.SDK.Environments.Installer.Editor
{
    /// <summary>
    /// The v2026.5 -> v2026.6 update routine for a creator project, and the only menu entry it
    /// needs: one window, one Apply, all three steps the release asks for.
    ///
    /// Step 1 is project-wide migration for the two renames the packages have been through:
    /// the Reflectis -> Virtuademy brand rename, and the authoring package becoming
    /// Virtuademy-SDK-Environments (namespaces, assembly names, package ids).
    /// One pass covers both, in that order, so a project can arrive from either side.
    ///
    /// Step 2 is <see cref="VirtuademyPOIPageMigrator"/>, which swaps the framework's
    /// GenericHookComponent for <c>POIPagePlaceholder</c> as the page marker of every POI. A creator
    /// project no longer has the framework, so without this pass the marker is a missing script
    /// and every POI in a world published from the project comes up empty. It runs right after the
    /// rename rewrite and re-reads each file, so the two passes can touch the same scene.
    ///
    /// Step 3 is <see cref="VirtuademyFolderMigrator"/>, which gathers what the SDK generates into
    /// the single Assets/Virtuademy folder. It runs after the rewrite, because the rewrite works
    /// from the paths the scan recorded and a folder move would invalidate them. Nothing breaks if
    /// it is skipped — both settings assets are found by type wherever they sit — so it is a
    /// toggle rather than a step.
    ///
    /// MonoBehaviour references survive the rename on their own (they resolve by GUID), but
    /// every reference stored BY NAME does not: [SerializeReference] payloads in scenes,
    /// prefabs and ScriptableObjects; Visual Scripting graphs (node types are serialized as
    /// fully-qualified type + assembly strings); UXML custom-control tags; the creator's own
    /// C# scripts (using directives) and asmdef references; the UPM manifest package ids.
    /// This tool rewrites all of those as raw text, BEFORE Unity tries to resolve the old
    /// names, then refreshes the AssetDatabase and rebuilds the Visual Scripting node library.
    ///
    /// Recommended flow for a creator project:
    ///   1. Commit / back up the project (the rewrite touches many files).
    ///   2. Update the SDK packages to their v2026.6 versions.
    ///   3. Run this routine, review the file list, Apply. Save nothing between 2 and 3: a scene or
    ///      prefab saved there is written with its graphs' unresolved nodes and cannot be recovered.
    ///      <see cref="VirtuademyUpdateSaveGuard"/> refuses those saves, and Apply closes the open
    ///      scenes without saving them.
    ///   4. Let Unity recompile and reimport. The scenes, prefabs and assets the routine changed are
    ///      then re-saved by <see cref="VirtuademyUpdateResave"/> without being opened; files it
    ///      did not change are left alone. Only the files it reports need a manual check and save.
    ///
    /// The re-save exists so that nobody has to save by hand at the wrong moment, which is what the
    /// rest of this comment is about — and it still describes what to do with a file the re-save
    /// put back because it would not have come out whole.
    ///
    /// Expect a wall of Visual Scripting deserialization errors on the FIRST open after this
    /// runs, and do not save anything until they stop. A graph records each unit by namespace and
    /// type, so the rewrite changes what every unit is called; while an asset-import worker
    /// cannot resolve the new name yet, Visual Scripting swaps the unit for
    /// <c>Unity.VisualScripting.MissingType</c> and keeps the original in `formerType` /
    /// `formerValue`. That swap renumbers the JSON `$id`s, and FullSerializer requires a
    /// definition to precede its reference, so the visible error is
    /// "Object definition has not been encountered for object with id=N ... have you reordered or
    /// modified the serialized data?" — alarming, and a consequence of the substitution rather
    /// than of damaged data. Most of them clear on their own, and the log then says
    /// "Missing unit type ... was found. Converted ... back".
    ///
    /// **Some do not clear by reopening**, because the failure is cached in the asset's imported
    /// artifact rather than in the asset. Observed on this repo: one prefab kept failing on a
    /// second open, in the main process while the window layout was being restored, with no
    /// recovery message at all. What fixes that one is a **Reimport** on the asset (right-click in
    /// the Project window), which deserializes it afresh with the assemblies loaded. Reopening the
    /// editor does not.
    ///
    /// So the order is: reimport, then OPEN the graph and check the units are real nodes and not
    /// "Missing Type", and only then save. **Saving first is the one way to make the loss
    /// permanent**: the units get written out as `MissingType` and the graph really has lost them.
    /// The asset itself is untouched until that save, so there is no hurry.
    ///
    /// **Reimport every affected asset, not one.** Each asset caches its own import artifact, so
    /// clearing one says nothing about the others — and the errors arrive a few at a time, as
    /// whatever is loaded happens to touch them, which makes it easy to believe the last reimport
    /// fixed the problem. It did not; it fixed that asset. Find them all before deciding you are
    /// done: the editor log names the missing type in a `formerType` entry, and grepping the
    /// project for that type name lists every asset that records it.
    ///
    /// The tool is idempotent: a second run finds nothing to change.
    /// </summary>
    public class VirtuademyRenameMigrator : EditorWindow
    {
        private class Entry
        {
            public string Path;
            public int Hits;
            public bool Selected = true;
        }

        // The old brand token is split so this file never matches its own patterns
        // (neither when the repo-side rename scripts run, nor when the tool scans itself
        // in a project where packages are embedded).
        private static readonly string OldBrand = "Reflec" + "tis";
        private const string NewBrand = "Virtuademy";
        private const string MenuPath = "Virtuademy/Update routines/v2026.5 -> v2026.6";
        private const string WindowTitle = "Update v2026.5 -> v2026.6";

        // Ordered: specific mappings first, then the generic namespace rule.
        private static readonly (string oldValue, string newValue)[] LiteralMap =
        {
            (OldBrand + ".SDK." + OldBrand + "Api", NewBrand + ".SDK.PlatformApi"),
            (OldBrand.ToLowerInvariant() + "-sdk-" + OldBrand.ToLowerInvariant() + "api", NewBrand.ToLowerInvariant() + "-sdk-platformapi"),
            (OldBrand + "-SDK-" + OldBrand + "Api", NewBrand + "-SDK-PlatformApi"),
            (OldBrand + ".SDK." + OldBrand + "BrowserCommunication", NewBrand + ".SDK.BrowserCommunication"),
            ("com.anotherealitysrl." + OldBrand.ToLowerInvariant() + "-", "com.anotherealitysrl." + NewBrand.ToLowerInvariant() + "-"),
            (OldBrand + "-SDK-", NewBrand + "-SDK-"),
            (OldBrand + "-CreatorKit-", NewBrand + "-CreatorKit-"),
            (OldBrand + "-MinigamesTemplate", NewBrand + "-MinigamesTemplate"),
            (OldBrand + "-PLG-", NewBrand + "-PLG-"),
        };

        private static readonly Regex GenericNamespaceRule =
            new(@"(?<![A-Za-z0-9_])" + OldBrand + @"\.", RegexOptions.Compiled);
        private static readonly Regex EditorNamespaceRule =
            new(@"(?<![A-Za-z0-9_])" + OldBrand + @"Editor\.", RegexOptions.Compiled);

        // The Environments rename (2026-09-09): the authoring package stopped being
        // "CreatorKit Worlds Core" and became "SDK Environments" — package id, three assembly
        // names and every namespace under the old prefix.
        //
        // Applied AFTER the brand rules, which is what lets one pass serve both hops: a project
        // still on the old brand has its old-brand authoring namespace turned into the
        // new-brand one by the generic rule above and is then caught here, while a project that
        // already took the brand rename is caught directly. Ordered longest-first, because the
        // runtime assembly's name is a prefix of the editor one's and replacing the short one
        // first would glue "Editor" onto the new name.
        //
        // The tokens are split for the same reason OldBrand is: this file must not match its own
        // table when the tool scans the project it is running in.
        private static readonly string OldWorlds = "Virtuademy.Creator" + "Kit.Worlds";
        private static readonly string OldWorldsPackage = "Virtuademy-Creator" + "Kit-Worlds-Core";
        private static readonly string OldWorldsId = "virtuademy-creator" + "kit-worlds-core";

        // The installer took the same name on 2026-09-23: its package id, its repository and its
        // namespace. The namespace needs no entry of its own — the brand rule turns it into the
        // old-worlds prefix, and OldWorlds below carries it the rest of the way. The id and the
        // repository do need one, because the old-worlds package entries end in "-Core".
        //
        // The id is the one that matters. It is the manifest KEY of the installer's git
        // dependency, and a key that is not the name in the package's package.json is expected to
        // make UPM reject it — so a project that re-resolves the renamed installer under its old
        // key would stop resolving (not yet observed on a real project, 2026-09-23). Rewriting the
        // key and the URL together, with the lock deleted as this tool already does, is what lets
        // the next resolve land on the renamed installer.
        private static readonly string OldSetupPackage = "Virtuademy-Creator" + "Kit-Worlds-Setup";
        private static readonly string OldSetupId = "virtuademy-creator" + "kit-worlds-setup";

        private static readonly string OldGraphsPackage = "Virtuademy-SDK-Gra" + "phs";
        private static readonly string OldTasksPackage = "Virtuademy-SDK-Ta" + "sks";
        private static readonly string OldDialogsPackage = "Virtuademy-SDK-Dia" + "logs";
        private static readonly string OldGraphsId = "virtuademy-sdk-gra" + "phs";
        private static readonly string OldTasksId = "virtuademy-sdk-ta" + "sks";
        private static readonly string OldDialogsId = "virtuademy-sdk-dia" + "logs";

        private static readonly string OldApi = "Virtuademy.SDK.Platform" + "Api";

        private static readonly string OldModels = "Virtuademy.SDK.Environments.Client" + "Models";
        private static readonly string OldInteraction = "Virtuademy.SDK.Environments.Inter" + "action";
        private static readonly string OldPlaceholders = "Virtuademy.SDK.Environments.Place" + "holders";
        private static readonly string OldSpawner = "Virtuademy.SDK.Environments.Object" + "Spawner";
        private static readonly string OldDialogs = "Virtuademy.SDK.Dia" + "logs";
        private static readonly string OldGraphs = "Virtuademy.SDK.Gra" + "phs";
        private static readonly string OldTasks = "Virtuademy.SDK.Ta" + "sks";
        private static readonly string OldDialogsEditor = "Virtuademy.SDK.Dialogs" + "Editor";
        private static readonly string OldScripting = "Virtuademy.Scripting" + "Api";
        private static readonly string OldSyncedObject = "Virtuademy.SDK.Environments.Visual" + "Scripting.SyncedObject";
        private static readonly string OldSyncedVariables = "Virtuademy.SDK.Environments.Visual" + "Scripting.SyncedVariables";
        private static readonly string OldCoreUtilities = "Virtuademy.SDK.Core.Utili" + "ties";
        private static readonly string OldCoreVisualScripting = "Virtuademy.SDK.Core.Visual" + "Scripting";
        private static readonly string OldCoreEditor = "Virtuademy.SDK.Core.Edi" + "tor";
        private static readonly string OldCreateTypeInstance = "Virtuademy.SDK.Core.CreateType" + "InstanceUnit";

        private static readonly (string oldValue, string newValue)[] EnvironmentsMap =
        {
            (OldWorlds + ".CoreHybridCLREditor", "Virtuademy.SDK.Environments.HybridCLREditor"),
            (OldWorlds + ".CoreEditor", "Virtuademy.SDK.Environments.Editor"),
            (OldWorlds + ".Core", "Virtuademy.SDK.Environments"),
            (OldWorlds, "Virtuademy.SDK.Environments"),
            (OldWorldsPackage, "Virtuademy-SDK-Environments"),
            (OldWorldsId, "virtuademy-sdk-environments"),
            (OldSetupPackage, "Virtuademy-SDK-Environments-Setup"),
            (OldSetupId, "virtuademy-sdk-environments-setup"),

            // The three packages that carry no platform left the SDK prefix on 2026-09-24:
            // Virtuademy-SDK-{Graphs,Tasks,Dialogs} are SPACS-{Graphs,Tasks,Dialogs}, beside
            // SPACS-Utility. Package id and repository only — their assemblies and namespaces were
            // already SPACS.*, which is what the three namespace rules further down land on.
            //
            // The ids are the entries that matter, for the reason the installer's are: each is
            // the manifest KEY of a git dependency, and a key that is not the name in the fetched
            // package.json makes UPM reject the package. A project on the old brand reaches these
            // through the brand rule first, so one pass carries both hops.
            (OldGraphsPackage, "SPACS-Graphs"),
            (OldTasksPackage, "SPACS-Tasks"),
            (OldDialogsPackage, "SPACS-Dialogs"),
            (OldGraphsId, "spacs-graphs"),
            (OldTasksId, "spacs-tasks"),
            (OldDialogsId, "spacs-dialogs"),

            // PlatformApi named a package that no longer exists: it became
            // Virtuademy-SDK-Library on 2026-09-10, and the DTOs it was named after moved to the
            // contracts package on the same day. The namespace outlived both.
            //
            // The assembly entry has to come first: the old namespace is a prefix of the old
            // assembly name, so rewriting the shorter one first would land the assembly on the
            // right value only by luck of the substring — and would do the wrong thing the
            // moment the two stop sharing a prefix. Neither is spelled out here, for the same
            // reason the tokens above are split: this file must not match its own table.
            (OldApi + ".Wire", "Virtuademy.SDK.ApiData.Wire"),
            (OldApi, "Virtuademy.SDK.ApiData"),

            // The client models split in two on 2026-09-14. What an authored world may see is
            // now a view in Virtuademy.ScriptingApi; the fuller model kept the CM name and the
            // ClientModels namespace and went to the application, which a creator does not
            // install. A graph that reached a CM type through Expose or InvokeMember recorded
            // its full name, so those names are rewritten onto the view.
            //
            // Member names are deliberately untouched: the views carry the same ones the nodes
            // always read, so a rewritten graph resolves its members without a second rule. A
            // graph that reached past that surface — a user's preferences, a session's
            // permissions — has no view to land on and must be re-authored; there is nothing to
            // migrate it to.
            //
            // The namespace alone is NOT in this table, and must not be: it still exists, on
            // the application side. Only these five full names move.
            (OldModels + ".CMUser", "Virtuademy.ScriptingApi.UserView"),
            (OldModels + ".CMSession", "Virtuademy.ScriptingApi.SessionView"),
            (OldModels + ".CMEnvironment", "Virtuademy.ScriptingApi.EnvironmentView"),
            (OldModels + ".CMExperience", "Virtuademy.ScriptingApi.ExperienceView"),
            (OldModels + ".CMTag", "Virtuademy.ScriptingApi.TagView"),

            // The interaction contracts moved on 2026-09-14 for the same reason the models did,
            // from the other direction: they had to become nameable. Virtuademy.SDK is a denied
            // namespace prefix in the script whitelist, and a deny by prefix beats every allow,
            // so a member taking one of these was a member no interpreted script could call.
            //
            // As above, the namespace itself is NOT in this table: four more types stay behind
            // in it. Only these three names move.
            // These were three full type names while four more types stayed in the namespace. On
            // 2026-09-14 the rest followed, along with every placeholder and the spawner
            // contracts, so the three namespaces are empty and move whole — one entry each
            // instead of a hundred type names, and a graph that named any of them is rewritten
            // whether or not anyone thought to list it.
            //
            // What made this safe is that the namespaces are empty *everywhere*, the platform's
            // own code included. That was not true of the earlier moves, which is why those are
            // still spelled out one type at a time above.
            (OldInteraction, "Virtuademy.Environments.ScriptingApi.Interaction"),
            (OldPlaceholders, "Virtuademy.Environments.ScriptingApi.Placeholders"),
            (OldSpawner, "Virtuademy.Environments.ScriptingApi.ObjectSpawner"),

            // The dialog engine and the graph structure are not the platform's, and moving them
            // into its surface would have said they were. They were renamed instead, which takes
            // them out of a denied prefix without coupling two reusable packages to anything:
            // SPACS.Dialogs and SPACS.Graphs, beside SPACS.Utility.
            //
            // Whole namespaces again, and for the same reason as the three above: nothing is left
            // behind in either.
            // Before the general rule, because these are substring replacements and this one is
            // longer: the dialogs package spelled its editor namespace as a sibling rather than a
            // child, so the general rule below would carry that spelling across intact instead of
            // landing on the nested one it now uses. There was never an assembly by this name —
            // the asmdef has always been dotted — so the rule is unambiguous.
            (OldDialogsEditor, "SPACS.Dialogs.Editor"),

            (OldDialogs, "SPACS.Dialogs"),
            (OldGraphs, "SPACS.Graphs"),
            (OldTasks, "SPACS.Tasks"),

            // One rule per package, because each mapping is uniform. For tasks that means
            // SPACS.Tasks, .Detectors, .Editor, .UI, .Utils, .XRDetectors and the TasksNetworked /
            // TasksXRKit samples all falling out of the same substring — assembly names included,
            // which is what asmdef references and the `asm:` field of a SerializeReference need.

            // Not the ChatBot namespace: Virtuademy.SDK.Core.ChatBot still holds IChatBotSystem,
            // in the framework package, and only this one type left it.
            ("Virtuademy.SDK.Core.ChatBot.EChatBotVoice",
             "Virtuademy.Environments.ScriptingApi.ChatBot.EChatBotVoice"),

            // Four groups crossed from the shared surface to the world one on 2026-09-14. The line
            // had been drawn as "what an external application also needs", which nothing enforced
            // and nothing could: Install is internal to the platform application's own assembly,
            // so no other host can put an implementation behind either interface. Redrawn as what
            // only the platform knows against what the player provides, these four are the
            // player's — the screen it draws, the panels over it, the language, the device.
            //
            // As above, the namespace itself is NOT in this table: the views and the analytic
            // statements stay in it. Only these four names move.
            (OldScripting + ".ILocalizationApi",
             "Virtuademy.Environments.ScriptingApi.ILocalizationApi"),
            (OldScripting + ".IPlatformApi",
             "Virtuademy.Environments.ScriptingApi.IPlatformApi"),
            (OldScripting + ".IScreenApi",
             "Virtuademy.Environments.ScriptingApi.IScreenApi"),
            (OldScripting + ".IHelpApi",
             "Virtuademy.Environments.ScriptingApi.IHelpApi"),

        };

        /// <summary>
        /// Renames that must stop at a word boundary, applied after <see cref="EnvironmentsMap"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two types left Virtuademy.SDK.Environments.VisualScripting for the scripting surface;
        /// the namespace keeps the nodes and everything else. That cannot be expressed as a
        /// substring: the namespace also holds SyncedVariablesEventNodes and SyncedObjectEditor,
        /// and a plain replacement of the shorter name rewrites the longer one with it.
        /// </para>
        /// <para>
        /// That is not a cosmetic risk. SyncedVariablesEventNodes is the "On Synced Variable
        /// Changed" node, and a graph stores its type name verbatim — so the naive rule would send
        /// every graph using it to a type that does not exist, and the node would come back empty.
        /// </para>
        /// </remarks>
        private static readonly (Regex Rule, string NewValue)[] TypeMap =
        {
            (Boundary(OldSyncedObject), "Virtuademy.Environments.ScriptingApi.Placeholders.SyncedObject"),
            (Boundary(OldSyncedVariables), "Virtuademy.Environments.ScriptingApi.Placeholders.SyncedVariables"),

            // The utilities, the Visual Scripting node bases and the three editor helpers left the
            // framework package for SPACS-Utility, which references nothing first-party — which is
            // what lets a creator take a string extension without taking the platform with it.
            //
            // Boundary-aware for a reason that is stronger here than anywhere else in this file:
            // Virtuademy.SDK.Core did not empty. It is still the framework's own namespace, and
            // about twenty children of it — Avatars, Fade, SystemFramework, Transitions… — stay
            // exactly where they are. A rule for the bare namespace would move all of them, so
            // there is none: the one type that left it is named outright below.
            //
            // The utilities entry is the one rule that must not touch a `using` line, and must skip
            // three names: that namespace did not empty. Its editor half (IndentDrawer) is still in
            // the framework — and SPACS spells its own editor namespace SPACS.Editor rather than
            // SPACS.Utilities.Editor, so rewriting it would invent a namespace that exists nowhere —
            // and two runtime types, JwtToken and HmacCredential, still live in it inside
            // Virtuademy-SDK-Core. A plain rewrite of `using Virtuademy.SDK.Core.Utilities;` therefore
            // takes JwtToken away from every file that names it (found the hard way on 2026-09-21,
            // running this tool on the application). The `using` line is left alone and
            // SupplementUtilitiesUsing adds `using SPACS.Utilities;` beside it instead, so the moved
            // utilities keep resolving without losing what stayed.
            (new Regex(@"(?<!using\s)" + Regex.Escape(OldCoreUtilities) + @"(?!\.(Editor|JwtToken|HmacCredential))(?![A-Za-z0-9_])", RegexOptions.Compiled), "SPACS.Utilities"),
            (Boundary(OldCoreVisualScripting), "SPACS.VisualScripting"),
            (Boundary(OldCoreEditor), "SPACS.Editor"),
            (Boundary(OldCreateTypeInstance), "SPACS.CreateTypeInstanceUnit"),

            // The nine platform-adapter types that kept the old brand in their names through the
            // wave C rename and lost it on 2026-09-21: the task adapters and the chatbot
            // placeholder. Bounded on both sides, because a project may spell them bare (with a
            // using) or fully qualified, and "Task" + brand is the tail of four of the others.
            // They carry [RenamedFrom] too, so a graph that was not rewritten still deserializes;
            // this rule is for the creator's own C# and for the text the attribute cannot reach.
            (Identifier("TaskSystem" + OldBrand + "Editor"), "TaskSystemVirtuademyEditor"),
            (Identifier("TaskSystem" + OldBrand), "TaskSystemVirtuademy"),
            (Identifier("Task" + OldBrand + "StepSetter"), "TaskVirtuademyStepSetter"),
            (Identifier("AnimatorTask" + OldBrand), "AnimatorTaskVirtuademy"),
            (Identifier("GrabTask" + OldBrand), "GrabTaskVirtuademy"),
            (Identifier("TimerTask" + OldBrand), "TimerTaskVirtuademy"),
            (Identifier("TriggerTask" + OldBrand), "TriggerTaskVirtuademy"),
            (Identifier("Task" + OldBrand), "TaskVirtuademy"),
            (Identifier(OldBrand + "ChatbotPlaceholder"), "VirtuademyChatbotPlaceholder"),

            // Two serialized fields of DialogPanelSpawner (2026-09-21). Deliberately WITHOUT
            // [FormerlySerializedAs] / [RenamedFrom] on the fields: a creator project takes the
            // rename through this pass, which rewrites the YAML key in every scene and prefab and
            // the member name in every graph, and then rebuilds its worlds. Published bundles that
            // skipped the pass lose the two flags — they are rebuilt at the cutover anyway.
            (Identifier("useReflectis" + "Nickname"), "useVirtuademyNickname"),
            (Identifier("useReflectis" + "Avatar"), "useVirtuademyAvatar"),

            // The scripting defines (2026-09-21). Whole identifiers, for the #if lines of a
            // creator's own scripts; the package's registrars retire the old symbols from
            // PlayerSettings by themselves when they load.
            (Identifier("REFLECTIS_CREATOR_KIT_WORLDS_PLACEHOLDERS"), "VIRTUADEMY_ENVIRONMENTS_PLACEHOLDERS"),
            (Identifier("REFLECTIS_CREATOR_KIT_WORLDS_TASKS"), "VIRTUADEMY_ENVIRONMENTS_TASKS"),
            (Identifier("REFLECTIS_CREATOR_KIT_WORLDS_VISUAL_SCRIPTING"), "VIRTUADEMY_ENVIRONMENTS_VISUAL_SCRIPTING"),
            (Identifier("REFLECTIS_CREATOR_KIT_WORLDS_DIALOGS"), "VIRTUADEMY_ENVIRONMENTS_DIALOGS"),
            (Identifier("REFLECTIS_CREATOR_KIT_WORLDS_ANALYTICS"), "VIRTUADEMY_ENVIRONMENTS_ANALYTICS"),
            (Identifier("REFLECTIS_DESKTOP"), "VIRTUADEMY_DESKTOP"),
            (Identifier("REFLECTIS_MOBILE"), "VIRTUADEMY_MOBILE"),
            (Identifier("REFLECTIS_VR"), "VIRTUADEMY_VR"),

            // The seventeen analytics types left Virtuademy.SDK.ApiData for Virtuademy.ScriptingApi on
            // 2026-09-14; the namespace itself stays (the wire DTOs are still in it), so each type is
            // named. A graph records enum values by the type's full name, and an unresolved one
            // deserializes to a string — found in this package's own analytics graphs on 2026-09-21.
            (Identifier("Virtuademy.SDK.ApiData.AnalyticDTO"), "Virtuademy.ScriptingApi.AnalyticDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceAnalyticDTO"), "Virtuademy.ScriptingApi.ExperienceAnalyticDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceCompleteDTO"), "Virtuademy.ScriptingApi.ExperienceCompleteDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceJoinDTO"), "Virtuademy.ScriptingApi.ExperienceJoinDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceStartDTO"), "Virtuademy.ScriptingApi.ExperienceStartDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceStepCompleteDTO"), "Virtuademy.ScriptingApi.ExperienceStepCompleteDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceStepDTO"), "Virtuademy.ScriptingApi.ExperienceStepDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceStepStartDTO"), "Virtuademy.ScriptingApi.ExperienceStepStartDTO"),
            (Identifier("Virtuademy.SDK.ApiData.ExperienceTranscriptDTO"), "Virtuademy.ScriptingApi.ExperienceTranscriptDTO"),
            (Identifier("Virtuademy.SDK.ApiData.SettableFieldAttribute"), "Virtuademy.ScriptingApi.SettableFieldAttribute"),
            (Identifier("Virtuademy.SDK.ApiData.XAPIObject"), "Virtuademy.ScriptingApi.XAPIObject"),
            (Identifier("Virtuademy.SDK.ApiData.XAPIStatement"), "Virtuademy.ScriptingApi.XAPIStatement"),
            (Identifier("Virtuademy.SDK.ApiData.XAPIVerb"), "Virtuademy.ScriptingApi.XAPIVerb"),
            (Identifier("Virtuademy.SDK.ApiData.EAnalyticType"), "Virtuademy.ScriptingApi.EAnalyticType"),
            (Identifier("Virtuademy.SDK.ApiData.EAnalyticVerb"), "Virtuademy.ScriptingApi.EAnalyticVerb"),
            (Identifier("Virtuademy.SDK.ApiData.EExperienceOutcome"), "Virtuademy.ScriptingApi.EExperienceOutcome"),
            (Identifier("Virtuademy.SDK.ApiData.EExperienceScoringType"), "Virtuademy.ScriptingApi.EExperienceScoringType"),
        };

        /// <summary>
        /// Matches <paramref name="typeName"/> only where an identifier ends, plus whatever
        /// <paramref name="unless"/> excludes.
        /// </summary>
        /// <remarks>
        /// The identifier boundary deliberately allows a dot to follow, because that is how a
        /// namespace rule reaches the types inside it. When the namespace being renamed still has
        /// a child that is staying put, that permissiveness is the problem, and <paramref
        /// name="unless"/> is how the caller says which child.
        /// </remarks>
        private static Regex Boundary(string typeName, string unless = "")
            => new(Regex.Escape(typeName) + unless + "(?![A-Za-z0-9_])", RegexOptions.Compiled);

        /// <summary>
        /// Matches <paramref name="typeName"/> only as a whole identifier: neither glued to a
        /// preceding namespace segment's letters nor extended by a suffix. A dot before it is
        /// fine, which is how the qualified spelling is reached.
        /// </summary>
        private static Regex Identifier(string typeName)
            => new(@"(?<![A-Za-z0-9_])" + Regex.Escape(typeName) + @"(?![A-Za-z0-9_])", RegexOptions.Compiled);

        /// <summary>
        /// An assembly-qualified name of one of our types: <c>Full.Type.Name, AssemblyName, Version=…</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The rules above rename namespaces, and an assembly-qualified name carries the namespace
        /// twice: once in the type, once in the assembly, which for the old packages was spelled the
        /// same way. Both get rewritten, but the assembly lands on a name that only looks right —
        /// the old placeholders assembly becomes <c>Virtuademy.Environments.ScriptingApi.Placeholders</c>,
        /// while the placeholders live in <c>Virtuademy.Environments.ScriptingApi</c>.
        /// </para>
        /// <para>
        /// It matters because Visual Scripting resolves a typed variable (a graph, object or scene
        /// variable declared with one of our types) through <c>Type.GetType</c> on exactly this string,
        /// which honours the assembly: with the wrong one the variable comes back as
        /// <c>Unknown</c> (found on 2026-10-09). Units are unaffected — their type is stored by full
        /// name alone.
        /// </para>
        /// <para>
        /// So the assembly is not derived from a table but read off the type itself, once the other
        /// rules have given it its final name: <see cref="FixAssemblyQualifiedNames"/> looks the full
        /// name up among the loaded Virtuademy and SPACS assemblies and writes down the one that
        /// declares it. The routine runs with the updated packages loaded, so that is where the type
        /// is. A name that resolves to nothing loaded is left as it is.
        /// </para>
        /// <para>
        /// The separators are matched as any whitespace and kept as found: Unity folds a long YAML
        /// value at its spaces, and in a prefab the line break often falls right after the type's comma.
        /// </para>
        /// </remarks>
        private static readonly Regex AssemblyQualifiedName = new(
            @"(?<![A-Za-z0-9_.+`])(?<type>(?:Virtuademy|SPACS)\.[A-Za-z0-9_.+]+)(?<separator>,\s+)(?<asm>[A-Za-z0-9_.\-]+)(?=,\s+Version=)",
            RegexOptions.Compiled);

        private static Dictionary<string, string> ourTypeAssemblies;

        /// <summary>Full type name → name of the assembly that declares it, for our own assemblies.</summary>
        private static Dictionary<string, string> OurTypeAssemblies
        {
            get
            {
                if (ourTypeAssemblies != null)
                {
                    return ourTypeAssemblies;
                }

                ourTypeAssemblies = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    if (!name.StartsWith(NewBrand + ".", StringComparison.Ordinal) && !name.StartsWith("SPACS.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (System.Reflection.ReflectionTypeLoadException e)
                    {
                        types = e.Types.Where(t => t != null).ToArray();
                    }

                    foreach (Type type in types)
                    {
                        if (type.FullName != null && !ourTypeAssemblies.ContainsKey(type.FullName))
                        {
                            ourTypeAssemblies.Add(type.FullName, name);
                        }
                    }
                }

                return ourTypeAssemblies;
            }
        }

        /// <summary>
        /// Points every assembly-qualified name of our types at the assembly that declares the type.
        /// See <see cref="AssemblyQualifiedName"/>. Idempotent: a name already right is not a hit.
        /// </summary>
        private static string FixAssemblyQualifiedNames(string text, out int hits)
        {
            int count = 0;
            string result = AssemblyQualifiedName.Replace(text, match =>
            {
                string type = match.Groups["type"].Value;
                if (!OurTypeAssemblies.TryGetValue(type, out string assembly)
                    || assembly == match.Groups["asm"].Value)
                {
                    return match.Value;
                }

                count++;
                return type + match.Groups["separator"].Value + assembly;
            });

            hits = count;
            return result;
        }

        private static readonly string[] TextExtensions =
        {
            ".cs", ".asmdef", ".asmref", ".json", ".uxml", ".uss", ".tss",
            ".unity", ".prefab", ".asset", ".md", ".txt",
        };

        private static readonly string[] YamlExtensions = { ".unity", ".prefab", ".asset" };

        /// <summary>What recompiles when rewritten, so the re-save has to wait for the next domain.</summary>
        private static readonly string[] CodeExtensions = { ".cs", ".asmdef", ".asmref" };

        private readonly List<Entry> entries = new();
        private Vector2 scrollPosition;
        private bool hasScanned;
        private bool unpinLockFile = true;
        private bool consolidateFolders = true;
        private bool hasLegacyFolders;
        private List<VirtuademyPOIPageMigrator.Entry> poiPageEntries = new();
        private bool migratePOIPages = true;
        private bool wasResavePending;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            VirtuademyRenameMigrator window = GetWindow<VirtuademyRenameMigrator>(false, WindowTitle, true);
            window.minSize = new Vector2(560, 320);
            window.Show();
            window.ScanProject();
        }

        #region GUI

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan project", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    ScanProject();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(entries.Count == 0))
                {
                    if (GUILayout.Button("Select all", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    {
                        entries.ForEach(e => e.Selected = true);
                    }
                    if (GUILayout.Button("Select none", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        entries.ForEach(e => e.Selected = false);
                    }
                }
            }

            DrawPendingResave();

            if (!hasScanned)
            {
                EditorGUILayout.HelpBox("Press \"Rescan project\" to search for outdated package references.", MessageType.Info);
                return;
            }

            int selectedCount = entries.Count(e => e.Selected);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. Renamed packages, namespaces and types", EditorStyles.boldLabel);

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No outdated reference found. The project is already migrated.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"{entries.Count} file(s), {entries.Sum(e => e.Hits)} occurrence(s). " +
                    "Review the list: exclude files whose matches are narrative content rather than type references.",
                    EditorStyles.miniLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                foreach (Entry entry in entries)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(18));
                        EditorGUILayout.LabelField(entry.Path, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"{entry.Hits}", GUILayout.Width(40));
                        if (GUILayout.Button("Ping", GUILayout.Width(44)))
                        {
                            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.Path);
                            if (asset != null)
                            {
                                EditorGUIUtility.PingObject(asset);
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);
                unpinLockFile = EditorGUILayout.ToggleLeft(
                    "Unpin this project's Virtuademy git packages in Packages/packages-lock.json so UPM re-resolves the renamed ids (recommended). Other packages keep their pinned versions",
                    unpinLockFile);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("2. POI page markers", EditorStyles.boldLabel);

            if (poiPageEntries.Count > 0)
            {
                int pages = poiPageEntries.Sum(e => e.Pages);
                int deadHooks = poiPageEntries.Sum(e => e.DeadHooks);
                migratePOIPages = EditorGUILayout.ToggleLeft(
                    $"Replace the POI page marker in {poiPageEntries.Count} file(s): {pages} page(s)" +
                    (deadHooks > 0 ? $", {deadHooks} unused missing-script hook(s) removed" : string.Empty) +
                    " (required)",
                    migratePOIPages);
                EditorGUILayout.LabelField(
                    "Without this, every POI in a world published from this project shows an empty panel.",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("No POI page uses the old marker. Nothing to migrate.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("3. Generated folders", EditorStyles.boldLabel);

            if (hasLegacyFolders)
            {
                consolidateFolders = EditorGUILayout.ToggleLeft(
                    "Gather Assets/CreatorKit and Assets/ReflectisSettings into Assets/Virtuademy (recommended)",
                    consolidateFolders);
                EditorGUILayout.LabelField(
                    "Assets keep their GUID, so every reference survives. Nothing breaks if this is skipped.",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("No legacy folder in this project. Nothing to consolidate.", MessageType.Info);
            }

            EditorGUILayout.Space(8);

            bool renameWork = selectedCount > 0;
            bool pageWork = poiPageEntries.Count > 0 && migratePOIPages;
            bool folderWork = hasLegacyFolders && consolidateFolders;

            if (!renameWork && !pageWork && !folderWork)
            {
                EditorGUILayout.HelpBox("Nothing selected to apply.", MessageType.Info);
                EditorGUILayout.Space(4);
                return;
            }

            EditorGUILayout.HelpBox(
                "Files are rewritten and moved in place. Make sure the project is committed to version control " +
                "(or backed up) before applying.",
                MessageType.Warning);

            List<string> steps = new();
            if (renameWork)
            {
                steps.Add($"rename in {selectedCount} file(s)");
            }
            if (pageWork)
            {
                steps.Add($"POI pages in {poiPageEntries.Count} file(s)");
            }
            if (folderWork)
            {
                steps.Add("folder consolidation");
            }

            if (GUILayout.Button($"Apply update ({string.Join(" + ", steps)})", GUILayout.Height(30)))
            {
                Apply(renameWork, pageWork, folderWork);
            }

            EditorGUILayout.Space(4);
        }

        /// <summary>The re-save runs by itself; this says what it is waiting for, and offers to run it
        /// once nothing stands in the way — for when the wait never ends on its own.</summary>
        private static void DrawPendingResave()
        {
            if (!VirtuademyUpdateResave.IsPending)
            {
                return;
            }

            if (VirtuademyUpdateResave.IsWaitingForReload || EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox(
                    "The files changed by the update will be re-saved once Unity has recompiled. Do not save them by hand.",
                    MessageType.Info);
                return;
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                EditorGUILayout.HelpBox(
                    "The files changed by the update are waiting to be re-saved, and will be after the compile errors are fixed.",
                    MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("The files changed by the update are waiting to be re-saved.", MessageType.Info);
                if (GUILayout.Button("Re-save now", GUILayout.Width(100), GUILayout.Height(38)))
                {
                    VirtuademyUpdateResave.Run();
                }
            }
        }

        private void OnInspectorUpdate()
        {
            // While pending, and once more when it stops being, so the box goes away.
            bool pending = VirtuademyUpdateResave.IsPending;
            if (pending || wasResavePending)
            {
                Repaint();
            }
            wasResavePending = pending;
        }

        #endregion

        #region Scan / apply

        private void ScanProject()
        {
            entries.Clear();

            try
            {
                int index = 0;
                List<string> candidates = CandidateFiles().ToList();
                foreach (string path in candidates)
                {
                    EditorUtility.DisplayProgressBar(WindowTitle, path, (float)index++ / candidates.Count);

                    string text = ReadTextOrNull(path);
                    if (text == null)
                    {
                        continue;
                    }

                    int hits = CountHits(text);
                    if (hits > 0)
                    {
                        entries.Add(new Entry { Path = path.Replace('\\', '/'), Hits = hits });
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            poiPageEntries = VirtuademyPOIPageMigrator.Scan();
            hasLegacyFolders = VirtuademyFolderMigrator.HasLegacyFolders();
            hasScanned = true;
            Repaint();
        }

        private void Apply(bool rewriteFiles, bool migratePages, bool consolidate)
        {
            List<Entry> selected = rewriteFiles
                ? entries.Where(e => e.Selected).ToList()
                : new List<Entry>();

            List<string> steps = new();
            if (rewriteFiles)
            {
                steps.Add($"rewrite {selected.Count} file(s) in place ({selected.Sum(e => e.Hits)} occurrence(s))");
            }
            if (migratePages)
            {
                steps.Add($"replace the POI page marker in {poiPageEntries.Count} file(s)");
            }
            if (consolidate)
            {
                steps.Add("gather the legacy folders into Assets/Virtuademy");
            }

            string what = string.Join(", then ", steps);
            what = char.ToUpperInvariant(what[0]) + what.Substring(1);

            if (!EditorUtility.DisplayDialog(
                    WindowTitle,
                    what + "?\n\nMake sure the project is committed / backed up first.",
                    "Apply", "Cancel"))
            {
                return;
            }

            // A scene open while its file is rewritten under it comes back as a "modified externally"
            // prompt, and a later save of it would write the old content back over the rewrite. Close
            // it first; the re-save opens it again when it is done.
            //
            // Closed WITHOUT saving, never through the usual "save modified scenes?" prompt. An open
            // scene was deserialized by the updated packages under its old type names, so its graphs
            // hold MissingType units with renumbered JSON ids; saving it writes that out, and no rewrite
            // brings the graph back ("Object definition has not been encountered for object with id=N"
            // on every later open — reproduced on 2026-10-09). Its unsaved changes are lost instead, and
            // the creator is told so before anything happens.
            HashSet<string> toRewrite = new(
                selected.Select(e => e.Path).Concat(migratePages ? poiPageEntries.Select(e => e.Path) : Enumerable.Empty<string>()),
                StringComparer.OrdinalIgnoreCase);
            List<Scene> openSceneList = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(s => !string.IsNullOrEmpty(s.path))
                .ToList();
            List<string> openScenes = openSceneList.Select(s => s.path).ToList();
            List<string> reopenScenes = new();
            if (openScenes.Any(toRewrite.Contains))
            {
                List<string> dirty = openSceneList.Where(s => s.isDirty).Select(s => s.path).ToList();
                if (dirty.Count > 0 && !EditorUtility.DisplayDialog(
                        WindowTitle,
                        "These open scenes have unsaved changes:\n\n  " + string.Join("\n  ", dirty) +
                        "\n\nThey are closed WITHOUT saving. Saved now, before the update, their Visual Scripting " +
                        "graphs would be written with the nodes the updated packages cannot resolve yet, and could " +
                        "not be recovered. Their unsaved changes are lost.",
                        "Discard changes and apply", "Cancel"))
                {
                    return;
                }
                reopenScenes = openScenes;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            int changedFiles = 0, failed = 0, changedPages = 0, failedPages = 0;
            List<string> changedPaths = new();
            bool lockUnpinned = false;

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    Entry entry = selected[i];
                    EditorUtility.DisplayProgressBar(WindowTitle, entry.Path, (float)i / selected.Count);

                    try
                    {
                        string text = ReadTextOrNull(entry.Path);
                        if (text == null)
                        {
                            continue;
                        }

                        string rewritten = Rewrite(text);
                        if (!string.Equals(rewritten, text, StringComparison.Ordinal))
                        {
                            File.WriteAllText(entry.Path, rewritten, new UTF8Encoding(HasUtf8Bom(entry.Path)));
                            changedFiles++;
                            changedPaths.Add(entry.Path);
                        }
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Debug.LogError($"[{WindowTitle}] Failed to rewrite {entry.Path}: {e}");
                    }
                }

                // After the rename, which may have rewritten the same scenes: the page pass re-reads
                // every file it touches, so it works on what the rename left.
                if (migratePages)
                {
                    EditorUtility.DisplayProgressBar(WindowTitle, "POI page markers", 1f);
                    changedPages = VirtuademyPOIPageMigrator.Apply(poiPageEntries, out failedPages, changedPaths);
                }

                // Only our own git packages: deleting the whole lock also unpinned every third-party
                // git dependency, and one with no #ref then resolved to whatever its default branch
                // is today (see VirtuademyPackageLock).
                if (rewriteFiles && unpinLockFile)
                {
                    List<string> unpinned = VirtuademyPackageLock.UnpinOurGitPackages();
                    lockUnpinned = unpinned.Count > 0;
                    if (lockUnpinned)
                    {
                        Debug.Log($"[{WindowTitle}] Unpinned {unpinned.Count} git package(s) in {VirtuademyPackageLock.LockPath} " +
                                  "so UPM resolves them again; every other entry keeps its pinned version:\n  " +
                                  string.Join("\n  ", unpinned));
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (lockUnpinned)
            {
                // Explicitly, rather than trusting the refresh to notice: the re-save waits for the
                // unpinned entries to come back, and only a resolve writes them.
                UnityEditor.PackageManager.Client.Resolve();
            }

            // After the refresh, which is what gives a rewritten file its place in the AssetDatabase
            // under the path the scan saw; before the folder move, which changes that path. The re-save
            // records GUIDs, so the move does not lose it.
            List<string> toResave = changedPaths
                .Where(p => YamlExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            bool codeChanged = changedPaths.Any(p =>
                CodeExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
            VirtuademyUpdateResave.Schedule(toResave, reopenScenes, lockUnpinned, codeChanged);

            // After the rewrite, never before: the rewrite writes to the paths the scan recorded,
            // and moving a folder out from under it would send those writes to files that moved.
            VirtuademyFolderMigrator.Result folders = consolidate
                ? VirtuademyFolderMigrator.Consolidate()
                : default;

            RebuildVisualScriptingUnits();

            string report = "Done.\n";

            if (rewriteFiles)
            {
                report += $"\nRewritten: {changedFiles}\nFailed: {failed}";
            }

            if (migratePages)
            {
                report += $"\nPOI page markers replaced in: {changedPages} file(s)" +
                          (failedPages > 0 ? $", {failedPages} failed" : string.Empty);
            }

            if (consolidate)
            {
                report += $"\nMoved into Assets/Virtuademy: {folders.Moved} asset(s), " +
                          $"{folders.Pruned} folder(s) removed" +
                          (folders.Refused > 0 ? $", {folders.Refused} left in place" : string.Empty);
            }

            EditorUtility.DisplayDialog(
                WindowTitle,
                report +
                (failed > 0 || failedPages > 0 || folders.Refused > 0 ? "\n\nSee the Console for details." : string.Empty) +
                (toResave.Count > 0
                    ? $"\n\nUnity will now recompile. Afterwards the {toResave.Count} scene(s), prefab(s) and asset(s) " +
                      "changed by the update are re-saved automatically, without opening them; you will get a report. " +
                      "Do not save them by hand before that."
                    : "\n\nUnity will now recompile."),
                "OK");

            ScanProject();
        }

        private static IEnumerable<string> CandidateFiles()
        {
            foreach (string path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (TextExtensions.Contains(extension))
                {
                    yield return path;
                }
            }

            // The UPM manifest carries the package ids. (The lock file is not rewritten: its entries
            // for our git packages are removed instead, by VirtuademyPackageLock.)
            if (File.Exists("Packages/manifest.json"))
            {
                yield return "Packages/manifest.json";
            }
        }

        /// <summary>Reads the file as text; returns null for binary content (NUL bytes,
        /// or a scene/prefab/asset that is not text-serialized YAML).</summary>
        private static string ReadTextOrNull(string path)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return null;
            }

            int probe = Math.Min(bytes.Length, 8000);
            for (int i = 0; i < probe; i++)
            {
                if (bytes[i] == 0)
                {
                    return null;
                }
            }

            string text = new UTF8Encoding(false).GetString(bytes);
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (YamlExtensions.Contains(extension) && !text.StartsWith("%YAML", StringComparison.Ordinal))
            {
                return null;
            }

            return text;
        }

        private static bool HasUtf8Bom(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return stream.Length >= 3 && stream.ReadByte() == 0xEF && stream.ReadByte() == 0xBB && stream.ReadByte() == 0xBF;
        }

        private static int CountHits(string text)
        {
            // Mirror Apply exactly: literal replacements first, then count what the generic
            // rules would still match on the intermediate text (avoids double counting).
            int hits = 0;
            foreach ((string oldValue, string newValue) in LiteralMap)
            {
                int index = 0;
                while ((index = text.IndexOf(oldValue, index, StringComparison.Ordinal)) >= 0)
                {
                    hits++;
                    index += oldValue.Length;
                }

                text = text.Replace(oldValue, newValue);
            }

            hits += GenericNamespaceRule.Matches(text).Count;
            text = GenericNamespaceRule.Replace(text, NewBrand + ".");
            hits += EditorNamespaceRule.Matches(text).Count;
            text = EditorNamespaceRule.Replace(text, NewBrand + "Editor.");

            foreach ((string oldValue, string newValue) in EnvironmentsMap)
            {
                int index = 0;
                while ((index = text.IndexOf(oldValue, index, StringComparison.Ordinal)) >= 0)
                {
                    hits++;
                    index += oldValue.Length;
                }

                text = text.Replace(oldValue, newValue);
            }

            foreach ((Regex rule, string newValue) in TypeMap)
            {
                hits += rule.Matches(text).Count;
                text = rule.Replace(text, newValue);
            }

            text = SupplementUtilitiesUsing(text, out int supplemented);
            hits += supplemented;

            FixAssemblyQualifiedNames(text, out int requalified);
            hits += requalified;

            return hits;
        }

        /// <summary>
        /// True when the file at <paramref name="path"/> is a text-serialized scene, prefab or asset
        /// whose Visual Scripting data still names something this routine renames. That data is the
        /// <c>_json</c> field of a machine, a graph asset or a Variables component; other matches in
        /// the file (a label, a narrative text) do not count.
        /// </summary>
        /// <remarks>Used by <see cref="VirtuademyUpdateSaveGuard"/>, which refuses to save such a file.</remarks>
        internal static bool HasUnmigratedGraphData(string path)
        {
            if (string.IsNullOrEmpty(path)
                || !YamlExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())
                || !File.Exists(path))
            {
                return false;
            }

            string text = ReadTextOrNull(path);
            return text != null && GraphJsonValues(text).Any(json => CountHits(json) > 0);
        }

        private const string JsonKey = "_json: '";

        /// <summary>
        /// The <c>_json</c> values of a YAML file, as written. Unity writes them as single-quoted
        /// scalars and folds a long one over several lines at its spaces (a prefab's graph, typically),
        /// so a value is read up to its closing quote rather than to the end of its line; a quote
        /// inside it is doubled.
        /// </summary>
        private static IEnumerable<string> GraphJsonValues(string text)
        {
            for (int start = text.IndexOf(JsonKey, StringComparison.Ordinal);
                 start >= 0;
                 start = text.IndexOf(JsonKey, start, StringComparison.Ordinal))
            {
                start += JsonKey.Length;
                int end = start;
                while (end < text.Length && (text[end] != '\'' || (end + 1 < text.Length && text[end + 1] == '\'')))
                {
                    end += text[end] == '\'' ? 2 : 1;
                }

                yield return text.Substring(start, end - start);
                start = end;
            }
        }

        private static string Rewrite(string text)
        {
            foreach ((string oldValue, string newValue) in LiteralMap)
            {
                text = text.Replace(oldValue, newValue);
            }

            text = GenericNamespaceRule.Replace(text, NewBrand + ".");
            text = EditorNamespaceRule.Replace(text, NewBrand + "Editor.");

            foreach ((string oldValue, string newValue) in EnvironmentsMap)
            {
                text = text.Replace(oldValue, newValue);
            }

            foreach ((Regex rule, string newValue) in TypeMap)
            {
                text = rule.Replace(text, newValue);
            }

            text = SupplementUtilitiesUsing(text, out _);
            text = FixAssemblyQualifiedNames(text, out _);

            return text;
        }

        /// <summary>
        /// A file that imports the old utilities namespace gets <c>using SPACS.Utilities;</c> added
        /// beside it, once: the moved extension methods resolve again, and whatever still lives in
        /// the old namespace (JwtToken, HmacCredential) keeps resolving too. Idempotent — a file that
        /// already imports SPACS.Utilities is left alone.
        /// </summary>
        private static string SupplementUtilitiesUsing(string text, out int hits)
        {
            hits = 0;
            string oldUsing = "using " + OldCoreUtilities + ";";
            if (!text.Contains(oldUsing) || text.Contains("using SPACS.Utilities;"))
            {
                return text;
            }

            int index = text.IndexOf(oldUsing, StringComparison.Ordinal);
            string newline = index >= 2 && text[index - 2] == '\r' && text[index - 1] == '\n' ? "\r\n" : "\n";

            hits = 1;
            return text.Insert(index + oldUsing.Length, newline + "using SPACS.Utilities;");
        }

        /// <summary>Rebuilds the Visual Scripting node library so the renamed unit types are
        /// picked up. Done via reflection so this assembly does not depend on Visual Scripting.</summary>
        private static void RebuildVisualScriptingUnits()
        {
            try
            {
                Type unitBase = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Unity.VisualScripting.UnitBase"))
                    .FirstOrDefault(t => t != null);

                unitBase?.GetMethod("Rebuild", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, null);

                if (unitBase != null)
                {
                    Debug.Log($"[{WindowTitle}] Visual Scripting node library rebuilt.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{WindowTitle}] Could not rebuild the Visual Scripting node library automatically " +
                                 $"({e.Message}). Run it manually: Edit > Project Settings > Visual Scripting > Regenerate Nodes.");
            }
        }

        #endregion
    }
}

namespace Virtuademy.BreakingChangeSolvers
{
    // Here rather than in the migrator's namespace: VirtuademyUpdateResave, which lives in this
    // one, reads it too, and the migrator already imports it.
    /// <summary>
    /// What the v2026.5 -> v2026.6 routine does to <c>Packages/packages-lock.json</c>: it unpins this
    /// project's own git packages and leaves every other entry as it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lock pins each git dependency to a resolved commit. The renamed packages need to be looked
    /// up again, so their entries have to go. The routine used to delete the whole file, and that
    /// also unpinned every third-party git dependency — and a third-party URL with no <c>#ref</c>
    /// then resolved to whatever that repository's default branch is today. Found 2026-09-25 on a
    /// creator project: <c>https://github.com/mob-sakai/UIEffect.git</c>, pinned for years to a
    /// commit with <c>package.json</c> at the root, re-resolved to a <c>main</c> that keeps it under
    /// <c>Packages/src</c>, and UPM stopped with "Repository does not contain a package manifest".
    /// </para>
    /// <para>
    /// So only the entries whose id carries one of our prefixes and whose source is git are removed —
    /// the same rule the setup window's re-resolve button applies. A registry or embedded package of
    /// ours is not pinned by commit and is left alone.
    /// </para>
    /// <para>
    /// The file is edited as text through a small scanner rather than a JSON library: these scripts
    /// compile into the creator project's own editor assembly, which is not guaranteed to reference
    /// one. The scanner only has to find the members of two objects; the kept entries are copied
    /// byte for byte, and UPM rewrites the whole file on its next resolve anyway.
    /// </para>
    /// </remarks>
    internal static class VirtuademyPackageLock
    {
        public const string LockPath = "Packages/packages-lock.json";
        public const string ManifestPath = "Packages/manifest.json";

        // The same three prefixes the installer uses: every name a package of ours has shipped under.
        private static readonly string[] OurPrefixes =
        {
            "com.anotherealitysrl.virtuademy",
            "com.anotherealitysrl.reflectis",
            "com.anotherealitysrl.spacs",
        };

        private static bool IsOurs(string id)
            => OurPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));

        /// <summary>
        /// Removes this project's own git packages from the lock file. Returns the ids removed; an
        /// empty list when there was nothing to unpin, no lock file, or a lock this scanner cannot
        /// read (in which case the file is left untouched and a warning says so).
        /// </summary>
        public static List<string> UnpinOurGitPackages()
        {
            List<string> removed = new();
            if (!File.Exists(LockPath))
            {
                return removed;
            }

            string text = File.ReadAllText(LockPath);
            if (!TryGetDependencies(text, out int open, out int close, out List<Member> members))
            {
                Debug.LogWarning($"[{nameof(VirtuademyPackageLock)}] {LockPath} could not be read, so nothing was unpinned. " +
                                 "Press \"Re-resolve packages\" in the setup window once the routine is done.");
                return removed;
            }

            List<Member> kept = new();
            foreach (Member member in members)
            {
                if (IsOurs(member.Key) && IsGitEntry(text, member))
                {
                    removed.Add(member.Key);
                }
                else
                {
                    kept.Add(member);
                }
            }

            if (removed.Count == 0)
            {
                return removed;
            }

            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string memberIndent = IndentBefore(text, members[0].Start);
            string closeIndent = IndentBefore(text, close);

            StringBuilder builder = new();
            builder.Append(text, 0, open + 1);
            for (int i = 0; i < kept.Count; i++)
            {
                builder.Append(i == 0 ? newline : "," + newline);
                builder.Append(memberIndent);
                builder.Append(text, kept[i].Start, kept[i].End - kept[i].Start);
            }
            builder.Append(newline).Append(closeIndent);
            builder.Append(text, close, text.Length - close);

            File.WriteAllText(LockPath, builder.ToString());
            return removed;
        }

        /// <summary>
        /// True once UPM has resolved again after <see cref="UnpinOurGitPackages"/>: the lock file
        /// exists and carries an entry for every git package of ours the manifest names. The manifest
        /// is the right yardstick because the unpinned entries come back exactly for the packages it
        /// still lists, while a package the routine renamed away never does.
        /// </summary>
        public static bool OurGitPackagesResolved()
        {
            if (!File.Exists(LockPath))
            {
                return false;
            }
            if (!File.Exists(ManifestPath))
            {
                return true;
            }

            string lockText = File.ReadAllText(LockPath);
            string manifestText = File.ReadAllText(ManifestPath);
            if (!TryGetDependencies(lockText, out _, out _, out List<Member> locked)
                || !TryGetDependencies(manifestText, out _, out _, out List<Member> declared))
            {
                // Unreadable is not "still resolving": waiting on it would wait forever.
                return true;
            }

            HashSet<string> lockedIds = new(locked.Select(m => m.Key), StringComparer.Ordinal);
            return declared
                .Where(m => IsOurs(m.Key) && IsGitReference(StringValue(manifestText, m)))
                .All(m => lockedIds.Contains(m.Key));
        }

        #region Scanner

        private struct Member
        {
            public string Key;
            public int Start;       // the opening quote of the key
            public int ValueStart;
            public int End;         // one past the value
        }

        private static bool TryGetDependencies(string text, out int open, out int close, out List<Member> members)
        {
            open = close = -1;
            members = null;
            try
            {
                int root = SkipWhitespace(text, 0);
                if (root >= text.Length || text[root] != '{')
                {
                    return false;
                }

                Member? dependencies = ReadMembers(text, root, out _)
                    .Cast<Member?>()
                    .FirstOrDefault(m => m.Value.Key == "dependencies");
                if (dependencies == null || text[dependencies.Value.ValueStart] != '{')
                {
                    return false;
                }

                open = dependencies.Value.ValueStart;
                members = ReadMembers(text, open, out close);
                return members.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The members of the object whose '{' is at <paramref name="open"/>;
        /// <paramref name="close"/> is the index of its '}'.</summary>
        private static List<Member> ReadMembers(string text, int open, out int close)
        {
            List<Member> members = new();
            int i = open + 1;
            while (true)
            {
                i = SkipWhitespace(text, i);
                if (text[i] == ',')
                {
                    i++;
                    continue;
                }
                if (text[i] == '}')
                {
                    close = i;
                    return members;
                }
                if (text[i] != '"')
                {
                    throw new FormatException($"Expected a key at {i}.");
                }

                int start = i;
                int keyEnd = SkipString(text, i);
                string key = text.Substring(start + 1, keyEnd - start - 2);
                i = SkipWhitespace(text, keyEnd);
                if (text[i] != ':')
                {
                    throw new FormatException($"Expected ':' at {i}.");
                }
                int valueStart = SkipWhitespace(text, i + 1);
                int end = SkipValue(text, valueStart);
                members.Add(new Member { Key = key, Start = start, ValueStart = valueStart, End = end });
                i = end;
            }
        }

        private static int SkipValue(string text, int i)
        {
            char c = text[i];
            if (c == '"')
            {
                return SkipString(text, i);
            }
            if (c == '{' || c == '[')
            {
                int depth = 0;
                while (true)
                {
                    char d = text[i];
                    if (d == '"')
                    {
                        i = SkipString(text, i);
                        continue;
                    }
                    if (d == '{' || d == '[')
                    {
                        depth++;
                    }
                    else if (d == '}' || d == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return i + 1;
                        }
                    }
                    i++;
                }
            }
            while (i < text.Length && text[i] != ',' && text[i] != '}' && text[i] != ']' && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            return i;
        }

        /// <summary>From the opening quote to one past the closing one.</summary>
        private static int SkipString(string text, int i)
        {
            i++;
            while (text[i] != '"')
            {
                i += text[i] == '\\' ? 2 : 1;
            }
            return i + 1;
        }

        private static int SkipWhitespace(string text, int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            return i;
        }

        private static string IndentBefore(string text, int index)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            string before = text.Substring(lineStart, index - lineStart);
            return before.Trim().Length == 0 ? before : "";
        }

        private static bool IsGitEntry(string text, Member member)
        {
            if (text[member.ValueStart] != '{')
            {
                return false;
            }
            Member? source = ReadMembers(text, member.ValueStart, out _)
                .Cast<Member?>()
                .FirstOrDefault(m => m.Value.Key == "source");
            return source != null && StringValue(text, source.Value) == "git";
        }

        private static string StringValue(string text, Member member)
            => text[member.ValueStart] == '"'
                ? text.Substring(member.ValueStart + 1, member.End - member.ValueStart - 2)
                : null;

        /// <summary>A manifest value UPM resolves through git: a URL, not a version or a file path.</summary>
        private static bool IsGitReference(string value)
            => value != null
               && !value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
               && (value.Contains("://") || value.StartsWith("git@", StringComparison.Ordinal));

        #endregion
    }
}
