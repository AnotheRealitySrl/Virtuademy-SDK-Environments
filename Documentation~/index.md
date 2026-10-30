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
**Virtuademy > Publish Environments** window does with buttons, for CI and for an assistant:

```
Unity -batchmode -nographics -projectPath <project> -logFile -
      -executeMethod Virtuademy.SDK.Environments.Editor.EnvironmentsCli.Run
      -vdCommand <command> [options]
```

| Command | What it does |
|---|---|
| `status` | the session, whether the Addressables are configured, the scene list and the interpreter. No network |
| `session [-vdRenew]` | the session fields of the computer's only usable connection; with `-vdRenew` renews an expired session silently, or exits 11 |
| `login [-vdConnection <connection> \| -vdApp <app> [-vdEnv <key>]] [-vdNoBrowser] [-vdLoginTimeoutSeconds <n>]` | logs in to a connection of the per-user store, or to the one of a configuration file of the project (added to the store when it is new). Prints `AUTH_URL <url>` as soon as it has it and opens the browser unless `-vdNoBrowser`; gives up 180 s after the URL (exit 27) |
| `logout [-vdConnection <connection>]` | ends the connection's session (the Azure account stays while another connection uses it) |
| `connections` | every connection of the store: tenant, environment, user, the platform of its app, whether its token is valid, and whether it is the computer's only usable one (`default`). No network |
| `whoami [-vdConnection <connection>]` | one connection (the computer's only usable one when none is named); exit 11 when it has no session. Never the token |
| `tenant-config -vdConfigFile <path> [-vdEnv <key>]` | imports the file downloaded from the Back Office into the per-user connection store, as the window's **Import configuration file...** does: one connection per environment of the file (or only `-vdEnv`'s), each one then used by every environment project of the computer. Writes nothing to the project; needs the network; does not log in; a file whose app is not a CreatorKit app is exit 32 |
| `worlds [-vdConnection <connection>]` | the worlds the login can deploy to, and where each included scene is already published. `-vdConnection` (also on `preview` and `deploy`) names the connection to act on, the computer's only usable one when it is not given |
| `build [-vdScenes a,b] [-vdConnection <connection>]` | verifies the interpreted scripts on the connection's platform (the computer's only usable one when none is named), builds every platform each included scene needs, zips into `ServerData` |
| `preview (-vdWorlds <ids> \| -vdTenant) [-vdConnection <connection>] [-vdExpectTenant <label>] [-vdScenes a,b]` | what a deploy there would add and update; checks the tenant label when given |
| `deploy (-vdWorlds <ids> \| -vdTenant) [-vdConnection <connection>] -vdExpectTenant <label> [-vdBuild] [-vdDryRun] [-vdScenes a,b]` | uploads and imports the built zips (and the interpreted assembly, to worlds); `-vdExpectTenant` is the tenant label of the connection it acts on (`whoami` shows it) and is mandatory (exit 24 when missing, 30 when it is another tenant's); `-vdBuild` builds first, `-vdDryRun` sends nothing |

| `published [-vdScenes a,b] [-vdConnection <connection>]` | every scene of the scene list (included in the build or not): where it is published and the thumbnail and localization file each place holds (address, SHA-256, whether the project's `Assets/Virtuademy/Editor/PublishFiles/` holds the same file, `canWrite`); whether the tenant can receive files from the SDK. Read-only, any tenant |
| `save-files [-vdScenes a,b] [-vdWorlds <ids> \| -vdTenant] [-vdBackupFolder <path>] [-vdDryRun] [-vdConnection <connection>]` | saves those files into `PublishFiles/` (never twice the same file) and writes a manifest for `restore`; `-vdBackupFolder` writes a full copy and its manifest to that folder instead |
| `set-files -vdExpectTenant <label> -vdScenes <one> [-vdThumbnail <asset>] [-vdLocalization <asset>] (-vdWorlds <ids> \| -vdAllWorlds \| -vdTenant) [-vdExcludeWorlds <ids>] [-vdDryRun] [-vdConnection <connection>]` | replaces the files at those places with project assets (under `Assets/`); an image goes online as a JPEG of at most 2560 x 1440 (converted into `PublishFiles/` when it is not one already), the files the places hold now are saved to the project first, a place with the same file is `unchanged`; a file the platform would refuse is exit 37, a place not changed is exit 38 |
| `restore -vdExpectTenant <label> -vdManifest <path> [-vdScenes a,b] [-vdWorlds <ids> \| -vdTenant] [-vdDryRun] [-vdConnection <connection>]` | puts back what a `save-files` manifest recorded, for the same connection, at the same environments |
| `scene-files -vdScenes <one> [-vdThumbnail <asset>] [-vdLocalization <asset>] [-vdClear]` | the scene's default thumbnail and localization file, in the scene list (needs no network; without options it shows them). A deploy sends them when it makes the scene's environment in a place, and never again: `deploy.targets[].scenes[].defaultFiles` says what it did, `-vdDryRun` reports it in `defaultFiles` |

A connection is one tenant of one Configuration-API instance (ADR 0037). It is named by its key (the instance's base URL, `#`,
the tenant key), its tenant key, its tenant label or `app/env`; a name that fits none or several is exit 26. The connections live
outside the project, in `%USERPROFILE%\.virtuademy\sdk\`, encrypted for the OS user. A command that names no connection acts on
the computer's only usable one: with two or more usable connections on the computer every such command must name one (exit 26
otherwise), also in a project that has always deployed to the same tenant; nobody chooses a default connection. Every
environment project of the computer uses every connection of the store. `worlds`, `preview`, `deploy` and `build` act on the
connection `-vdConnection` names, and refuse a connection whose app is not a CreatorKit app (exit 32). The store's `status.json` in that folder lists them with no secret: `connections` and `whoami`
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
missing, 24 tenant not confirmed (`-vdExpectTenant` missing), 26 connection unknown, 27 login timed
out, 28 login failed, 29 `login` in batch on an OS with no encrypted store (the login would end with the process), 30 target
mismatch (`-vdExpectTenant` is not the tenant of the connection the command acts on), 31 platform version mismatch (`preview` or
`deploy` to a tenant whose platform version has another year or release than the one the project was set up from, or reports
none; a project set up from `develop` is not checked), 32 platform mismatch (the connection's app is not a CreatorKit app: `worlds`,
`preview`, `deploy` and `build` on it, a `login` or `tenant-config` with a key of another platform; the remedy is the CreatorKit
configuration file), 37 a thumbnail or localization file would be refused by the platform (nothing was sent), 38 one or more places
were not changed (`set-files`, `restore`: not allowed, the tenant's platform cannot receive files yet, not published there, or an error; the
others may have changed). 25 (API host not matching the logged-in tenant) is no longer
returned and stays reserved, and so does 23 (configuration file not ignored by git, up to 2026.6: `tenant-config` writes
nothing to the project any more). Arguments are strict (a misspelt, duplicated or stray option is exit 10). A timeout during
a deploy lets the step in flight finish and reports what was and was not done. Batch mode never starts an interactive login on its own: log in once with `login` (or from the connection's group in **Virtuademy > Publish Environments**), and the
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
