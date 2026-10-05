# Virtuademy-SDK-Environments

The package a creator authors a Virtuademy environment with: the placeholder bases and the
placeholder set, the Visual Scripting nodes and event units, the object spawner and interaction
contracts, the task and dialog adapters, the analytics contract, the samples, and the editor
publish tooling. Package id `com.anotherealitysrl.virtuademy-sdk-environments`.

## How to install

Use the setup window of `Virtuademy-SDK-Environments-Setup` (`Virtuademy/Setup/Setup project`):
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
| `Virtuademy.SDK.Environments.Editor` | editor code, publish tooling and editor authentication |
| `Virtuademy.SDK.Environments.HybridCLREditor` | the HybridCLR integration (`defineConstraints: [HYBRIDCLR_INSTALLED]`) |
| `Virtuademy.SDK.Environments.I2Loc`, `.I2Loc.Editor` | the optional I2 Localization bridge |
| `Virtuademy.Environments.ScriptingApi` | the interpreted-script facade and the placeholders, the part of this package the server-side whitelist admits by name |

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

The update routines under `BreakingChangeSolvers/Editor` sit outside every assembly definition, so
they compile into the creator project's own editor assembly. `Virtuademy/Update routines/v2026.5 ->
v2026.6` carries a project across the package renames.

## History

The READMEs and changelogs of the five packages merged into this one at 9.0.0 are kept, as
historical records, under [`legacy-packages/`](legacy-packages/).
