// Nothing references this assembly: its only entry point is
// I2LocalizedTextProvider.RegisterAtRuntime. Without this attribute the
// player's managed code stripping drops the whole assembly, the provider is
// never registered and every LocalizedXxx element keeps its UXML text.
// The assembly compiles only when I2LOC is defined, so a project without
// I2Loc is not affected.
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]
