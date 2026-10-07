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
| `Virtuademy.SDK.Environments` | all runtime code |
| `Virtuademy.SDK.Environments.Editor` | editor code, publish tooling and editor authentication |
| `Virtuademy.SDK.Environments.HybridCLREditor` | the HybridCLR integration (`defineConstraints: [HYBRIDCLR_INSTALLED]`) |
| `Virtuademy.SDK.Environments.I2Loc`, `.I2Loc.Editor` | the optional I2 Localization bridge |
| `Virtuademy.Environments.ScriptingApi` | the interpreted-script facade, the one first-party assembly the server-side whitelist admits by name |

The update routines under `BreakingChangeSolvers/Editor` sit outside every assembly definition, so
they compile into the creator project's own editor assembly. `Virtuademy/Update routines/v2026.5 ->
v2026.6` carries a project across the package renames.

## History

The READMEs and changelogs of the five packages merged into this one at 9.0.0 are kept, as
historical records, under [`legacy-packages/`](legacy-packages/).
