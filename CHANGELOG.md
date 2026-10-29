# Release notes

## Unreleased

The review fixes below warrant a minor bump: fixes, two values appended to `EnvironmentsBuildFailure`, the six per-module define-symbol
classes kept as `[Obsolete]` shells. No public member removed or renamed.

#### Added
- **Third-person hold preview** (2026.7, minor): the SpawnableObject inspector's "Preview third-person hold" button
  shows a mannequin (Unity primitives, not saved) holding the item as desktop, web and mobile hold it in third person,
  from its hold offset and right-hand pivot; "Hide preview" removes it. The inspector moved to the editor assembly
  (`SpawnableObjectPlaceholderEditor`), with its hand-reference buttons as before.
- **`SpawnableObjectPlaceholder.customThirdPersonHold` / `thirdPersonHoldOffset`** (2026.7, minor): on desktop, web
  and mobile, in third person, an item held with the hand at its own offset from the avatar's chest instead of the
  platform's default. Off by default.
- **`PickablePlaceholder.dragMode`** (2026.7, minor): `Model` (default) drags the 3D object as a manipulable; `Icon`
  drags the item as its icon on desktop, web and mobile, back where it was on release unless used on a target or
  dropped on the inventory. Until now every pickable was dragged as its icon on desktop: an environment that wants that
  sets `Icon` on its pickables.
- **`IToolsApi.ToolUseStarted` / `ToolUseEnded`, `ToolUse`** (2026.7, minor): every player's tool uses, this player's
  (`IsLocal`) and, in a multiplayer session, the others', with the tool's name, the player and an `Aim` transform
  (where the use starts, its forward; for another player at their avatar's right hand). An effect every player sees
  (a spray's jet) starts under `Aim` and stops on `ToolUseEnded`; what the use does to the world is done only for
  `IsLocal` uses.
- **`InventoryPlaceholder.returnItemsToOriginOnLeave`** (2026.7, minor): in a multiplayer session the items a player
  took from the scene for themselves go back into the room when they leave, near where they left (default) or where
  each was at the start (on).
- **Task panel placement per platform** (2026.7, minor): `TaskUIPlaceholder.desktopPlacement`, `mobilePlacement` and
  `vrPlacement` (`ETaskPanelPlacement`: `InWorld`, `WithPlayer`) choose for each platform whether the panel stands in the
  world, where the placeholder is, or goes with the player (on the screen on desktop and mobile; in VR low on their
  left, moving and turning with their body). Defaults keep today's behaviour: desktop and mobile with the player, VR in
  the world. In the world, `worldPanelSize` (metres, 0.61 x 0.69 by default) times the placeholder's scale sets the
  panel's size; selecting the placeholder draws its outline in the Scene view.
- **The inventory's item slots round up so the grid has no holes** (2026.7, minor): on the first page to whole columns
  once a second column starts (whole rows in VR), past it to whole pages, with empty, usable slots;
  `InventoryPlaceholder.inventorySlotsCount` follows (with `SlotCountsChanged`), as it does when an item that does not
  fit adds a slot. The equipment's slots are declared, so its columns, rows and pages show empty, inert cells instead.
- **`IToolsApi.HeldToolModel`** (2026.7, minor): the held tool's 3D model in the avatar's hand, on desktop and mobile in
  third person, where an effect of the tool (a spray's jet) starts; null in first person, when the tool shows no model,
  and in VR.
- **The build refuses an environment whose scripts compile into `Assembly-CSharp-firstpass`.** A component binds in
  the app only if the app has a class with the same assembly, namespace and name, and the app has no
  `Assembly-CSharp-firstpass` (nor `Assembly-UnityScript-firstpass` or `Assembly-CSharp-Editor-firstpass`): scripts
  under a creator's `Assets/Plugins` or `Assets/Standard Assets` compile into it, so each of their components
  arrived in the app as a missing script (seen with the SALSA add-ons `Amplitude` and `AmplitudeSALSA`, which the app
  holds in `Assembly-CSharp`). `EnvironmentsBuild.BuildAndZip`, behind **Build Addressables** and **Build & Deploy**
  in the window and `EnvironmentsCli`'s `build` and `deploy -vdBuild`, now reads the included scenes' dependencies
  before it builds, and stops with a dialog and a Console error that name every such component (scene or prefab,
  GameObject path, type, script) and say how to fix it: move the plugin's runtime scripts, with their `.meta` files,
  out of those folders to a normal one (the vendor's own folder, for example), which compiles into `Assembly-CSharp`.
  In the command line it is `ExitBuildFailed` with `build.failure` `FirstpassScripts`, and the components in
  `build.firstpassComponents`. Warrants a minor bump.
- **`InventoryPlaceholder`** (`ScriptingApi/Placeholders/Pickable/`), one placeholder for the tools radial menu and the
  inventory of pickable items, with the fields of `ToolInventoryPlaceholder` and `InventorySlotPlaceholder` (same names)
  and a selector, `InventoryMode`: `None`, `Tools` or `Inventory`. At scene start the app initializes and shows only
  the selected system; setting `InventoryMode` at runtime (or editing it in the inspector in Play mode) raises
  `InventoryModeChanged(previous, current)`, on which the app hides the current system and initializes (the first
  time) and shows the new one. In VR one controller button will open either UI, hence one system at a time. The
  Inventory section also carries the VR panel's settings: `inventoryAction` (the input that opens and closes it) and
  `inventoryZOffset`, `inventoryYOffset`, `inventoryOpenSpeed` (its placement in front of the camera and its opening
  time), the counterparts of the tools' `action`, `zOffset`, `yOffset` and `openSpeed`. The two
  old placeholders stay and keep working. Unlike `InventorySlotPlaceholder` it has no `IsNetworked`: the inventory is
  per user, and the app's network bridge for it only called the same initialization. Warrants a minor bump.
  It also carries `pickedToolsShared` (as on `ToolInventoryPlaceholder`) and `startingItems` (as on
  `InventorySlotPlaceholder`). Without an `inventoryAction`, B/Y (the secondary button of either hand) opens the VR
  panel.

- **The inventory changes during the experience**, from scripts and Visual Scripting. `InventoryPlaceholder.SetSlotCounts(wearSlots,
  inventorySlots)` gives it more slots (counts only grow; it raises `SlotCountsChanged`), beside the `InventoryMode`
  setter. Two nodes: **Virtuademy Inventory: Set Mode** (`SetInventoryModeNode`) and **Virtuademy Inventory: Set Slot
  Counts** (`SetInventorySlotCountsNode`). Additive; warrants a minor bump.

- **`InventoryItemTriggerDetectorPlaceholder.activation`** (`EActivation`: `Contact`, the default and the behaviour
  so far, or `Use`). In VR with the `InventoryPlaceholder`, the tools are used with the trigger: a `Contact` target
  reacts as soon as the right tool touches it (its events and the right/wrong feedback, no use spent), a `Use` target
  reacts when the tool touching it is used (the trigger; a Hold tool keeps acting while the trigger is held). Desktop
  and mobile: the click with the tool, as before, for both. Additive; warrants a minor bump.
- **Equipment slots declared by the environment.** `InventoryPlaceholder.equipmentSlots`: a list of
  `EquipmentSlotDefinition` (type `Hand` or `Custom`, the custom slot's name as the wearables give it in `wearSlot`, an
  optional icon shown on the slot in place of its name; the hand slot always shows the application's hand icon), shown from the start. A wearable whose slot is not declared adds it; with an empty list the wear
  slots are counted by `wearSlotsCount`. The counts of this placeholder are where the inventory starts: an item that
  does not fit adds a slot, so nothing is refused (the old placeholders keep their behaviour). At runtime `AddEquipmentSlot(type, name, icon)` (raises
  `EquipmentSlotsChanged`) and the node **Virtuademy Inventory: Add Equipment Slot** (`AddEquipmentSlotNode`).
  Additive; warrants a minor bump.
- **An inventory slot shows the quantity of copies** (from 2 up; one copy shows no number), never the uses. Picked
  copies stack in one slot, as before: `numberOfUses` stays a per-copy value for creators (a use spends the top copy's; a copy taken out keeps its
  own), and "∞" is no longer shown. Behaviour change for items with more than one use: two picked copies of a
  2-use item showed "4", they show "2". Warrants a minor bump.

- **The Virtuademy theme** (`Common/Theme/`, `Runtime/UIKit/Theme/`). `VirtuademyTheme.asset` holds the colour
  roles (`EThemeRole`: surface, raised surface, border, text, secondary text, action, text on action, success,
  warning, error, info, scrim), the type scale, the three Poppins weights, radii and spacing. The action colour is
  the Virtuademy blue `#006EF5`. `VirtuademyTokens.uss` carries them as `--vd-*` variables (generated by
  `Virtuademy/UI/Generate theme tokens`), `VirtuademyComponents.uss` builds the panel classes on them (`vd-panel`,
  `vd-button--primary`, `vd-toast`, `vd-list`, `vd-check`…, and `vd-bg-*`/`vd-text-*`/`vd-border-*` per role),
  `Virtuademy.tss` is the theme of the SDK's two new PanelSettings (`VirtuademyScreenPanelSettings`, sorting order
  -1, so environment overlays stay under the application's UI; `VirtuademyWorldPanelSettings`, one pixel per
  millimetre). `ThemeColorRole` colours a uGUI graphic by role. Warrants a minor bump.
- **Tenant colours at runtime.** `VirtuademyThemeRuntime.Publish` takes the palette the application publishes;
  `ThemedDocument` (on every new SDK panel) and `ThemeColorRole` follow it, and roles left out keep the SDK value.
- **UI Toolkit panels for environments** (`Common/Prefabs/UI/Toolkit/`), scene prefabs with the same functions as the
  uGUI ones and callbacks for each: `VirtuademyPanel` and its `_Hint`, `_Warning`, `_Error`, `_Success`, `_Welcome`,
  `_Confirm` variants (successors of the description, warning, wrong-answer, welcome and confirm canvases),
  `VirtuademyListPanel` (list item canvas, checklist), `VirtuademyCheckMark` (check completed, and a cross),
  `VirtuademyLabel` (hover description), `VirtuademyChoicePanel`, `VirtuademyHud` (counter, progress, hint: the
  animated screen canvas; over the screen on desktop and mobile, a panel that follows the head in VR through
  `OverlayPanelPlacement`) and `NotificationHost` (toasts and popup). Texts take a localization key and a
  fallback. Warrants a minor bump.
- **Toasts and popups for scripts and graphs.** `IVirtuademyGameplay.UI` (`IUIApi`): `ShowToast(text, severity,
  duration)`, `ShowPopup(title, text, buttons, onClosed)` reporting the button pressed, `ClosePopup`,
  `IsPopupOpen`, `GetThemeColor(role)` and `ThemeChanged`; nodes `Virtuademy UI: Show Toast`, `Show Popup`
  (continues on Shown and on Closed with the button index), `Close Popup`, `Get Theme Color`. Drawn by the scene's
  `NotificationHost` when it has one, by the application's otherwise. Adding a member to `IVirtuademyGameplay`
  breaks any other implementation of it (the application's is the only one). Warrants a minor bump.
  Popups and toasts fade in and out over the theme's `--vd-motion-fade` (0.2 s). A popup can close on a click or
  tap outside it, reporting -1: `NotificationHost.closePopupOnOutsideClick` (off by default), the `ShowPopup`
  overload with `closeOnOutsideClick`, and the node's Close On Outside Click input. The HUD's hint button shows its
  `Hint On Request` text (a second press hides it); with that text empty it only raises On Hint Requested.
  `SafeFrame.area`: `Content` (centred column on mobile) or `Edges` (to the side edges, clear of the bars and the
  corner controls); the HUD uses Edges and sits at the top right of the frame.
  Popups close by themselves without a timer of the creator's: `ShowPopup(..., closeOnOutsideClick, autoCloseSeconds)`
  returns the popup's id, its timer belongs to that popup (it can never close a later one, which a delay followed by
  `ClosePopup()` did), and `ClosePopup(int id)` closes only that popup; `NotificationHost.popupAutoCloseSeconds` is
  the default (0: never). Nodes: Show Popup has Auto Close Seconds and a Popup Id output, Close Popup a Popup Id
  input (0: whichever is open, as before).
  In VR, `OverlayPanelPlacement` sizes the document's root to its VR size itself (the popup laid its text out one
  letter per line and the HUD piled up its rows: the root had no size) and has a sideways offset: the HUD floats
  0.9 m to the right and 0.55 m lower, the popups in front and centred. In VR an overlay's colliders are on only while
  it has something to press (`OverlayPanelPlacement.BlocksRay`: the notification host while a popup is open, the HUD
  while its hint button shows): the controller ray hits triggers on the UI layer, so the host's collider, 1.4 x 0.9 m
  in front of the user, caught the ray meant for the application's tutorial panel behind it.
- **`IHelpApi.IsOpen` and `IHelpApi.Opened`**, next to `Closed`, and the nodes `Virtuademy Tutorial: Is Tutorial Open`
  and `On Tutorial Opened`. They follow the application's help (tutorial) state, which it opens by itself on a first
  visit and which keeps the user from moving. In VR the HUD and the notification host hide, and let the ray through,
  while it is open (`OverlayPanelPlacement.vrHideWhileHelpOpen`, on by default). The application's implementation
  watches the state instead of the system's events, which it clears at every scene setup: `Closed` no longer goes
  silent after the first environment.
- **`IVirtuademyGameplay.Tablet` (`ITabletApi`)**: `IsAvailable`, `IsOpen`, `Opened`, `Closed` for the application's tablet
  (desktop and VR; none on mobile), read-only, and the nodes `Virtuademy Tablet: Is Tablet Open`, `On Tablet Opened`
  and `On Tablet Closed`. The SDK's overlays do not react to it. Adding a member to `IVirtuademyGameplay` breaks any
  other implementation of it (the application's is the only one).
- **`ILocalizationApi.TranslateOr(key, fallback)`** and the node `Virtuademy Localization: Translate Or`: the
  fallback instead of the raw key when the Localization File has no translation.
- **`SafeFrame`** keeps a screen panel clear of the application's reserved zones (`ReservedScreenZones`: top bar,
  bottom bar and task HUD on desktop and WebGL, top bar and joystick on mobile, measured from the application's
  code) and of the device's safe area.
- **`Virtuademy/Localization/Check missing translations`** lists the keys the open scenes use that the
  Localization File does not translate, per language.
- **Poppins SemiBold** (`Common/Fonts/Poppins-SemiBold.ttf`), and `Virtuademy/UI/Create Poppins font fallbacks`,
  which creates dynamic TextMesh Pro assets for the three weights and sets them as fallback of the two static
  211-glyph ones. The editor imports the TextMesh Pro Essential Resources once when a project lacks them, so the
  SDK's texts no longer render magenta in a new creator project.

#### Changed
- `InventoryPlaceholder.SetSlotCounts` documents what its wear count is: the wear inventory's generic cells, counted only
  when no equipment slots are declared; with declared slots, `AddEquipmentSlot` adds a slot.
- **The package depends on AI Navigation 2.0.14, Animation Rigging 1.4.1 and Splines 2.9.0**, the versions the
  Virtuademy app ships. A creator project now gets them with the SDK, at the app's versions, so the NavMesh, rig
  and spline components of an environment carry the fields the app reads. A project that lists one of them at
  another version in its own manifest keeps that version: a direct entry wins over a dependency. Animation
  Rigging brings Burst with it. Warrants a minor bump.

- **The SDK's UI Toolkit stylesheets read the theme.** `WorldSpaceButtonPanel.uss`, `ButtonChoicePanel.uss`,
  `Toggle.uss`, `TogglePanelWithVideo.uss` and the two 360 button sheets take their blue, the panel surface and
  the text on buttons from `var(--vd-*)`, with the theme value as fallback, and `ButtonTheme.tss` imports the
  tokens. The blue becomes the Virtuademy `#006EF5` (was `#387AFF` / `#3A7FFF`), and the hover and pressed colours
  of the video toggle button no longer darken (hover scales, as on the other buttons). Environments rebuilt with
  this version show it; published ones are unchanged.
- **`ILocalizationApi` documents what the application does.** `LanguageChanged` passes the language in force before
  the change (it said: the new one), `PreviousLanguage` is the language at world ready until a change (it said:
  empty), `AvailableLanguages` is every language of the localization database and `SetLanguage` takes a language
  name and does not check the tenant's set. Comments only: the behaviour is unchanged.

#### Fixed
- **Build: a failed build no longer zips and deploys a previous run's output.** `EnvironmentsBuild.BuildAndZip`
  checks the build-target switch and the Addressables build result, empties each scene's platform folders and
  old zip before building, and restores the Addressables settings and the original active build target in
  `finally`. A failure is reported as `EnvironmentsBuildFailure.BuildFailed` with a message.
- **Build: scene names are validated first.** A scene whose filtered name is empty (a name with no a-z or 0-9,
  such as a non-Latin one) or shared with another selected scene aborts the build with
  `EnvironmentsBuildFailure.InvalidSceneNames` before anything is touched. The empty name made the stale-folder
  cleanup act on `ServerData` itself and delete other scenes' builds. `SceneNameFiltered` lower-cases with the
  invariant culture.
- **Build: a project whose Addressables settings are not registered as the default builds again.** Addressables builds
  the settings `EditorBuildSettings` names, and the SDK loads them by path: with no registration every scene failed
  with "Addressable Asset Settings does not exist" (silently until the result was checked). `BuildAddressables`
  registers the settings it builds, and returns false when the project has none.
- `AddressablesBuildScript.BuildAddressablesForAllPlatforms` stops at the first platform that cannot be switched to or
  fails to build, and switches back to the original build target; it ignored both results.
- Deploy: the window's build and deploy buttons are re-enabled when an operation throws. The zip upload streams
  from disk with a 30-minute timeout instead of reading the whole file into memory under a 100 s one.
- **The update-routine save guard no longer reads and regex-scans every saved scene and prefab forever.** A
  literal prefilter skips files that cannot match, results are cached by path, time and size, and the guard
  retires once the v2026.5 -> v2026.6 routine has completed: the routine writes
  `ProjectSettings/VirtuademyUpdateRoutine-v2026.6.txt` when no file under `Assets/` still carries old graph
  data, and a later scan that finds some deletes it again. A run that finds nothing to change (a project migrated
  before) completes the routine too, from the command line or by opening the window.
- Script define symbols are set by one `[InitializeOnLoad]` class, `EnvironmentsScriptDefineSymbols`, which
  writes a build-target group only when its symbols differ. The six classes that each rewrote every group on
  every domain reload are `[Obsolete]` constant holders now. The symbols set are unchanged.
- `SpawnableObjectPlaceholder`: no error-level trace on every validate, one debounced `SaveAssets` and only when
  the spawn list changed, inspector edits recorded with Undo and guarded against missing references. The file
  is UTF-8.
- `TaskVirtuademyStepSetter`: an unknown task id from the room no longer throws, and the join-room listener is
  removed when the setter is destroyed.
- The "On Owner Changed" event unit removes its graph instances from its static registries when they are
  uninstantiated. "On Language Changed" subscribes even when the localization system is not up yet; it never
  fired when the graph started first.

- `Default_Canvas`, `CheckCompleted`, `Hover_DescriptionCanvas` and `AnimatedScreenCanvas` no longer serialize the
  XR Interaction Toolkit's `TrackedDeviceGraphicRaycaster`, which the package does not depend on: it was a missing
  script in creator projects. In VR the application adds it at scene setup to every world-space root canvas of the
  environment (`VirtuademyEventManager.SetupEnvironmentCanvasesForXR`), as it already did for the canvases that
  lacked it.
- `AnimatedScreenCanvas`: its 36 objects on layer 0 are on layer 5 (UI), so a click on the HUD no longer reaches the
  object behind it (the application's interaction gate ignores a 3D click only over UI on layer 5).

## v10.0.0

### Added to this release on 2026-10-14 (tag moved)

The `v10.0.0` tag was moved on 2026-10-14 onto the commit that adds the changes below. They are editor-only
(command lines, editor windows); nothing a published world or the app loads changed. A project resolved before
keeps the earlier commit until it re-resolves: the setup's editor-start update or `SetupCli -vdSetupSteps
resolve` does it.

**Requires Virtuademy-SDK-Core v0.3.0 as moved on 2026-10-14** (the commit that adds
`EditorSessionManager.InteractiveLoginAllowed` and `AppConfigurationImporter`): `EnvironmentsCli` refers to
both directly, so this package does not compile against the earlier v0.3.0 commit (989e0aa). The setup's
editor-start update and `SetupCli resolve` move the packages together.

#### Added
- **`EnvironmentsCli`: build, preview and deploy an environment without the Addressables window**. A batch entry point, `-executeMethod Virtuademy.SDK.Environments.Editor.EnvironmentsCli.Run
  -vdCommand <command> [options]`, in the style of the setup package's `SetupCli`: `-vd...` arguments,
  one `[EnvironmentsCli] REPORT {json}` line at the end of every run, documented exit codes, and it exits the
  editor itself (no `-quit`: the work is asynchronous, so the method keeps the editor alive until it is done,
  with an overall timeout, `-vdTimeoutMinutes`, 60 by default).
  - **Commands.** `status` (session, Addressables configured, scene list, interpreter; no network),
    `session [-vdRenew]`, `tenant-config -vdConfigFile <path> [-vdEnv <key>] [-vdConfigFolder <folder>] [-vdAllowTracked]` (see the
    SDK-Core entry: the file lands in `Assets/Editor/TenantConfiguration` unless `-vdConfigFolder` names another folder
    under `Assets/`, `-vdEnv` picks its environment, the first one by default, and the command refuses with 23 when git does not ignore where it
    would sit, unless `-vdAllowTracked`), `worlds` (the worlds the
    login can deploy to, and where each included scene is already published), `build [-vdScenes a,b]` (what
    **Build Addressables** does, interpreted scripts included), `preview (-vdWorlds <ids> | -vdTenant)
    [-vdExpectTenant <label>]` (what a deploy would add and update, per scene) and `deploy (-vdWorlds <ids> |
    -vdTenant) -vdExpectTenant <label> [-vdBuild] [-vdDryRun] [-vdScenes a,b]` (what **Deploy** / **Build &
    Deploy** and the tenant buttons do). The target of a deploy is always explicit, and an id the login cannot
    deploy to stops the command before anything is built or sent. `-vdDryRun` does everything but the uploads
    and imports. At tenant level the scripts are not uploaded, and the command says so.
  - **A deploy names its tenant, and the platform it will call.** `deploy` requires `-vdExpectTenant <label>`,
    the tenant label the session shows (`session` prints it), compared case-insensitively with the logged-in
    tenant's: missing or different, it exits with 24 before anything is built or sent (`preview` checks it when
    given). Every command that calls the platform (`worlds`, `preview`, `deploy`) also checks that the
    Application API it will call, which the editor takes from the project's generated `PlatformEndpoints` asset
    before it looks at the login, is on the same host as the one the logged-in tenant's configuration names, and
    exits with 25 when it is not (the token would go to another platform). The REPORT of those three carries a
    `target` object: tenant label, environment and both hosts.
  - **A timeout never cuts a deploy in half.** When `-vdTimeoutMinutes` passes during a deploy, the scene step in
    flight (its upload and import) finishes, no further world, assembly, scene or link step starts, and the run
    exits with 21 and a REPORT whose `deploy` says, per world and scene, what was uploaded, started and linked and
    what was not; ten minutes after the deadline a hard ceiling ends the run anyway, with the same partial report.
    `deploy.importsStarted` (renamed from `published`) counts the imports the platform accepted: it answers
    "started", and the import runs on the platform afterwards, so it is not a count of live environments.
  - **Arguments are strict.** Any `-vd...` argument (prefix `-`, `--` or `/`, any case) that is not exactly one of
    the command's options is refused with 10, as is an option given twice, a bad `-vdTimeoutMinutes` and a stray
    value (`-vdWorlds 12 13`, which used to deploy to 12 alone). Unity's own arguments are not touched.
  - **Exit codes.** 0 done, 1 unexpected, 10 bad arguments, 11 login required, 12 Addressables not configured,
    13 nothing to build, 14 build failed, 15 interpreted scripts refused, 16 world not deployable, 17 deploy
    finished with problems, 18 not a tenant manager, 19 configuration file not imported, 20 platform
    unreachable, 21 timeout, 22 zips missing, 23 configuration file would not be ignored by git, 24 tenant not
    confirmed (`-vdExpectTenant` missing or different), 25 API host does not match the logged-in tenant.
  - **Batch mode never starts an interactive login.** A command that calls the platform renews an expired
    session silently and, when that is not possible, exits with 11 and says to log in from
    **Virtuademy > Addressables management**. `build` needs a session only for a project with interpreted
    scripts (the server's verification is the authoritative one, so without a session such a build is refused,
    as in the window, unless the scripts are unchanged since they were last published); a project without
    interpreted scripts builds with no session at all.
  - **The editor is kept from reloading its scripts for the whole run.** A build switches the active build
    target, which queues a recompilation and, a few seconds later, a domain reload that ends any awaiting code:
    measured in batch mode, a deploy that followed a build never resumed. The command locks the reload
    (`EditorApplication.LockReloadAssemblies`) and exits without unlocking.
  - **Window and command line share the code, so they cannot drift.** The orchestration moved out of
    `AddressablesManagementWindow` into public static classes: `EnvironmentsBuild` (verify the interpreted
    scripts, build and zip, `BuildAndZip` returns what was built where), `EnvironmentsDeploy` (the world list
    and its role filter, the deploy preview, the deploy to worlds and to the tenant with a per-world and
    per-scene report, the API calls) and `SceneListScriptableObject.Find()` / `LoadOrCreate()`. The window keeps
    its dialogs, its deploy-error panel and its progress bar: it passes them in as a `DeployObserver`.
- **`AddressablesConfiguration`: apply and check the SDK's Addressables configuration without the
  window**. The public static class
  `Virtuademy.SDK.Environments.Editor.AddressablesConfiguration` owns what the "Fix Addressables
  configurations" button did in `AddressablesManagementWindow`, moved there rather than copied:
  `IsConfigured` (the settings, the active profile and the groups all match) and `Apply()` (does
  nothing when configured; otherwise sets the top-level settings, the profile and the default group,
  creating the Addressables settings if the project has none, and throws if the project does not end
  configured). `RemoteBuildPath` and `RemoteLoadPath` give the values the profile must hold.
  - **`Apply()` resets the groups**, as the button did: every group but the default one and every
    entry of the default group are removed.
  - **Why.** A project set up entirely from the command line
    (`Virtuademy-SDK-Environments-Setup` `SetupCli`) kept Unity's default profile and groups, because
    the configuration lived in three private methods of the window and could only be reached by
    reflection on a window nobody had opened. `SetupCli` now has an `addressables` step that calls
    `Apply()`.
  - The window keeps its bindings and delegates to the class; nothing changes for someone using it.
- **`UpdateRoutinesCli`: run the update routines from the command line**. A batch entry point,
  `-executeMethod Virtuademy.BreakingChangeSolvers.UpdateRoutinesCli.Run -vdRoutine <routine> [options]`, in the
  style of `EnvironmentsCli` and the setup package's `SetupCli`: `-vd...` arguments, unknown options refused, one
  `[UpdateRoutinesCli] REPORT {json}` line per launch, documented exit codes (arguments are strict like
  `EnvironmentsCli`'s: `--vdDryRun`, `-VdDryRun`, a duplicate or a stray value is exit 10), and it exits the editor itself (no
  `-quit`). It compiles with the migrators into the update routines' own editor-only assembly,
  `Virtuademy.SDK.Environments.UpdateRoutines` (see Fixed below), and reaches the v2026.4.x -> v2026.5.0 routine,
  which is in `Virtuademy.SDK.Environments.Editor`, by reflection.
  - **Routines.** `list` (the routines the project has, their menu names, and whether each finds something to
    change: a dry scan), `2026.6` (**v2026.5 -> v2026.6**: the text rewrite, the POI page markers, the GUID
    collisions and the legacy folders, with the window's defaults) and `2026.5` (**v2026.4.x -> v2026.5.0**: the
    Mobile output of Platform: Switch nodes, in the nodes where it is not connected). `-vdDryRun` scans and reports and
    changes nothing; `-vdExclude a,b` unticks files (asset paths, or a folder with a trailing `/`); `-vdSkip` turns off
    `unpin`, `pages`, `guids` or `folders` of `2026.6`. The report names the files changed per category, the ones the
    re-save left for a manual check, and what is left. Running a routine again finds nothing.
  - **Exit codes.** 0 done, 1 unexpected, 2 launch again, 3 changes pending (a dry run found something), 10 bad
    arguments, 11 routine not loaded, 12 compile errors, 13 finished with problems, 14 timeout, 15 run `2026.6`
    first.
  - **One launch is one round.** `2026.6` rewrites scripts and the manifest, so Unity has to recompile before the
    changed files can be re-saved: the launch that changes the project exits with 2, and the next one waits for the
    re-save (`VirtuademyUpdateResave`), reports it and finds nothing more to change. The caller launches again, with
    the same arguments, while the code is 2.
  - **Nothing is saved before the routine.** `2026.6` only rewrites text; the re-save goes through
    `VirtuademyUpdateSaveGuard` like any save. `2026.5` saves the graphs it changes, so it refuses to start (15) while
    any scene, prefab or asset still has Visual Scripting data that `2026.6` renames.
  - **Window and command line share the code.** The scan and the apply of `2026.6` moved out of
    `VirtuademyRenameMigrator` into `VirtuademyRenameRoutine` (`ScanProject`, `Apply`, `Options`, `Result`), those of
    `2026.5` out of `CheckPlatformUnitMobilePortUpdater` into static methods taking a `Context` (the progress bar and
    the "save modified scenes?" prompt are what the window passes in), exposed to the command line by
    `CheckPlatformUnitMobilePortRoutine`. The windows behave as before. `VirtuademyUpdateResave.LastRun` gives the
    outcome of the last re-save, `VirtuademyFolderMigrator.Result.Refusals` says what was left in place and why, and
    the node library rebuild reports whether it happened (it does not in a batch project whose unit options were
    never built: run **Update VS Library**).

#### Changed
- **The window's public deploy helpers moved to `EnvironmentsDeploy`** (`GetUploadLinkForWorld`,
  `ImportSceneToWorld`, `ImportEnvironmentDll`, `LinkEnvironmentDll`, `ImportSceneToTenant`,
  `GetTenantUploadLink`, `UploadZip`, `UploadZipViaFtp`, `FtpUploadConfig`), and `PublishableWorld` became
  public with them. Nothing in the SDK called them from outside the window. `UploadZip` and `UploadZipViaFtp`
  are static now. The window keeps the old members as `[Obsolete]` forwarders, so code written against it
  still compiles.
- **"Build & Deploy" and "Build & Deploy to Tenant" refuse to start when the Addressables are not
  configured** (behaviour change). They stop before the
  interpreted DLL step, log an error in the deploy panel and the Console naming the "Fix
  Addressables configurations" button, show a dialog and re-enable the buttons. Until now only
  "Build Addressables" turned into that button; the two deploy buttons built every platform for
  minutes and failed at the end, in `CheckBuildResult`, with "Subfolder StandaloneWindows64 not
  found in ServerData\...". The build behind "Build Addressables" checks too. None of them applies
  the fix itself: it deletes groups and entries, which the creator has to agree to.

#### Fixed
- **The window's "Build & Deploy" and "Build & Deploy to Tenant" lock assembly reloads while they run.** The build
  switches the active build target, which queues a recompilation and a domain reload, and the flow then awaits the
  network: a reload in the middle of it ended the awaiting code silently, with a half-sent deploy. The two flows
  call `EditorApplication.LockReloadAssemblies()` and always unlock in a `finally` (early returns and exceptions
  included), so the reload waits for the end of the flow.
- **Addressables management window layout.** The login status at the top wraps instead of running
  out of the window and squeezing the Login/Logout button, which keeps its width and the same margin
  as Login. The status icons of the advanced configuration keep their size: next to a long value,
  as Remote.LoadPath and PlayerVersionOverride have once configured, they shrank to nothing.
- **"Fix Addressables configurations" no longer leads to a binary catalog.** It set Addressables'
  "Enable Json Catalog" but not the `ENABLE_JSON_CATALOG` scripting define that Addressables' own settings
  inspector adds with it, so the first build after the fix wrote a binary catalog and failed with the
  "Unable to build Addressables correctly" help box (untick and tick "Enable Json catalog" by hand, save,
  restart). `AddressablesConfiguration.Apply()` now adds the define for Windows, Android, iOS and WebGL,
  and `IsConfigured` requires it: projects fixed before show the top-level settings in red until fixed
  again.
- **The 2026.6 update routine moves the legacy folders on its first run.** `VirtuademyFolderMigrator`
  created the destination folders (`Assets/Virtuademy/Editor/Settings`, …) inside the asset-editing batch,
  where they are not in the asset database yet, so every move into them was refused ("Parent directory is not
  in asset database") and the assets moved only on a second run. The destinations are now created before the
  batch.
Fixes to the v2026.5 -> v2026.6 update routine, found by running it from the command line on a 2026.5 project moved to
2026.6.0. Editor-only.

- **The routine opens and runs in a project that does not compile.** The migrators, the window, the re-save, the save
  guard and `UpdateRoutinesCli` sat outside every assembly definition, so they were compiled into the project's own
  `Assembly-CSharp-Editor`, and a compile error in the project's assemblies (an auto-referenced assembly definition that
  still referenced `Reflectis.CreatorKit.Worlds.Placeholders` was enough) kept that assembly from being built: no menu
  item, and `-executeMethod` answered "class could not be found", in exactly the state the routine exists to repair. They
  are now the assembly `Virtuademy.SDK.Environments.UpdateRoutines` (`BreakingChangeSolvers/Editor/`, editor-only,
  references nothing), which no code of the project can break. Type names, namespaces, the menu path and the entry
  point `Virtuademy.BreakingChangeSolvers.UpdateRoutinesCli.Run` are unchanged. `BreakingChangesSolver2025_4`, which
  names SDK runtime types, stays outside every assembly definition and moved to `BreakingChangeSolvers/Legacy/Editor/`:
  the new assembly does not pull it in, and the `Editor` folder keeps it compiled into `Assembly-CSharp-Editor`, with
  its menu **Virtuademy ▸ Update routines ▸ v2025.3 -> v2025.4** (a package script outside every assembly definition
  and outside an `Editor` folder is not compiled at all).
- **Assembly references in `.asmdef` and `.asmref` files are mapped to the right assemblies.** They went through the
  namespace rules, so `Reflectis.CreatorKit.Worlds.Placeholders` became `Virtuademy.Environments.ScriptingApi.Placeholders`,
  a namespace, and the project still did not compile. `VirtuademyAssemblyReferenceMigrator` maps them from the real list
  of the 2026.5 assemblies (Creator Kit Worlds Core, Placeholders, Visual Scripting, Tasks, Dialogs, Analytics; SDK
  Core, Dialogs, Graphs, Tasks, Tenant Configuration, ReflectisApi; under both brands) to the 10.0.0 ones, by name and by
  `GUID:` (a GUID that still resolves is left alone; one that does not is written as the successor's name). It only
  writes the name of an assembly that exists in the project or its packages, never touches a reference to an assembly that
  exists now, and removes the duplicates the mapping creates. A reference with no successor in the project is left as it
  is and reported (`unmappedAssemblyReferences` in the command line's report, a warning in the window); one whose
  successor took over only part of it is reported as `partialAssemblyReferences`.
- **A legacy asset whose namesake the SDK regenerated no longer blocks the folder step for good.** The SDK writes
  `Assets/Virtuademy/Settings/SampleTexture.asset` on the first run after the update, so the old
  `Assets/ReflectisSettings/SampleTexture.asset` was refused ("something already sits at") on every run and the legacy
  folder never went away. Every reference to either asset is now looked for by GUID before anything is deleted: an
  unreferenced old asset is deleted; an unreferenced namesake is deleted and the old asset moves in (its references keep
  their GUID); two referenced assets with the same content are merged by pointing the references of the old one at the
  other; two referenced assets that differ are still left alone and reported. The report says what was merged.
- **No exception when the project never built its Visual Scripting node library.** `UnitBase.Rebuild()` threw a
  NullReferenceException (Visual Scripting's plugin is not started in such a project, which a project only ever opened in
  batch mode is). The routine recognizes the state and skips the rebuild with a log line that says what builds the library
  (**Update VS Library**, or `SetupCli -vdSetupSteps vslibrary`); the command line's report says `nodeLibrary:
  "NotBuilt"`, with a note.

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
- **The update routine rewrites only the manifest entries of packages the registry has listed.**
  - The brand rules ran over the whole `Packages/manifest.json`, so a package of ours that creators
    add by hand and no registry entry lists (`Reflectis-CreatorKit-Worlds-Avatars`,
    `Reflectis-SDK-RPMAvatars`) had its key and URL turned into a `Virtuademy-*` repository that
    does not exist, and the project stopped resolving.
  - The manifest now goes through the rules one entry at a time, and only for the ids a registry
    entry has ever listed, including the develop channel's intermediate names, plus the
    installer's. Every other entry is left exactly as it is. Scripts, scenes, prefabs and assets are
    rewritten as before.
- **The update routine unpins only the packages it renames.**
  - It removed from `packages-lock.json` every git package with one of our prefixes, so a package of
    ours the routine had not changed (one a creator added by hand, at no ref) was re-resolved to
    its default branch's latest commit.
  - It now unpins only the git entries whose manifest entry it rewrote (old and new id of a renamed
    package, or a changed URL). The re-save waits for those packages only, so one that does not
    resolve for unrelated reasons no longer holds it.
  - A project that already ran the routine finds nothing to rewrite and is not unpinned again;
    "Re-resolve packages" in the setup window is still there for that.
- **A deploy that went wrong says so at the end.** Build & Deploy closed every run with "All world
  deploys completed", also when the platform had refused the interpreted assembly and every scene
  had been held back. It now ends with how many problems there were and how many scene imports
  went through, in the Console and in a dialog; only a clean run says it completed. The refusal of
  the assembly is listed in the window with its reason (each distinct violation once, with the
  platforms that raised it, or the platform's message and error code), where it used to say only
  that the assembly was rejected. The tenant deploy ends the same way.
- **Scene scripts resolve on the player: every project's scripts compile as
  `VirtuademyEnvironmentScripts`.** Unity binds a scene's script by the assembly name in its
  MonoScript and only accepts names the player listed when it was built, so a scene compiled against
  `HotUpdate_<productGUID>_<fingerprint>` loaded with every scripted component missing although the
  DLL loaded fine. The scripts now live in `Assets/VirtuademyEnvironmentScripts/`, whose asmdef
  declares that one name in every creator project, and the DLL is published exactly as compiled.
  Its file, its record on the platform and its storage folder are named
  `EnvironmentsDll_<productGUID>_<fingerprint>`, which still changes with the code. The build no
  longer renames the asmdef, so it no longer stops for a recompilation halfway through a deploy.
  A project with its scripts in `Assets/HotUpdate` is refused by the build until the interpreter
  configuration is run again, which moves the folder. Needs a player that loads environment
  assemblies under that name (Virtuademy-Unity `ScriptAlias`) and the platform's matching identity
  check; an older player keeps showing the scripts missing.
- **A scene zip uploaded by hand carries its scripts.** Every addressables build now writes
  `<scene>/environment-dll.json` into each scene zip, naming the interpreted assembly the scenes were
  compiled against (`null` without HybridCLR), and builds the assembly bundle beside them as
  `ServerData/<AssemblyName>.zip` — before, only Build & Deploy built it. Upload the bundle next to
  the scene zips and the platform's archive import registers it and links the scenes, which until
  now only the window's deploy did: a scene zip imported from the Backoffice loaded with every
  scripted component missing. Needs the platform's matching import (SPACS-Virtuademy with the
  `environment-dll.json` support); an older platform ignores the file.
- **Build & Deploy to worlds uploads the assembly before the scenes, every time.** The import of a
  scene now refuses one whose declared assembly is not registered, so the bundle has to be there
  first. It is no longer skipped when the scripts are unchanged: that marker is per project, not per
  tenant, and a deploy to a second tenant went out without its scripts. A scene whose assembly the
  platform rejects is not published, instead of being published without its scripts. The explicit
  link after the scenes stays, so the window still works against a platform that predates the
  declaration. Deploy to Tenant is unchanged: tenant environments still cannot carry scripts.
- **The hot-update asmdef references every whitelisted assembly from the start.** The interpreter
  setup used to seed only `Virtuademy.ScriptingApi` and `Virtuademy.Environments.ScriptingApi`, and
  its comment claimed the whitelist admitted nothing else first-party; `policy.json` also admits
  `SPACS.Dialogs`, `SPACS.Tasks`, `Unity.TextMeshPro` and `UnityEngine.UI`, so a script the publish
  would accept failed to compile until the creator added the reference by hand. The setup now adds
  all six, on a new asmdef and on an existing one (additive: nothing the creator added is removed).
  Since the asmdef takes part in the assembly fingerprint, the first publish after the setup renames
  the assembly once.
- **The placeholders get short paths, one folder per module.** In a creator project under
  `C:\Users\<user>\UnityProjects\<Org>\<Project>`, 225 files of this package went past Windows'
  260-character path limit inside `Library/PackageCache`, and Unity, Visual Studio and Notepad++
  could not open them. The `Virtuademy.Environments.ScriptingApi` assembly folder moves from
  `Runtime/ScriptingApi` to `ScriptingApi/`, beside `Runtime/`; each placeholder's scripts sit in
  `ScriptingApi/Placeholders/<Module>/` (`POI`, `Quiz`, `Chatbot`…) instead of the
  `Scripts/ScriptPlaceholders/<X Script Placeholder>` and `Scripts/Internal/SDK_*` folders; their
  prefabs, models, materials and sprites move to `Prefabs/<Module>/`; fonts, audio and the shared UI
  prefabs to `Common/`. `PlaceholderBases` is gone: its bases are in `Placeholders/Utilities`, the
  spawnable object in `Placeholders/SpawnableObject`, floor and spawn point in
  `Placeholders/Fundamentals`. Unreferenced duplicates stay, under `Prefabs/<Module>/Legacy/`.
  Every file keeps its `.meta`, so GUIDs, prefab and scene references, the assembly, its name and
  every namespace are unchanged: nothing a creator project or a published environment records
  moves. The longest placeholder path is 91 characters, against 139 before.
- **The skybox experiences' world-space UI follows the same rule, and is now generic UI.** Its
  generic panels `WorldSpaceButtonPanel` and the menu panel (`MenuPanel/ButtonChoicePanel`), their
  panel settings and `ButtonTheme.tss` live in `Common/Prefabs/UI/`, beside the toggle and video
  examples (`Common/Prefabs/UI/TogglesExamples/`, with the graphics they use under `Graphics/`, and
  `Common/Prefabs/UI/VideoOverlay/`). `Runtime/Utilities` is gone: the UI Toolkit components — the
  binders, the toggle and its group, the video binder, the rebuilder — are in
  `Runtime/UIKit/UIToolkit/`, beside `LocalizedUITypes`, and their inspectors in
  `Editor/UIKit/UIToolkit/`. Same assembly, same namespace, same GUIDs. `Prefab/` keeps only the
  hand reference prefabs.
- Seventeen empty folders left over from the 9.0.0 merge are removed.
- **The analytics ControlManager/Informative-Item prefabs and graphs get short paths.** The folders `Runtime/Analytics/Prefabs/ControlManager_Informative_Item` and `Runtime/Analytics/Graphs/ControlManager_InformativeItem` become `.../ControlManager`, and the ` Variant` suffix Unity appends to prefab-variant file names is dropped across the package (26 prefabs). The longest analytics path is 93 characters, against 115 before.
- **The Visual Scripting scripts get short folders.** `Runtime/VisualScripting` and `Editor/VisualScripting` become `Runtime/VS` and `Editor/VS`; the `Nodes/` level is removed so `Unit/` and `EventUnit/` sit directly under `VS/Scripts/`; `VisualScriptingInteraction` becomes `VSInteraction` and `SyncedVariable` becomes `SyncVar`. The longest VS path drops from 106 to 73 characters. Same assembly, same namespaces (set in each asmdef's `rootNamespace`), same GUIDs — the Visual Scripting node database is regenerated locally (it is gitignored).
- **The toggle and video UI are generic examples under `Common/Prefabs/UI/TogglesExamples`.** Prefabs:
  `Toggles/SimpleToggle` (was SkyboxPOI), `Toggles/ToggleWithVideo` (SkyboxPOI Video),
  `Toggles/ToggleWithOverlayVideo` (SkyboxPOI DirectVideo), `VideoOverlay/VideoOverlay`
  (SkyboxVideoScreen); `MenuPanel/ButtonChoicePanel` (in `Common/Prefabs/UI/`) and the 360 buttons moved alongside. UXML/USS:
  `TogglePanel`, `TogglePanelWithVideo`, `ToggleButton`, `ToggleButtonVideo`, `Toggle.uss`,
  `TogglePanelWithVideo.uss`, `VideoOverlay.uxml/.uss`, `VideoOverlayScreenPanelSettings`; USS classes
  `poi-*` are `toggle-*`. Same GUIDs, so scenes and prefabs keep their references; element names
  (`icon`, `panel`, `media`, `Title`, `Description`, `video-button`, `close-button`…) are unchanged.
- **`UIToolkitToggleElement` only raises events.** `targetName` and `useDisplay` are gone: what
  "open" means is up to `onOpen` / `onClose` (typically `panel.SetActive(true/false)`). The new
  `panel` field is only used to count presses on the open panel's UIDocuments as inside for
  `UIToolkitToggleGroup`; `openClassName` now goes on the trigger only; `startOpen` opens (raising
  `onOpen`) on Start. `UIDocumentBinder` no longer requires a UIDocument on its own object.
- **SkyboxPOI is split into a toggle and a panel document.** The root holds the toggle; `Toggle`
  renders the icon (`SkyboxPOIToggle.uxml` / `SkyboxPOIToggleVideo.uxml`); `TogglePanel` (inactive)
  holds the panel document and its binders, moved with their fileIDs so instance overrides follow.
  `SkyboxPOI.uxml` / `SkyboxPOIVideo.uxml` keep only the panel, with an `icon-spacer` where the icon
  was.
- **Video is a binder: `UIToolkitVideoBinder` replaces `WorldSpacePOIVideoButton` and
  `SkyboxVideoOverlay`** (both removed, with `SkyboxVideoOverlay.prefab` and
  `SkyboxPOIDirectVideo.uxml`). The binder plays its clip/URL into a named element while bound and
  stops when disabled, so a video opens and closes with `SetActive`. `UIDocumentVRPlacement` moves a
  screen-space document to world space in front of the camera in VR. `SkyboxVideoScreen.prefab`
  combines them with a `UIToolkitButtonBinder` for the X. `SkyboxPOI Video` opens a `Video` child
  from its "Start video" button; `SkyboxPOI DirectVideo` has the SkyboxPOI structure with the video
  as `TogglePanel`, its X calling `UIToolkitToggleElement.Close()`.
- **The POI toggle and group are general UI Toolkit components.** `WorldSpacePOIToggle` is
  `UIToolkitToggleElement` (a trigger element shows/hides a target element) and `WorldSpacePOIGroup`
  is `UIToolkitToggleGroup`; neither is tied to POIs nor to world-space panels. Fields `iconName` /
  `panelName` are `triggerName` / `targetName`, and the group's `pois` is `toggles`
  (`[FormerlySerializedAs]` on all three, same script GUIDs, so prefabs keep working). New options:
  an empty trigger name (the toggle is driven only from code / UnityEvents), `useDisplay`,
  `openClassName` (a USS class on target and trigger while open), `SetOpen(bool)`, a `Closed` event,
  and the group's `exclusive` flag (off: several toggles may stay open, the group only closes them
  on an outside tap). No `[RenamedFrom]`: the two were never released and no graph names them.
- **The UI Toolkit binders share one base, `UIDocumentBinder`.** The document lookup, the lazy
  bind retried for up to 120 frames, `Rebind()` and the detach watch that re-binds after a rebuild
  were copied in every binder; `WorldSpaceButtonBinder`, `WorldSpacePanelImageBinder`,
  `WorldSpacePanelImageCarousel`, `LocalizedUIBinder`, `WorldSpacePOIVideoButton` and
  `UIToolkitToggleElement` now inherit them and only implement `BindTo(root)` /
  `UnbindFromTree()`. The `document` field keeps its name, so scenes and prefabs are untouched.
  `WorldSpacePanelImageBinder.Apply()` is now `Rebind()`: on a disabled component it waits for
  OnEnable instead of writing the images right away. `SpaceLineHeightBinder` keeps its own cycle
  (it also binds in Edit Mode).
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

### Breaking
- **Every published world must be rebuilt.** A built Addressable bundle records each component by assembly, namespace and class, and all three changed.
- **Every Visual Scripting graph must be migrated**, in a creator's project and in this repo. A graph records each unit as namespace plus type — no assembly — so the namespace change is the one that reaches graphs. Run `Virtuademy Worlds/Creator Kit update routines/Package rename migration`, then `Regenerate Nodes`. **Expect deserialization errors on the first open afterwards, and do not save until they stop**: while a worker cannot resolve a renamed unit yet, Visual Scripting substitutes `MissingType` and that renumbers the JSON `$id`s, which surfaces as "Object definition has not been encountered for object with id=N". Most clear by themselves once the assemblies load — the log then says the missing type "was found" — but some are cached in the asset's imported artifact and survive a reopen: those need a **Reimport** on the asset, then a look at the graph to confirm the units are real nodes, and only then a save — **on every affected asset**, since each caches its own import artifact and the errors surface a few at a time. Saving before that check is the one way to make the loss permanent. Verified on this repo: 55 graph-bearing assets, every `$ref` still preceded by its `$id` after the rename, and no compile error.
- Every `using Virtuademy.CreatorKit.Worlds…` in a creator's own scripts breaks, and so does any asmdef that names one of the three assemblies as a string rather than a GUID. The migrator rewrites both.
- A project that depends on `com.anotherealitysrl.virtuademy-creatorkit-worlds-core` in its `manifest.json` no longer resolves it. Existing registry releases are unaffected: each pins the git repo at a tag, and that tag still carries the old id.
- Five namespaces the package declares are **not** renamed and are not part of this change: `Virtuademy.SDK.Core`, `...Core.ApplicationManagement`, `...Core.ChatBot`, `...Core.Networking` and `...Core.NetworkingSystem`. Those five types moved here from SDK-Core keeping their original namespaces so consumers' `using` directives kept working, and renaming them now would break the very thing they exist to preserve.
- **`ManipulablePlaceholder.GizmosEnabled` is now `GizmosDisabled`**, with the serialized field and the inspector row inverted to match. The default for a fresh placeholder is unchanged; a placeholder authored before this carries the old boolean under the old name, so its value is lost on reimport and reads as "gizmos shown". Re-check the box on any placeholder that had gizmos turned off.

### Added
- **The TMP component under a POI placeholder is the source of truth for its text.** The inspector fields already pushed their value into the TMP on edit; selecting a placeholder now pulls the TMP text back into the serialized field, so the inspector shows what is actually displayed, and the runtime getters read the TMP with the serialized field as fallback. A creator who typed straight into the TMP no longer sees the text reverted.
- **The update routine resolves project assets that share their GUID with a package asset.**
  It is step 3 of `v2026.5 -> v2026.6`, and the folder step becomes step 4.
  - **Where the collisions come from.** Creator projects imported template assets with their
    `.meta` files: fonts, a click sound, UI prefabs, utility graphs. Some of them later moved into
    this package with the same GUIDs. Unity then logs "GUID [...] for asset 'Assets/...' conflicts
    with 'Packages/...' — Assigning a new guid".
  - **What Unity does with them.** The new GUID goes to the project's copy, and only in memory.
    The project's references silently resolve to the package's asset, and every scene saved
    afterwards writes a GUID that no other machine knows. Seen on Reflectis-EducationContent:
    six assets, with three references already saved under such a GUID.
  - **What the step does.** For each collision it either keeps the project's copy under a
    permanent new GUID and rewrites the references to it (`.meta`, scenes, prefabs, assets,
    materials, UXML/USS and the rest under `Assets/` and `ProjectSettings/`), or deletes the copy
    in favour of the package's.
  - **Defaults.** It keeps the copy when the two differ and uses the package's when they are
    identical. The new GUID is the one Unity assigned in memory, so references saved on this
    machine since the conflict stay valid.
  - **Detection.** It reads our packages' `.meta` files directly. For other packages it relies
    on the AssetDatabase reporting a different GUID.
- **The interpreter is reinstalled when HybridCLR changes version.**
  - `HotUpdateSetupper.Setup` used to keep any interpreter it found in the project's local IL2CPP
    copy (`HybridCLRData/LocalIl2CppData-*`). HybridCLR only checks that the folder exists, so after
    the HybridCLR package moved to another version the copy kept the old libil2cpp. That happens
    on a Virtuademy update, or on the repair of a project that had pulled another HybridCLR.
  - The setup now compares the installed libil2cpp version with the package's and reinstalls on a
    mismatch.
  - `GetInterpreterVersionIssue` reports the mismatch, which the setup window uses to offer Fix.
  - It is not part of `GetSetupIssue`, so the publish gate is unchanged: the published DLL is
    compiled without the local IL2CPP.
- **`InputSettingsMono` is back within a creator's reach**, in
  `Virtuademy.Environments.ScriptingApi.Placeholders` with its data class `InputSettings`. It lived
  in the application framework (`Virtuademy-SystemCore`), which creator projects stopped installing
  in 2026-09, so a world could no longer declare its starting input settings. The script keeps its
  GUID, so scenes that already carry the component keep it and its values.

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
- **The v2026.5 -> v2026.6 update no longer lets a graph be saved broken.** Between updating the
  packages and applying the routine, a scene or prefab is opened by an editor that no longer knows
  its node types: Visual Scripting swaps them for `MissingType` and renumbers the graph's JSON ids.
  One save in that window was enough to make every graph with SDK nodes unreadable for good
  ("Object definition has not been encountered for object with id=N"), and the routine itself
  offered that save, through the usual "save modified scenes?" prompt before rewriting the open
  scenes. Apply now closes them without saving, after saying which unsaved changes are lost, and a
  new `VirtuademyUpdateSaveGuard` refuses any save (Ctrl+S, prompts, Prefab Mode, other tools) of a
  file whose Visual Scripting data still carries the old names, until the routine has rewritten it.
  A creator whose graphs already broke this way restores the files from before the update and runs
  the routine on them without opening them first.
- **Typed Visual Scripting variables keep their type through the rename.** A graph, object or scene
  variable declared with an SDK type stores the type's assembly-qualified name, and the namespace
  rules rewrote its assembly to a name that does not exist (the placeholders' old assembly became
  `Virtuademy.Environments.ScriptingApi.Placeholders`; they live in
  `Virtuademy.Environments.ScriptingApi`), so the variable came back as `Unknown`. The routine now
  writes down the assembly that declares the type, also when Unity has folded the name over two
  lines.
- **The package's GLB models import in a creator project.** `ChatbotAvatarFemale.glb`,
  `ChatbotAvatarMale.glb` and the Scene Changer's `portal.glb` are imported by glTFast's
  `GltfImporter`, but the package never declared `com.unity.cloud.gltfast`: they only worked in
  Virtuademy-Unity, whose own manifest carries it. In a creator project the three files showed up
  as plain files and the two chatbot avatar prefabs, which nest them, lost their model. The package
  now depends on `com.unity.cloud.gltfast` 6.16.0, the version the application ships.
- **The interpreter setup drops missing asmdefs from HybridCLR's Hot Update Assembly Definitions.**
  The list only ever grew: an asmdef deleted or replaced — a hot-update folder removed and set up
  again — stayed registered as a missing reference beside the one in use. `RegisterHotUpdateAssembly`
  now removes the entries whose asset no longer exists, and only those. `GetSetupIssue` reports them
  — HybridCLR reads `.text` off every entry and throws on a missing one when it compiles the DLLs —
  so the setup window shows the project as not ready and its button, which runs the cleanup, is
  enabled.
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
