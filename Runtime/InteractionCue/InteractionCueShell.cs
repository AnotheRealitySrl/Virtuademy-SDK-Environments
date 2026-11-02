using UnityEngine;

namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// Marks a renderer created by <see cref="InteractionCueDriver"/> to draw a cue's rim, so that no cue takes it for
    /// one of the object's own renderers. Carries no logic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class InteractionCueShell : MonoBehaviour
    {
    }
}
