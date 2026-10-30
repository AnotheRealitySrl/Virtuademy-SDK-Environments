using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Reach Zone element: this client's player entering the trigger completes the task, Right.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ReachZoneElementDriver : ElementDriver
    {
        protected override void OnBind()
        {
            bool trigger = false;
            foreach (Collider c in GetComponents<Collider>())
            {
                trigger |= c.isTrigger;
            }
            if (!trigger)
            {
                Debug.LogWarning("[ReachZoneElement] " + name + " has no trigger collider: the player cannot enter it.", this);
            }
        }

        // A physics callback, not a per-frame check.
        private void OnTriggerEnter(Collider other)
        {
            if (Element == null || !IsLocalPlayer(other))
            {
                return;
            }
            Report(TaskResult.Right(gameObject), true);
        }

        // The local player's rig (each client reports for its own player). Without the application (a creator's play
        // mode), a collider tagged Player.
        private static bool IsLocalPlayer(Collider other)
        {
            if (IVirtuademyGameplay.IsAvailable && IVirtuademyGameplay.Current.Player?.Root != null)
            {
                return other.transform.IsChildOf(IVirtuademyGameplay.Current.Player.Root);
            }
            return other.CompareTag("Player");
        }
    }
}
