using System.Runtime.CompilerServices;

// The application installs the implementation and nothing else can, because
// IVirtuademyGameplay.Install is internal to this assembly. An interpreted script references this
// assembly in full, so a public installer would let one script replace the surface every other
// script calls.
//
// The named assembly lives in Assets/_Project, not in this package: the implementations of these
// contracts belong to the application, along with SM and the world systems, while the surface ships
// to creators. A creator installs this surface; they do not install what answers it.
[assembly: InternalsVisibleTo("Virtuademy.Worlds.ScriptingApiBackend")]

// The application's interaction core raises the interaction events of InteractablePlaceholder
// (IInteractableEventSink). Internal so that a script cannot raise them.
[assembly: InternalsVisibleTo("Virtuademy.Worlds.Interaction")]

// The SDK runtime draws and binds the placeholders whose logic is not the application's (the interaction cue): it
// subscribes to their internal hooks (a "created" event, a state-changed event). Internal so that a script sees none
// of them; a script still reaches these placeholders through their public members only.
[assembly: InternalsVisibleTo("Virtuademy.SDK.Environments")]
