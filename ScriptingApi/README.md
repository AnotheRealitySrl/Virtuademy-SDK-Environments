# Virtuademy.Environments.ScriptingApi

The surface an **interpreted script** in an authored world is allowed to call. One assembly, no
references of its own, and the only first-party assembly a script may name.

## Why it exists

Interpreted scripts (HybridCLR hot-update DLLs) are verified server-side against a whitelist —
`policy.json` in `SPACS-Virtuademy/DllVerification`, enforced by `DllVerificationEngine` with a
static Mono.Cecil scan before anything runs. That whitelist has always **allowed the assembly name
`Virtuademy.Environments.ScriptingApi` and the matching namespace prefix**, and **denied**
`Virtuademy.SDK`, `Virtuademy.CreatorKit`, `Virtuademy.Worlds`, `Virtuademy.Core` and
`Virtuademy.ClientModels` outright.

Since the package became `Virtuademy-SDK-Environments` (10.0.0) its own namespaces sit under the
`Virtuademy.SDK` denial, so the perimeter no longer leans on the `Virtuademy.CreatorKit` entry to
cover this package. That entry is **not** dead, though — two application types still live in
`Virtuademy.CreatorKit.Core`, and all eight first-party denied prefixes were measured against the
whole Unity tree on 2026-09-09 and cover live code. `policy.json` needs no change.

One thing to know before touching those denials: they are evaluated **before** the allow list, so
denying `Virtuademy` wholesale would reject this assembly's namespace too and disable every
interpreted script — without an error anyone would connect to the cause. See
`DllVerification/README.md`.

Until now that allowance pointed at nothing: no such assembly existed. The perimeter was therefore
maximal by accident — a script could reach `mscorlib`, a slice of `UnityEngine`, TextMeshPro and
nothing of ours — and the API the whitelist promised was unwritten. This is that API.

## The rule for adding to it

**Nothing here may introduce a capability the platform does not already grant through a shipped
Visual Scripting node.** A graph and a script get the same reach; each member's doc comment names
the node it corresponds to. That keeps the security question about the *mechanism* rather than
about a growing list of powers, and it means widening the surface starts with "which node is this?"

Adding a member is the only way to widen what scripts can do, so it is a deliberate act. Removing
one breaks published worlds, so treat the surface as public API from the first published script.

## What the whitelist forces on the API's shape

Three constraints, all discovered from the policy rather than chosen:

- **No `Task`, no `async`/`await`.** `System.Threading` is a denied prefix, so a script cannot even
  name `Task`. Anything asynchronous takes a callback (`System.Action`, which is allowed) or is
  driven by a coroutine — `IEnumerator` is `System.Collections`, allowed, and
  `StartCoroutine(IEnumerator)` is permitted while the string overload is not.
- **No first-party type in a signature, however indirect.** Only primitives, types this namespace
  declares, and `UnityEngine` types. A `UnityEvent` cannot cross it either, which is why
  `ILocalizationApi.LanguageChanged` is an `event Action<string>` with the UnityEvent subscription
  kept on the implementation side.
- **No generics on the surface.** A generic instantiated only from interpreted code has no AOT
  counterpart and fails at load, not at compile time.

## Shape

`VirtuademyEnvironments` is the entry point, with one property per capability group:

| Group | What it covers |
|---|---|
| `VirtuademyEnvironments.Player` | transforms, teleport, movement, avatar visibility, and the camera: first/third person, the three input arrangements, speed, moves and pans |
| `VirtuademyEnvironments.Screen` | fading to black and back |
| `VirtuademyEnvironments.Localization` | current and previous language, translation by key, switching, the change event |
| `VirtuademyEnvironments.Session` | session id, multiplayer, master client, shared clock, environment name, shard state, local user id and name |
| `VirtuademyEnvironments.SaveData` | the local player's saved values, and leaderboard submissions |
| `VirtuademyEnvironments.Platform` | VR, WebGL or mobile |
| `VirtuademyEnvironments.Help` | whether a help panel exists, opening and closing it, the closed event |
| `VirtuademyEnvironments.Scene` | placeholder initialisation, spawned-object visibility, transitions, returning to the lobby |
| `VirtuademyEnvironments.Tools` | tool-inventory opacity, answer feedback |
| `VirtuademyEnvironments.Sync` | ownership of synced objects: asking for it, releasing it, and the three events for gained, lost and refused |

`VirtuademyEnvironments.IsAvailable` says whether the runtime is present; the group properties throw
`InvalidOperationException` with an explanatory message when it is not, rather than returning null.

The implementation lives **outside this package entirely**, in the app:
`Assets/_Project/ScriptingApi/`, assembly `Virtuademy.Worlds.ScriptingApiBackend`. It installs
itself before the first scene loads, and **every member of it delegates to
`IVirtuademyFramework`** — it resolves no system of its own, because the framework is already the
one place that knows which system answers what. `VirtuademyEnvironments.Install` and `IWorldBackend` are `internal` with
`InternalsVisibleTo` for that one assembly: a script references this assembly in full, so a public
installer would let one script replace the surface every other script is calling.

**Why the app and not this package.** §1 of the package plan assigns `Assets/_Project` "`SM` · the
world systems · the implementations of the Environments game contracts", and what a creator installs
is `SPACS-*` + `Interface` + `Environments` — *not* SDK-Core. An implementation backed by `SM` and
by the SDK-Core system interfaces therefore cannot ship to creators, and putting it in this package
would have shipped it. The creator gets the surface; the platform provides what answers it. The
backend's own namespace, `Virtuademy.Worlds.*`, is on the whitelist's denied list, so a script cannot
reach around the facade to it.

Nothing is cached: each call goes through the framework to whichever system answers it, at the
moment it is made, exactly as the equivalent node does. Calling before the platform has booted
therefore fails the same way a graph would — no special grace.

## Known gaps

- ~~**This package is not yet distributable.**~~ **Closed 2026-09-09.** It said the package named
  `SM` in 56 files and took eight `I*System` interfaces from `Virtuademy.SDK.Core`. All of that is
  gone: the framework contract replaced the system resolution, the contracts moved to the main
  project, and this package no longer references `Virtuademy.SDK.Core` at all.
- **Ten groups against 125 nodes.** The package ships 94 flow/getter nodes and 31 event nodes;
  measured 2026-09-09. What a script still cannot do: dialogs, tasks and quiz, **synced
  variables**, analytics, spawning objects, the contextual menu, the control manager, the
  interactable and manipulable events, video events, and scene navigation beyond the lobby
  (`Change Scene`, `Reload Scene`, `Check Scene Availability`). Several are blocked by the surface
  rules rather than by effort — they would need a placeholder, a DTO or a `CM*` model to cross the
  boundary, so each needs a flattening decision of its own.

- **The awaitable events are not exposed, and that is a real difference from a graph.** Ten of the
  31 event nodes are `AwaitableEventUnit`: the platform *waits* for every registered graph before
  it proceeds. `Scene: On Load`, `On Setup` and `On Unload` are three of them, and the seven quiz
  ones are the rest. A `event Action` cannot express "the platform is holding for you", so a
  faithful version needs either a deferral token declared in this namespace or a completion
  callback — and a handler that forgets to complete hangs the world. Until that decision is made,
  a scripted world cannot delay setup or teardown the way an authored graph can. `On Setup
  Completed` is a plain notification and has no such problem, which makes it the obvious next
  member.

- **`VirtuademyEnvironments.Sync` reaches the component, not the framework.** Every other group delegates to
  `IVirtuademyFramework` for everything. The three ownership operations do not: the nodes behind
  them (`CheckOwnershipNode`, `RequestOwnershipNode`, `ReleaseOwnershipNode`) call the
  `SyncedObject` component and resolve no system, so there is nothing for the framework to decide.
  Only the events go through it, because the signal starts on a per-object bridge in the
  application and a script has no graph to register against.
- **Two members the framework has and this does not**, on purpose. `JoinExperience` takes a
  `CMExperience` that only `FindExperienceByAddressableName` can produce, and both would have to be
  flattened together; and `SpawnProjectAsset` returns a `GameObject` through a callback, which means
  `Action<GameObject>` — a generic instantiation whose AOT counterpart cannot be guaranteed from
  here.
- **`PlayerCount` was removed** from the session group. It was on the first slice with no node
  behind it, which is the rule at the top of this file failing quietly the first time it was
  applied.
- ~~**No negative test.**~~ **Half closed 2026-09-09**, and the half that was missing was not the
  half this bullet claimed. `DllVerification` already had 45 tests against the engine, synthesizing
  subject assemblies with Cecil, including `System.IO`. What none of them did was name a
  first-party symbol, so the sentence people rely on was matched against a prefix list by eye.
  `PerimeterRedTeamTests` now pins it: `SM` refused as both an unlisted assembly and a denied
  namespace, the authentication system, this package under its `Virtuademy.SDK.Environments.*`
  names, the backend behind this facade, `Task` — and the two positives that had no test at all,
  that **this assembly is reachable** and that no denied prefix swallows it. Each asserts the
  denial rather than the refusal, because with the denials deleted the default deny still refuses
  and the weaker assertions could not tell the difference; verified by mutating `policy.json`, not
  by watching them pass.

  **Still open**: the end-to-end half. A real script, in a real project, rejected by a real
  publish — the test-env project's job, and the only thing that covers the editor-side verifier,
  the upload and the endpoint. And per ADR 0019 the enforced policy is `policy.json` **plus**
  operator deltas that can loosen it, so no unit test speaks for a given deployment.
- **`policy.json` allows the bare assembly name `HotUpdate`**, while `HotUpdateDllLocator`
  documents that Unity compiles the hot-update assembly as `HotUpdate_<productGUID>`. Whether that
  entry is dead depends on whether the engine matches an assembly's own name or only its
  references — unread so far.
- **The name is not frozen yet, and two documents disagree.** This assembly takes the name
  `policy.json` already ships, because that file is deployed and the plan's `§4`
  (`Virtuademy.HotUpdate.ScriptingApi`) is not. From the first published scripted world the name
  becomes a wire symbol and cannot change: a built bundle records components by assembly.
