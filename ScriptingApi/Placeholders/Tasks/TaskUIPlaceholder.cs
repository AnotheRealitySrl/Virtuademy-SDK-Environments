using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class TaskUIPlaceholder : SpawnableHandlerPlaceholder
    {
        /// <summary>Where the task panel stands in VR.</summary>
        public enum EVRPlacement
        {
            /// <summary>Where this placeholder is, in the world (as before 2026.7).</summary>
            Fixed = 0,

            /// <summary>
            /// With the player: beside them, low on the left, turning and moving with their body (not their head),
            /// so it stays at hand without covering the view.
            /// </summary>
            FollowPlayer = 1,
        }

        [Tooltip("VR only: the panel stays where this placeholder is (Fixed), or goes with the player, low on their left "
            + "(Follow Player). Set it before the scene is loaded; the panel is placed when it is created.")]
        public EVRPlacement vrPlacement = EVRPlacement.Fixed;

        [SerializeField] public GameObject taskSystem; //Reference to task system for callbacks (Forse non serve, ma ci collego gli eventi di callback)
        [SerializeField] public string descriptionKey;
        [SerializeField] public bool showIntroductionAtAwake = false;

        [HideInInspector] public UnityEvent<bool> onDisplayIntro;
        public void DisplayIntroduction(bool value)
        {
            onDisplayIntro?.Invoke(value);
        }
    }
}
