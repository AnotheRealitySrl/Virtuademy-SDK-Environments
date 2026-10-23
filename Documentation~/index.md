# Virtuademy-SDK-Environments

The package a creator authors a Virtuademy environment with: the placeholder bases and the
placeholder set, the Visual Scripting nodes and event units, the object spawner and interaction
contracts, the task and dialog adapters, the analytics contract, the samples, and the editor
publish tooling. Package id `com.anotherealitysrl.virtuademy-sdk-environments`.

## How to install

Use the setup window of `Virtuademy-SDK-Environments-Setup` (`Virtuademy/Setup project`):
it reads the platform's package registry and installs this package together with its
dependencies at the versions a platform release was published with.

To modify the package itself, mount it as a submodule under the `Packages` folder instead.

Dependencies:

- `com.anotherealitysrl.virtuademy-sdk-core` — the interpreted-script surface and the transport
- `com.anotherealitysrl.spacs-utility` — generic utilities and the Visual Scripting node bases
- `com.anotherealitysrl.spacs-graphs`, `com.anotherealitysrl.spacs-tasks`,
  `com.anotherealitysrl.spacs-dialogs` — the graph, task and dialog engines the adapters build on

The `Virtuademy-SDK-*` packages are the SDK proper (Core, Environments, Library); the `SPACS-*`
ones carry no platform and can be used on their own.

## Assemblies

| Assembly | Holds |
|---|---|
| `Virtuademy.SDK.Environments` | the runtime code an interpreted script must not reach: Visual Scripting nodes, the task, dialog and analytics adapters, the catalog variables |
| `Virtuademy.SDK.Environments.Editor` | editor code, publish tooling (`EnvironmentsBuild`, `EnvironmentsDeploy`, the window and the `EnvironmentsCli` command line) |
| `Virtuademy.SDK.Environments.HybridCLREditor` | the HybridCLR integration (`defineConstraints: [HYBRIDCLR_INSTALLED]`) |
| `Virtuademy.SDK.Environments.I2Loc`, `.I2Loc.Editor` | the optional I2 Localization bridge |
| `Virtuademy.Environments.ScriptingApi` | the interpreted-script facade and the placeholders, the part of this package the server-side whitelist admits by name |

## Command line

`Virtuademy.SDK.Environments.Editor.EnvironmentsCli` builds and publishes from a terminal what the
**Virtuademy > Addressables management** window does with buttons, for CI and for an assistant:

```
Unity -batchmode -nographics -projectPath <project> -logFile -
      -executeMethod Virtuademy.SDK.Environments.Editor.EnvironmentsCli.Run
      -vdCommand <command> [options]
```

| Command | What it does |
|---|---|
| `status` | the session, whether the Addressables are configured, the scene list and the interpreter. No network |
| `session [-vdRenew]` | the session fields of the project's active connection; with `-vdRenew` renews an expired session silently, or exits 11 |
| `login [-vdConnection <connection> \| -vdApp <app> [-vdEnv <key>]] [-vdNoBrowser] [-vdLoginTimeoutSeconds <n>]` | logs in to a connection of the per-user store, or to the one of a configuration file of the project (added to the store when it is new). Prints `AUTH_URL <url>` as soon as it has it and opens the browser unless `-vdNoBrowser`; gives up 180 s after the URL (exit 27) |
| `logout [-vdConnection <connection>]` | ends the connection's session (the Azure account stays while another connection uses it) |
| `connections` | every connection of the store: tenant, environment, user, whether its token is valid, which one the project uses. No network |
| `whoami [-vdConnection <connection>]` | one connection (the project's active one when none is named); exit 11 when it has no session. Never the token |
| `tenant-config -vdConfigFile <path> [-vdEnv <key>] [-vdConfigFolder <folder>] [-vdAllowTracked]` | imports the file downloaded from the Back Office into `Assets/Editor/TenantConfiguration`, adds it to the App Assets and selects it; exits 23 when git does not ignore where it would land (it holds the app's secret), unless `-vdAllowTracked` |
| `worlds [-vdConnection <connection>]` | the worlds the login can deploy to, and where each included scene is already published. `-vdConnection` (also on `preview` and `deploy`) must be the project's active connection, or the command exits 25 and sends nothing |
| `build [-vdScenes a,b]` | verifies the interpreted scripts, builds every platform each included scene needs, zips into `ServerData` |
| `preview (-vdWorlds <ids> \| -vdTenant) [-vdExpectTenant <label>] [-vdScenes a,b]` | what a deploy there would add and update; checks the tenant label when given |
| `deploy (-vdWorlds <ids> \| -vdTenant) -vdExpectTenant <label> [-vdBuild] [-vdDryRun] [-vdScenes a,b]` | uploads and imports the built zips (and the interpreted assembly, to worlds); `-vdExpectTenant` is the tenant label the session shows and is mandatory (exit 24 when missing or different); `-vdBuild` builds first, `-vdDryRun` sends nothing |

A connection is one tenant of one Configuration-API instance (ADR 0037). It is named by its key (the instance's base URL, `#`,
the tenant key), its tenant key, its tenant label or `app/env`; a name that fits none or several is exit 26. The connections live
outside the project, in `%LOCALAPPDATA%\Virtuademy\SDK\`, encrypted for the OS user, and the project's active connection is
the last one it logged in to. The store's `status.json` in that folder lists them with no secret: `connections` and `whoami`
can be read from it without starting Unity. `-vdResultFile <path>` (any command) writes the REPORT, and only it, to that file,
replaced whole, with `state: done`; a `login` writes it first with `state: awaiting-browser` and the URL.

Unity refuses to open a project another editor has open, so `login`, `logout`, `connections` and `whoami` run in batch only while the
project is closed (`Temp/UnityLockfile` tells the two cases apart). With the editor open, drop a request file instead: write
`UserSettings/Virtuademy/requests/<id>.json.tmp` with `{ "command": "login" | "logout" | "status", "connection": "...",
"noBrowser": false, "loginTimeoutSeconds": 180, "resultFile": "path" }` and rename it to `<id>.json`. The editor picks it up within
a fraction of a second and writes `<id>.result.json`; a login writes it first with `state: awaiting-browser` and the URL. Nothing
else can be asked this way, never a build or a deploy.

Every run ends with a `[EnvironmentsCli] REPORT {json}` line and exits the editor with a code: 0 done,
1 unexpected, 10 bad arguments, 11 login required, 12 Addressables not configured, 13 nothing to build, 14
build failed, 15 interpreted scripts refused, 16 world not deployable, 17 deploy finished with problems, 18
not a tenant manager, 19 configuration file not imported, 20 platform unreachable, 21 timeout, 22 zips
missing, 23 configuration file would not be ignored by git, 24 tenant not confirmed, 25 target mismatch (`-vdConnection` is
not the project's active connection), 26 connection unknown, 27 login timed out, 28 login failed, 29 `login` in batch on an OS with
no encrypted store (the login would end with the process). Arguments are strict (a misspelt, duplicated or stray option is exit 10). A timeout during
a deploy lets the step in flight finish and reports what was and was not done. Batch mode never starts an interactive login on its own: log in once with `login` (or from **Virtuademy > Available Tenants**), and the
commands renew the session silently. The scenes to publish are the ones ticked **Include in build** in the window's list
(`Assets/Virtuademy/Editor/Settings/AddressablesSceneList.asset`). Details: the class's XML documentation.

## Folders

| Folder | Holds |
|---|---|
| `Runtime/` | the `Virtuademy.SDK.Environments` assembly, one folder per module: `SkyboxExperiences/` holds the components of the skybox experiences' world-space UI, `UIKit/UIToolkit/` the UI Toolkit helpers any world-space panel reuses (binders, rebuilder, localized text) |
| `ScriptingApi/` | the `Virtuademy.Environments.ScriptingApi` assembly: the facade at its root, the interaction, spawner and chatbot contracts, and `Placeholders/<Module>/` with each placeholder's scripts. `Placeholders/Utilities/` holds the bases every placeholder derives from |
| `Prefabs/<Module>/` | each placeholder's prefabs and what they are built from (models, materials, sprites), one folder per module of `ScriptingApi/Placeholders/`. A `Legacy/` subfolder keeps the copies nothing in the package references, which published environments may still use |
| `Prefabs/SkyboxExperiences/` | the skybox experiences' world-space UI: the 360 buttons, the POI panels, the video overlay, their styles and graphics |
| `Common/` | fonts, audio and the UI prefabs several modules share, among them the generic world-space panels `WorldSpaceButtonPanel` and `ButtonChoicePanel` |
| `Prefab/` | the hand reference prefabs `SpawnableObjectPlaceholder` loads by path in the editor |
| `Editor/` | the editor assemblies, mirroring the `Runtime/` modules (`Editor/UIKit/UIToolkit/` for the binders' inspectors) |

The placeholders sit outside `Runtime/` to keep paths short: inside a creator project's
`Library/PackageCache/com.anotherealitysrl.virtuademy-sdk-environments@<hash>/`, Windows leaves
about 96 characters for a path within the package before it reaches the 260-character limit that
Unity, Visual Studio and most editors still enforce.

The update routines under `BreakingChangeSolvers/Editor` are an assembly definition of their own,
`Virtuademy.SDK.Environments.UpdateRoutines`, that references nothing but Unity. A project whose own
scripts or assembly definitions still name the old assemblies does not compile after the packages are
updated, and the routines still open and run: that is the state they repair. `Virtuademy/Update
routines/v2026.5 -> v2026.6` carries a project across the package renames, and the same routines run
from the command line with `-executeMethod
Virtuademy.BreakingChangeSolvers.UpdateRoutinesCli.Run -vdRoutine list|2026.6|2026.5`.

## History

The READMEs and changelogs of the five packages merged into this one at 9.0.0 are kept, as
historical records, under [`legacy-packages/`](legacy-packages/).
