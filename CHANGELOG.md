# Release notes

## Unreleased

### Changed
- **The graph, task and dialog engines are `SPACS-Graphs`, `SPACS-Tasks` and `SPACS-Dialogs`**
  (ids `com.anotherealitysrl.spacs-{graphs,tasks,dialogs}` 3.0.0, were
  `virtuademy-sdk-{graphs,tasks,dialogs}`), and this package's dependencies follow. The
  `Virtuademy-SDK-*` prefix is kept for the SDK proper — Core, Environments, Library; what carries
  no platform is `SPACS-*`. Their assemblies and namespaces were already `SPACS.*`, so nothing a
  published world or a graph records changes.
- **The package rename migrator carries the three ids and repository names.** A manifest key
  `virtuademy-sdk-graphs` (a project on the develop channel) or `reflectis-sdk-graphs` (a 2026.5
  project, through the brand rule first) lands on `spacs-graphs`, and the same for tasks and dialogs.
  The key is what matters: UPM rejects a git dependency whose key is not the id in the fetched
  `package.json`.
- **Four world-space UI helpers leave the old namespace.** `WorldSpaceButtonBinder`,
  `WorldSpacePanelImageBinder`, `WorldSpacePOIToggle` and `WorldSpaceUIDocumentRebuilder` were
  declared in `Reflectis.CreatorKit.Worlds.Placeholders`, which no other type uses; they are in
  `Virtuademy.SDK.Environments.Utilities`, beside the other utilities, and the inspector of the
  first in `...Utilities.Editor`. No `[RenamedFrom]`: the four were never released, prefabs
  reference them by script GUID, and no graph names them.
- The HybridCLR verifier's menu root (its entries are commented out) is `Virtuademy/Security/`,
  and editor messages and comments stop calling the project and the setup window "Creator Kit".
- `Documentation~/index.md` replaces the one-line stub `index.md.txt`, which still said
  `Virtuademy-SDK-CreatorKit` and was not the file the README links to.
- **The scene list of the Addressables management window is a registry of the project's scenes.**
  Every `.unity` under `Assets/` is listed (packages excluded), each with its own "Include in build"
  and platforms; new scenes appear on their own, unticked, deleted ones disappear, the settings of
  the scenes already listed are kept (the entry follows the scene through renames and moves). The
  add/remove buttons are gone, replaced by a search box, an "Only in build" filter and a
  publication filter (all / published / not published). Nothing is published because it was added
  by hand, nothing is forgotten because it was not. Scenes that are not the project's own are left
  out: under one of the excluded folders (package samples, third-party assets, plugins — the list
  starts at `Assets/Samples`, `Assets/Plugins`, `Assets/_ThirdParty`, `Assets/ThirdParty`,
  `Assets/Third Party`, `Assets/StreamingAssets`, `Assets/TextMesh Pro` and is edited from the
  "Excluded folders" foldout under the list), inside an embedded or local package, or in a folder
  carrying a `package.json`.
- **Every selected world says what the build does to it.** Under each ticked world (and under
  "Deploy to tenant"): the scenes that overwrite an environment already published with that name,
  in orange, and the ones that arrive as new environments, in green. It follows "Include in build"
  as you tick.

### Added
- **Each scene of the Addressables management window says where it is already published.** Under
  every entry of the scene list: "Published in Showroom, Academy", "Published at tenant level",
  "Not published yet" — with catalog, status and last update in the tooltip. The window reads it
  from the Application API right after loading the worlds (all the worlds the account can see) and
  again after every deploy. Groundwork for the 2026.6 republish campaign (ADR 0021: every world
  built with the Reflectis SDK is rebuilt with this one): a scene published in several worlds is
  the candidate to move to tenant level before rebuilding.
- **The publication filter narrows to one world.** After All / Published / Not published, the
  dropdown lists "Tenant level" (when the account can read it) and one "World: …" per world the
  account can see, each with the number of the project's scenes published there — "World:
  Showroom (3)". A tenant environment shared to a world counts as tenant level, as in the
  "Published in" line.

### Fixed
- **The interpreter setup drops missing asmdefs from HybridCLR's Hot Update Assembly Definitions.**
  The list only ever grew: an asmdef deleted or replaced — a hot-update folder removed and set up
  again — stayed registered as a missing reference beside the one in use. `RegisterHotUpdateAssembly`
  now removes the entries whose asset no longer exists, and only those.
- **"Install interpreter" configures the project in one click.** After the window installs the
  HybridCLR package, `HotUpdateSetupper` finishes the setup on the reload that follows. It ran
  inside `[InitializeOnLoadMethod]`, where asset database work is deferred: the hot-update asmdef was
  renamed but could not be loaded, so its registration in HybridCLR's Hot Update Assembly
  Definitions failed, the project was not ready to build, and the log still said the configuration
  had completed. The setup now runs on the first editor tick after the reload, imports the asmdef
  synchronously before registering it, and keeps its pending flag until the setup passes — retried
  on the reload a rename causes, up to three times, then reported as an error. `Setup()` logs an
  incomplete run as a warning instead of an error.
- **UI Toolkit localized elements translate in a player build again.** Since the `LocalizedXxx`
  elements moved here, I2Loc reaches them through `I2LocalizedTextProvider`, in the
  `Virtuademy.SDK.Environments.I2Loc` assembly, which nothing references: its only entry point is a
  `[RuntimeInitializeOnLoadMethod]`. The player's managed code stripping dropped the whole assembly,
  the provider was never registered, and every element kept the text authored in its UXML, with no
  error — on WebGL, mobile and VR alike. The Editor hid it, because `[InitializeOnLoadMethod]`
  registers the provider there anyway. The assembly now carries `[assembly: AlwaysLinkAssembly]`
  and the method `[Preserve]`. The assembly still compiles only when `I2LOC` is defined, so a
  project without I2Loc gets neither.
- **Language changes reach the localized elements again after a play session in the Editor.** I2
  sets `OnLocalizeEvent` to null when the Editor exits play mode, and the provider subscribed only
  once, so edit-mode previews stopped following the language until the next domain reload. It now
  subscribes on every registration and registers again when the Editor is back in edit mode.
- **The v2026.5 -> v2026.6 update routine no longer unpins third-party git packages.** It used to
  delete `Packages/packages-lock.json` so that the renamed packages were looked up again, and that
  also released every other git dependency from the commit it was pinned to. One with no `#ref` then
  resolved to whatever its repository's default branch is today — on a creator project,
  `https://github.com/mob-sakai/UIEffect.git`, whose `main` keeps `package.json` under
  `Packages/src`, so UPM stopped with "Repository does not contain a package manifest". The routine
  now removes from the lock only the entries of our own git packages (ids starting with
  `com.anotherealitysrl.virtuademy`, `.reflectis` or `.spacs`), the same rule as the setup window's
  re-resolve button; every other entry keeps its pinned commit or version. The re-save step that
  used to wait for the lock file to reappear now waits until every git package of ours the manifest
  names is back in it, which also covers a re-save scheduled by the previous version.
- **Logging out forgets where the scenes are published.** The per-world choices, their counts, the
  "Published in" lines and an active publication filter stayed from the ended session; they are
  cleared now, and a refresh still in flight at logout drops its results instead of bringing them
  back.
- **The `v2025.3 -> v2025.4` update routine no longer hangs or aborts.** It processes only the
  prefabs and scenes that use `InteractablePlaceholderObsolete` or the detectors, once each and in
  dependency order (nested prefabs and variant bases first), instead of every prefab and scene three
  times with source prefabs reloaded per instance. Obsolete components added on model (`.fbx`)
  instances are migrated on the instance instead of throwing, inherited components are left to
  their source prefab, read-only package prefabs are skipped. The progress bar is cancelable, a
  failing asset is reported at the end instead of stopping the routine, and unsaved scenes are
  offered for saving before it starts.

## v10.0.0

### Changed
- **The last nine types named after the old brand are renamed, and the nodes stop saying it.**
  `TaskReflectis`, `TaskSystemReflectis`, `TaskReflectisStepSetter`, the four task adapters
  (`Animator`/`Grab`/`Timer`/`TriggerTaskReflectis`), `TaskSystemReflectisEditor` and
  `ReflectisChatbotPlaceholder` become their `Virtuademy` spellings (`TaskVirtuademy`,
  `TaskSystemVirtuademy`, `VirtuademyChatbotPlaceholder`, …); the prefab
  `ReflectisChatBotPlaceholder` follows. The 2026-08 brand rename had kept class names on purpose,
  as wire symbols: a published bundle records a component by class and assembly, and a graph
  records a type by its full name. Both are true, and both are why the rename lands **now** — every
  published world already has to be rebuilt at the cutover for the assembly rename, so the class
  rename rides the same rebuild and costs nothing extra; done later it would force a second one.

  Script GUIDs are preserved (file rename, same `.meta`), so scenes and prefabs need nothing. Each
  runtime type carries `[RenamedFrom]` with its previous full name, so a graph that still records
  it deserializes; the rename migrator gained the nine names as whole-identifier rules for the
  creator's own C# and for text the attribute cannot reach.

  Every Visual Scripting node title, surtitle and category drops the old brand:
  `Virtuademy\Flow`, `Virtuademy\Get`, `Virtuademy\Expose`, `Virtuademy\Create`,
  `Events\Virtuademy`, `Virtuademy Scene: On Load`, and so on. Categories and titles are
  metadata, not identity — a graph references the unit type — so nothing breaks, but the fuzzy
  finder needs **Regenerate Nodes** once. The `<remarks>` on the scripting-API interfaces that
  quote node names follow.

  `DialogPanelSpawner.useReflectisNickname` / `useReflectisAvatar` become `useVirtuademyNickname`
  / `useVirtuademyAvatar` — **without** `[FormerlySerializedAs]` or `[RenamedFrom]`, by choice.
  Both flags are serialized in a creator's scenes and prefabs and reachable from a graph by member
  name, and the rename migrator carries them: it rewrites the YAML key and the graph member in one
  pass, after which the worlds are rebuilt. A published bundle that skipped the pass reads the two
  flags as `false`; it is rebuilt at the cutover regardless.

  **The scripting defines follow.** `REFLECTIS_CREATOR_KIT_WORLDS_{PLACEHOLDERS,TASKS,
  VISUAL_SCRIPTING,DIALOGS,ANALYTICS}` are `VIRTUADEMY_ENVIRONMENTS_*`, and the platform
  selectors `REFLECTIS_{DESKTOP,MOBILE,VR}` read by `CheckPlatformUnit` are `VIRTUADEMY_*`. The
  five `[InitializeOnLoad]` registrars that add the module symbols to PlayerSettings now retire
  the old symbol first, so a project updating the package is left with the new set and no
  stragglers. The rename migrator rewrites all eight as whole identifiers, for the `#if` lines
  of a creator's own scripts. A creator project that referenced the old symbols by hand needs
  either the migrator pass or a manual edit; nothing else in a world depends on them.
- **The rename migrator no longer takes `JwtToken` away.** Its `Virtuademy.SDK.Core.Utilities ->
  SPACS.Utilities` rule assumed that namespace had emptied except for an editor drawer; it had not —
  `JwtToken` and `HmacCredential` still live there, in Virtuademy-SDK-Core — so a plain rewrite of
  `using Virtuademy.SDK.Core.Utilities;` broke every file that names them (CS0246, found running
  the tool on the application on 2026-09-21). The rule now skips those two types and never touches
  a `using` line; instead, a file importing the old namespace gets `using SPACS.Utilities;` added
  beside it, once, so the moved utilities resolve and what stayed keeps resolving.
- **One update routine for the release, named after it.** The two entries a creator had to find
  and run in the right order — `Package rename migration` and `Consolidate the Virtuademy folder`
  — are a single menu item, `Virtuademy/Update routines/v2026.5 -> v2026.6`, matching how every
  other release's migration is named. One window, two sections, one Apply: the text rewrite runs
  first and the folder consolidation after it, which is the order that works and not one a
  creator should have had to know. Each half is still skippable — the file list keeps its
  per-file checkboxes, the consolidation is a toggle — and either section reports that the
  project is already migrated rather than disappearing, so a project that needs only one of the
  two still reaches the button. `VirtuademyFolderMigrator` lost its `[MenuItem]` and is driven by
  the window.
- **The update routine re-saves what it changed, so the creator does not have to.** The last manual
  step — open every world scene and save it, so the migrated data is written back the way Unity
  writes it — is gone. The routine records the scenes, prefabs and assets it rewrote, by GUID so the
  folder consolidation cannot lose them, and `VirtuademyUpdateResave` re-serializes exactly those,
  with `AssetDatabase.ForceReserializeAssets` and no scene opened. Files the routine did not change
  are not touched. It waits for the moment a save is safe: the next domain reload when scripts or
  assembly definitions were rewritten, the package lock written again when it was deleted, no
  compile errors. Each file is reimported first and re-saved alone, and put back byte for byte when
  its re-serialization logs an error, gains a Visual Scripting `MissingType` or loses graph content;
  those are listed in the Console for a manual check. Scenes open in the editor are closed before
  the rewrite and reopened after the re-save.
- **A catalog no longer depends on a C# type name.** The `RemoteLoadPath` the publish window
  writes is now `{Catalog.BaseUrl}/{Catalog.WorldId}/…`: two runtime
  variables owned by the new `Virtuademy.CatalogVariables`, which the application fills through
  `AddressablesRuntimeProperties.SetPropertyValue` before each catalog loads. The names have dots
  for readability but no class answers to them, on purpose — the previous template named
  `Virtuademy.AddressablesVariables` (and `Reflectis.AddressablesVariables` before the brand
  rename), and every move of that identifier broke every world published before it.

  Both old types stay, `[Obsolete]` and getter-only: Addressables reaches them by reflection
  exactly when a legacy catalog loads, and their getters record the hit in
  `Virtuademy.LegacyCatalogProbe`. The application reads the probe after the load and reports a
  `LegacyAddressablesCatalog` diagnostic naming the world; the load itself is unchanged. Worlds
  published with this version record the new names; worlds published earlier keep loading and
  are listed by the diagnostic until rebuilt. The two compat types are retired once that list is
  empty.
- **Splines sample: the units answer to their former bare names, and `Expose SplineAnimate` exposes
  what it says.** The seven units once lived in the global namespace; a graph saved against that
  spelling records `"$type":"SetSplineAnimateContainerUnit"` and, after the move into
  `Virtuademy.SDK.Environments.VisualScripting.Splines`, deserialized as `MissingType` — logged as an
  error, which fails a player build. Each unit carries `[RenamedFrom]` with its bare name, so those
  graphs come back on load. `ExposeSplineAnimateUnit` also registered its `IsPlaying` port under
  the key `Duration` and never created `Duration` at all: the node showed a "Duration" port that
  returned a bool. Both ports exist now and return what their name says. The sample still assumes
  `com.unity.splines` is installed in the project that imports it; the package does not declare it,
  because the module is optional.
- **The analytics graphs name the analytics types where they are.** `ExperienceVerbManager`,
  `SendMultiAnswerQuizData` and `ControlManager_Informative` still recorded `EExperienceOutcome` and
  `EExperienceScoringType` under `Virtuademy.SDK.ApiData`; the seventeen analytics types left that
  namespace for `Virtuademy.ScriptingApi` on 2026-09-14, so those enum values deserialized as
  strings (`fsReflectedConverter expected Object but got String in "Pass"`). Rewritten, and the
  rename migrator carries the seventeen moves as whole-identifier rules for creator graphs.
- **The client models left for the application; the package speaks in views.**
  `IVirtuademyFramework` hands back `UserView`, `SessionView`, `ExperienceView` and
  `EnvironmentView` from `Virtuademy.ScriptingApi` instead of the `CM*` types, and the nodes that
  expose them follow. The models themselves — with the members an authored world never reads —
  now live beside their siblings in the application, so a creator's project no longer carries
  the platform's notion of a user account.

  `CreateLeaderboardRecord` takes `(string, float)` rather than a client model: the node already
  had both as ports and built the model only to pass it on.

  The rename migrator gained five entries, on the **full type names**. The namespace is
  deliberately absent from that table — it still exists, on the application side — so only the
  five moved names are rewritten. Member names are untouched, which is what lets a rewritten
  graph resolve its members without a second rule. A graph that reached past this surface — a
  user's preferences, a session's permissions, an environment's catalogue — has no view to land
  on and must be re-authored; there is nothing to migrate it to.
- Removes a dead `using NUnit.Framework;` from `SpawnableObjectListReference`, which pulled the
  test framework into a package creators install and was used by nothing in the file.

### Changed
- **The scripting entry point is `VirtuademyEnvironments`, not `World`.** Two things ruled the
  old name out: "Worlds" is leaving the application name — the concept survives on the wire,
  where `WorldDTO` keeps it, but it stops being the word a creator reads on every line — and the
  name carried nothing that said which platform it belonged to.

  Plain `Virtuademy` was the first choice and is ruled out by the compiler, not by taste. Every
  SDK namespace is rooted at `Virtuademy`, so in `Virtuademy.Player` the namespace wins over the
  type and the line fails with CS0234 — in a creator script in the global namespace exactly as
  much as inside the SDK. Reaching it would take
  `global::Virtuademy.Environments.ScriptingApi.Virtuademy.Player`.

  This is a breaking change for authored scripts, taken now because nothing can break: the facade
  is two days old and no script has ever reached publication, because the deployed whitelist still
  named the pre-rename assembly and the deploy path refused every one of them. There is **no
  migrator entry**, deliberately — `World` is far too common a token to rewrite in a creator
  project by substitution (three unrelated `World.` call sites in the application prove it), and
  there is nothing out there to migrate.

  The assembly and the namespace are unchanged, so `policy.json` and the operator overrides need
  no edit: the whitelist is keyed on those, never on a type name.
- **`Virtuademy.SDK.PlatformApi` is `Virtuademy.SDK.ApiData`.** The old name said where the types
  came from — a package that no longer exists under that name — rather than what they are. Three
  analytics graphs shipped in this package name two of those types in their serialized `$type`
  values and were rewritten with them, so they resolve rather than deserialize as `MissingType`.
- The rename migrator gained the two entries that carry a creator project across that rename. The
  assembly entry runs before the namespace entry, because the namespace is a prefix of the
  assembly name and the shorter rule would otherwise consume it.
- **The package is `Virtuademy-SDK-Environments`.** Business ruled out the name "Creator Kit" and this package was the last place it was a wire symbol rather than prose. The id is `com.anotherealitysrl.virtuademy-sdk-environments`, and the three assemblies are `Virtuademy.SDK.Environments`, `Virtuademy.SDK.Environments.Editor` and `Virtuademy.SDK.Environments.HybridCLREditor`.
- Every namespace under the old `Virtuademy.CreatorKit.Worlds` prefix moved to `Virtuademy.SDK.Environments`, by one substitution rule. The `.Core.` segment the merge left behind is gone with it: `...Worlds.Core.ClientModels` is now `...Environments.ClientModels`, and the three namespace pairs the merge had left duplicated (`Placeholders`, the package root, `Editor`) are single namespaces again. That merge was verified collision-free first — no two types of the same name met.
- `Virtuademy.Environments.ScriptingApi` keeps its name. It is the one assembly `policy.json` whitelists by name, that file is deployed, and a published script records it.
- The rename migrator handles both hops in one pass now, and its menu entry is `Package rename migration`. A creator project on the old brand is carried through the brand rename and then this one; a project already on the new brand is caught directly.

### Breaking
- **Every published world must be rebuilt.** A built Addressable bundle records each component by assembly, namespace and class, and all three changed.
- **Every Visual Scripting graph must be migrated**, in a creator's project and in this repo. A graph records each unit as namespace plus type — no assembly — so the namespace change is the one that reaches graphs. Run `Virtuademy Worlds/Creator Kit update routines/Package rename migration`, then `Regenerate Nodes`. **Expect deserialization errors on the first open afterwards, and do not save until they stop**: while a worker cannot resolve a renamed unit yet, Visual Scripting substitutes `MissingType` and that renumbers the JSON `$id`s, which surfaces as "Object definition has not been encountered for object with id=N". Most clear by themselves once the assemblies load — the log then says the missing type "was found" — but some are cached in the asset's imported artifact and survive a reopen: those need a **Reimport** on the asset, then a look at the graph to confirm the units are real nodes, and only then a save — **on every affected asset**, since each caches its own import artifact and the errors surface a few at a time. Saving before that check is the one way to make the loss permanent. Verified on this repo: 55 graph-bearing assets, every `$ref` still preceded by its `$id` after the rename, and no compile error.
- Every `using Virtuademy.CreatorKit.Worlds…` in a creator's own scripts breaks, and so does any asmdef that names one of the three assemblies as a string rather than a GUID. The migrator rewrites both.
- A project that depends on `com.anotherealitysrl.virtuademy-creatorkit-worlds-core` in its `manifest.json` no longer resolves it. Existing registry releases are unaffected: each pins the git repo at a tag, and that tag still carries the old id.
- Five namespaces the package declares are **not** renamed and are not part of this change: `Virtuademy.SDK.Core`, `...Core.ApplicationManagement`, `...Core.ChatBot`, `...Core.Networking` and `...Core.NetworkingSystem`. Those five types moved here from SDK-Core keeping their original namespaces so consumers' `using` directives kept working, and renaming them now would break the very thing they exist to preserve.
- **`ManipulablePlaceholder.GizmosEnabled` is now `GizmosDisabled`**, with the serialized field and the inspector row inverted to match. The default for a fresh placeholder is unchanged; a placeholder authored before this carries the old boolean under the old name, so its value is lost on reimport and reads as "gizmos shown". Re-check the box on any placeholder that had gizmos turned off.

### Added
- **The TMP component under a POI placeholder is the source of truth for its text.** The inspector fields already pushed their value into the TMP on edit; selecting a placeholder now pulls the TMP text back into the serialized field, so the inspector shows what is actually displayed, and the runtime getters read the TMP with the serialized field as fallback. A creator who typed straight into the TMP no longer sees the text reverted.

### Fixed
- **POIs published from a creator project are no longer empty.** A POI finds its pages by a marker
  under its "Pages" container, and the marker was the framework's `GenericHookComponent` (id
  `POIPage`). Once the authoring packages stopped installing the framework, that component was a
  missing script in every creator project, so a world published from one carried no marker, the
  POI found no page, and its panel came up empty. Pages are now marked with the new
  `POIPagePlaceholder`, and `POIBase` uses it. The two other hooks the package placed —
  `PanTransform` on the POI and on the Mirror, `TeleportTarget` on the Mirror — were never read by
  anything and are removed. **The v2026.5 -> v2026.6 update routine gained a step for it**, "POI
  page markers": it swaps the old marker for the new one in every scene and prefab of the project
  (pages duplicated under "Pages", unpacked POIs, copies of the prefab), keeping each component's
  fileID, and removes the other dead hooks when the framework is not installed. Worlds with POIs
  must be republished after the routine runs; the platform also reads the old marker, and falls
  back to the container's direct children, so worlds already published keep working meanwhile.
- **`PanForcer` now fires on mobile.** The graph's platform switch had `OutputTriggerWebGL` and `OutputTriggerVR` wired and `OutputTriggerMobile` dangling, so a forced pan did nothing on a phone or tablet. Mobile joins the WebGL branch.

## v9.0.0

### Changed
- **Five sibling packages were absorbed into this one**, which is now the whole authoring surface: `Virtuademy-CreatorKit-Worlds-Placeholders` (4.1.0), `-VisualScripting` (2.4.0), `-Tasks` (2.2.1), `-Dialogs` (1.2.0) and `-Analytics` (3.2.0). They live under `Runtime/<Name>` and `Editor/<Name>`; their READMEs and changelogs are kept under `Documentation~/legacy-packages/`. Nothing was rewritten — 389 scripts and the assets beside them moved as they were.
- **Twelve assemblies became three.** Everything runtime compiles into `Virtuademy.CreatorKit.Worlds.Core`, everything editor into `Virtuademy.CreatorKit.Worlds.CoreEditor`, and `Virtuademy.CreatorKit.Worlds.CoreHybridCLREditor` stays separate because it carries a `HYBRIDCLR_INSTALLED` define constraint the rest must not inherit. Assembly *names* are unchanged, so no consumer reference had to be rewritten: every asmdef reference in the project is written as a GUID, and a moved asmdef keeps its GUID.
- Core's own placeholder base classes moved from `Runtime/Placeholders` to `Runtime/PlaceholderBases`, which frees the name for the placeholder set that came in and says what they are.
- The Splines, Quiz and task-scene samples are declared here now, so they stay importable from the Package Manager.

### Breaking
- **Every published world must be rebuilt.** A built Addressable bundle records each component by assembly, namespace and class, and five assemblies stopped existing. Source projects are unaffected — script references resolve by GUID from the `.meta`.
- A project that referenced one of the five packages by id in its `manifest.json` no longer resolves it. There is one package to depend on.
- Namespaces did **not** change in this release, so no `using` in a creator's own scripts breaks, and no Visual Scripting graph does either (a graph records its units by namespace and type, never by assembly).

## v8.1.0

### Added
- Added `BlockedByOtherUser` (64) to `IInteractable.EBlockedState`, for collaborative tools that need to signal "another user is currently in here" without borrowing a flag that already belongs to another feature. The quiz used to reuse `BlockedByLockObject` for this, which meant its own recompute erased the lock an operator had set from the contextual menu. Purely additive: every `allowedBlockedStates` mask in the project is a negative "allow all except", so the new bit is permitted by default everywhere and only the options that must react to it need updating.

## v8.0.0

### Added
- Added `SpawnProjectAssetAsync` signature to `IReflectisApplicationManager`.
- Automatic setup and automatic list management for spawnable objects.
- SFTP support, tenant environments and platform selection in the addressables/env upload windows.
- Moved input actions into core.
- Added dependencies on Reflectis SDK ReflectisApi and TenantConfiguration.

### Changed
- Improved addressables management with a dependency on tenant selection.
- Renamed keys for avatar prefabs.

### Fixed
- Fixed spawnable object editor flow and addressables window first setup.
- Fixed session participants and single-world access handling.

## v7.1.1

### Added
- Added function to set the system language

## v7.0.1

### Added

- Fixed compile issue in `BreakingChangeSolver`.

## v7.0.0

### Changed

- Changed `JoinSession` method signature in `JoinAndLoadSession` of `IReflectisApplicationManager`.

### Added

- Added catalog info and isTenant in `CMEnvironment`.
- Added optional isTenant name in GetExperienceByAddressableName method of `IClientModelSystem`.

## v6.0.0

### Changed

- Revised all `CMWorld`, `CMEvent`, `CMEnvironment` entities to support new structure based on Experiences and Sessions.
- Change legacy signatures of `GetWorldFolders`, `GetWorldAssets` and `SearchWorldAssets` signatures in `IClientModelSystem`.

### Added

- Added `TextBox` option to `EPrefabIdentifier` in `IObjectSpawnerSystem`.
- Added `ManageMySessions `entry in `EFacetIdentifier` of `CMPermission`.
- Add some `[CreateProperty]` decorators in `CMUser` and `CMWorld`.

## v5.0.1

### Fixed

- Fixed issue of `AddressablesConfigurationWindow` not creating addressables settings if missing.
- Fixed issue of unused addressables entries not being cleaned up by `AddressablesConfigurationWindow`.

## v5.0.0

### Changed

- Redesigned the addressables management window, now the configuration and the management of the scenes is more user-friendly:
  the user references the scenes directly without dealing with notion of catalogs and builds them all at once.
- Removed null definitions from `CMEvent`.

## v4.2.0

### Added

- Add missing splines namespaces in `CreatorKitUpgradeWindow`

## v4.1.0

### Added

- Added CreatorKitUpgradeWindow to allow migrate scenes to Reflectis Worlds 2025.1.x versions.

### Fixed

- Add `SelectedInteractable` reference in `IVisualScriptingInteractionSystem` interface.
- Added extra checks to prevent a null reference in case a visual scripting interactable has already been removed from scene.

## v4.0.0

### Changed

- Changed package name, from Virtuademy-SDK-CreatorKit to Reflecits-CreatorKit-Core, and updated namespaces according to new package name.

### Added

- Added ClientModels, Help, ObjectSpawner, ApplicationManagement and SceneObjects modules, previously located in SDK.
  ApplicationManagement does not contain all the logic contained in SDK, but only the part that is specific to Virtuademy.

## v3.8.0

### Added

- Added a "UI starter kit" consisting of a spritesheed useful for faster prototyping.

### Fixed

- Removed an unused camera from pan transform in POI placeholder.
- Add open and close callbacks for POI placeholder

## v3.7.2

### Fixed

- Fixed `AddressablesBundleScriptableObject` configure button.

## v3.7.1

### Fixed

- Fixed package version in package.json

## v3.7.0

### Fixed

- Updated visual scripting graph `SendMultiAnswerQuizData` to correctly send the score to the BackOffice for the analytics.
- Improved animations of ChatBot's humanoid templates.

### Added

- Added new visual scripting node `LoadDefaultEvent`, that that loads the default event. Using this node is equivalent to pressing the "home" button on the HUD (or on the VR tablet).

## v3.6.0

### Added

- Added new visual scripting node `GetLocalPlayerID`, that returns the player ID, which is a numeric identifier that is unique for the user in the current shard context.
- Added new `ChatBotPlaceholder` that allows to set up a chatbot with multimodal (audio/text) input and output.

### Fixed

- Updated visual scripting node `OnOtherPlayerEntered` and `OnOtherPlayerLeft` to correct faulty management of networking events and prevent null reference exceptions after leaving and joining events multiple times.

### Deprecated

- Environment thumbnails loading has been deprecated. Now it is necessary to load thumbnails from the backoffice instead of using the addressable system

## v3.5.0

### Added

- Added new Point of Interest (POI) component placeholders. A POI is an interactable UI element that, once selected, display a panel with info like text, images, video, and links.
- Added new visual scripting event nodes `OnOtherPlayerEntered` and `OnOtherPlayerLeft`, that can detect players entering/leaving the Reflectis event where the local player is currently staying. These nodes also expose two identifiers related to the user that just entered/left: `User Id`, which is a unique identifier related to the Reflectis profile, and `Player Id`, that is a numeric identifier that is unique only in the context of the current shard.
- Added new visual scripting node `ChangeScene`. It can be used to move local player to a static Reflectis event by providing the name of the environment used by the target event.
- Added new visual scripting node `SendMultiAnswerQuizData`. It can be used to send quiz results to the server to display its data on the analytics table.
- Added new visual scripting node `SetCurrentShardOpenState`. It can be used to open/close the current shard (i.e. the shard where the local user is). Closing a shard will prevent more players to join it, and as long as it stays closed it will be treated by Reflectis as if it reached max capacity.
- Added new visual scripting node `GetCurrentShardOpenState`. It can be used to get the state of the current shard: true if it's open, false if it's closed.

## v3.4.0

### Added

- Added `DiagnosticGenerateExperienceIDUnit` to generate unique experienceID
- Added `DiagnosticSendDataUnit` to allow creators to send diagnostic data
- Added `SwitchNetworkMasterUnit` to switch visual scripting flow base on if the client is the master client or not
- Added `OnSceneUnloadEventUnit` to launch events on scene change
- Added `GetCMUserByIDNode` to get a CMUser data by using its ID.
- Added `URLImageToTexture` to apply a sprite to an image from a URL link.
- Added `ProfileImageURL` to the exposed data of the already existing `CollectPlayerDataNode` node.
- Added new visual scripting node `InitializePlaceholder`. It can be used to initialize the placeholder component of a GameObject that has been instantiated dynamically.
- Added `ConstraintSource` type to visual scripting type library.
- Added Scriptable Object class to create addressable bundles to auto-setup when needed.

### Fixed

- Updated visual scripting node `EnableOtherCharacters` so that it uses the new method `EnableOtherAvatarsMeshes` from the AvatarSystem. The node now works as intended (up until now it was considered as not implemented).
- Updated synced var change unit view to improve their usage.

## v3.3.1

### Fixed

- Fixed synced variables parameters (`OnSyncedVariableInit`,`OnSyncedVariableEventUnit`,`OnSyncedVariablesEventNodes`), removed unused value input and unuesed isSynced variable

## v3.3.0

### Added

- Added `OnSelectedGenericInteractableChangeNode` that will be triggered whenever the current selected generic interactable changes, and its argument is a reference to the new selected item. The event is also fired when the user clicks on an empty area, in which case the reference is null.
- Added `SetFirstPersonCameraModeNode` and `SetThirdPersonCameraModeNode`. They can be used to switch the character controller to first person or third person view respectively.

## v3.2.0

### Added

- Added `startPaused` variable to `BigScreenPlaceholder` to switch off autoplay on start.
- Added `onVideoPlayed` and `onVideoPaused` callbacks in `BigScreenPlaceholder`.
- Added `OnVideoPlayedEventUnit` and `OnVideoPausedEventUnit` scripts to provide callbacks for video player play/pause events when triggered by visual scripting.

## v3.1.3

### Added

- Added localization logic to write and translate custom keys.

### Fixed

- Fixed the configuration of the scale of the environmental dashboard (now support also the collider the dashboard itself).

## v3.1.2

### Fixed

- Added cache for placeholder types in `SceneComponentsMapper`, to improve loading times and avoid operation involving reflection when possible.

## v3.1.1

### Fixed

- Fixed missing validity check of build and load paths in the top-level Addressables settings, which may result in catalogs being built in the wrong folder.

## v3.1.0

### Added

- Added `OnSceneLoadEventNode` that will be triggered during scene loading before placholders' mapping. If marked as coroutine will wait the flows to finish while in fade in.
- Added `OnSceneSetupEventNode` that will be triggered during scene setup. If marked as coroutine will wait the flows to finish while in fade in.
- Added `OnSceneSetupCompletedEventNode` that will be triggered after the setup fadeout is completed.
- Added scale on `InteractablePlaceholder` in manipulable mode (UX feelds since there is no bounding box).
- Added color picker and non proportional scale to `InteractablePaceholder` in contextual menu mode (non proportialScale needs manipulation mode to work properly).
- Added `OnSyncedVariableInit` node that will be triggered when entering the scene. It outputs the value of the synced variable, whether or not it has changed and an event.

## Fixed

- Fixed an issue of the Addressables configuration window not updating properly the load and build paths of the Addressables groups.
- Fixed an issue of the Addressables configuration window not updating the value of a profile variable if such variable was defined but empty.
- Fixed issue of `automaticSetup` and `isNetworking` fields of `InteractablePlaceholder` not being shown in the inspector.
- Fixed the synced position of the synced object when player enter the environment.
- Improved `InteractablePlaceholder`'s custom inspector.
- Disabled CRC check option in Addressables groups.

## v3.0.0

### Changed

- An `InteractablePlaceholder` now accepts viual scripting state machines instead of scriptable actions to handle the generic interaction.
- Improved name conventions of public variables and properties of existing visual scripting nodes.

### Added

- Added `ScreenName` field to `BigScreenPlaceholder` to identify big screens by a custom name instead of its `GameObjsect`'s name.
- Added new visual scripting nodes: interactable behaviours blocking by selection, pan character camera, disable character mesh, teleport player, open-close tutorial.
- Added visual scripting nodes which collect data from current event, current environment and current user.
- Added an `ActionPlayerMap` defining XR inputs (useful for recognizing user input by visual scripting).
- Added visual scripting nodes which detect manipulation start/end of a `Manipulable` object.
- Implemented a utility to auto-setup visual scripting nodes.

### Removed

- Removed awaitable scriptable actions in favour of visual scripting machines.
- Removed legacy `ToastInteractablePlaceholder`.

### Fixed

- Fixed `NetworkPlaceholdersManager` editor window scrolling.
- Fixed `SyncedObject`'s custom inspector not showing the info of the base class.
- Improved `InteractablePlaceholder`'s custom inspector.

## v2.0.0

### Changed

- Massive SPACS -> Reflectis refactor.
- Reorganized package structure.
- Reimplemented `BigScreen` placeholder (with prefab) for an easier configuration and improved flexibility.
- Reimplemented `TeleportPoint` logic (now called `SceneChanger`).
- Reimplemented `ScriptableActions` to support async/await paradigm, and to support the selected interactable as a parameter of the action.

### Added

- `INetworkedPlaceholder` interface to distinguish network placeholder from local one.
- Added an environmental dashboard prefab with placeholder.
- `InteractionPlaceholder` which supports the interaction provided by the `SDK.Interaction` module.
- Add first integration with visual scripting, with nodes for ownership management, variables syncronization and invoking of networked events.
- WebView placeholders (both for screen-space and 3D WebViews).
- Added window to configure network placeholder IDs.
- `SpawnAddressable` placeholder for spawning addressable assets.
- `InteractableOwnershipPlaceholder` for ownership management of an interactable object.
- `HelpSpawnObjectPlaceholder` which allows for spawning an object on help system calls.
- `GoToPreviousEventButtonAdder` which allows to add a button to the menu that teleports the player to the previous event.
- `GoToPreviousEventOnCollisionPlaceholder` which allows to create a teleport point to the previous event.
- `MascotteNameSetCheckPlaceholder` which allows to check if the mascotte name has been set at scene start. If not, it calls the help at start.
- `RPMAvatarWebViewButtonPlaceholder` which allows to open the `ReadyPlayerMeWebView`.

### Deprecated

- Environmental player List prefab
- Environmental tutorial prefab

### Removed

- Legacy `BigScreen` prefab and placeholder

## v1.0.0

Initial release
