using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// Gives every <see cref="InteractionCuePlaceholder"/> its <see cref="InteractionCueDriver"/> when the placeholder
    /// wakes up, in the application and in a creator's play mode alike.
    /// </summary>
    internal static class InteractionCueRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Without a domain reload the static event keeps its handlers: never subscribe twice.
            InteractionCuePlaceholder.Created -= OnCueCreated;
            InteractionCuePlaceholder.Created += OnCueCreated;
        }

        private static void OnCueCreated(InteractionCuePlaceholder cue)
        {
            if (!cue.TryGetComponent(out InteractionCueDriver driver))
            {
                driver = cue.gameObject.AddComponent<InteractionCueDriver>();
            }
            driver.Bind(cue);
        }
    }
}
