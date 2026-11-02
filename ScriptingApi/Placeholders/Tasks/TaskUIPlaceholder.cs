using System;
using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class TaskUIPlaceholder : SpawnableHandlerPlaceholder
    {
        /// <summary>Where the task panel stands, chosen for each platform.</summary>
        public enum ETaskPanelPlacement
        {
            /// <summary>In the world, where this placeholder is, at the size below and the placeholder's scale.</summary>
            InWorld = 0,

            /// <summary>
            /// With the player: on the screen on desktop and mobile; in VR beside them, low on the left, turning and moving
            /// with their body (not their head).
            /// </summary>
            WithPlayer = 1,

            /// <summary>No panel on this platform: the tasks run without one (a procedure's events, or the environment's
            /// own panels, can show them).</summary>
            Hidden = 2,
        }

        [Header("Panel placement")]
        [Tooltip("Desktop and web: the panel on the screen (With Player), in the world, where this placeholder is, or none "
            + "(Hidden).")]
        public ETaskPanelPlacement desktopPlacement = ETaskPanelPlacement.WithPlayer;

        [Tooltip("Mobile: the panel on the screen (With Player), in the world, where this placeholder is, or none (Hidden).")]
        public ETaskPanelPlacement mobilePlacement = ETaskPanelPlacement.WithPlayer;

        [Tooltip("VR: the panel in the world, where this placeholder is, with the player, on their left beside the view, "
            + "or none (Hidden).")]
        public ETaskPanelPlacement vrPlacement = ETaskPanelPlacement.InWorld;

        [Tooltip("In the world: the panel's width and height, in metres, multiplied by this placeholder's scale on each "
            + "axis. The scale sizes the panel, it does not stretch its content: text and controls keep their size and "
            + "lay out in the area. The panel is centred on the placeholder and read from the side its forward axis "
            + "(blue) points away from. Select the placeholder to see it in the Scene view.")]
        public Vector2 worldPanelSize = new Vector2(0.61f, 0.69f);

        [SerializeField] public GameObject taskSystem; //Reference to task system for callbacks (Forse non serve, ma ci collego gli eventi di callback)
        [SerializeField] public string descriptionKey;
        [SerializeField] public bool showIntroductionAtAwake = false;

        [HideInInspector] public UnityEvent<bool> onDisplayIntro;
        public void DisplayIntroduction(bool value)
        {
            onDisplayIntro?.Invoke(value);
        }

        // The order of the requests, across placeholders: the latest wins
        private static int lastPersonalPanelRequest;

        /// <summary>
        /// When <see cref="ShowAsPersonalPanel"/> was last called on this placeholder, as an order among every
        /// placeholder's calls (0: never). The application reads it when it creates the panel, so a call made before
        /// (on Start, say) is not lost.
        /// </summary>
        public int PersonalPanelRequest { get; private set; }

        /// <summary>Raised by <see cref="ShowAsPersonalPanel"/>; the application switches the panel on it.</summary>
        public event Action PersonalPanelRequested;

        /// <summary>
        /// Makes this placeholder's panel the one shown with the player. Only one panel with the player (With Player: on
        /// the screen on desktop and mobile, beside the player in VR) is shown at a time: the one the environment asked
        /// for last, else the first one created. The panel shown until then is hidden, not destroyed: it keeps its tasks
        /// and its state. Panels in the world are not affected. Callable from Visual Scripting, also before the panel
        /// exists: the request is kept and applied when it is created.
        /// </summary>
        public void ShowAsPersonalPanel()
        {
            PersonalPanelRequest = ++lastPersonalPanelRequest;
            PersonalPanelRequested?.Invoke();
        }

        /// <summary>Whether any platform shows this panel in the world.</summary>
        public bool AnyInWorld => desktopPlacement == ETaskPanelPlacement.InWorld || mobilePlacement == ETaskPanelPlacement.InWorld
            || vrPlacement == ETaskPanelPlacement.InWorld;

#if UNITY_EDITOR
        // The world panel's preview in the Scene view (2026.7): its outline at the size and scale the application
        // gives it, the card's header strip at the top, and the side it is read from. Editor only.
        private void OnDrawGizmos()
        {
            if (!AnyInWorld)
                return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 size = new Vector3(Mathf.Max(0.01f, worldPanelSize.x), Mathf.Max(0.01f, worldPanelSize.y), 0f);
            Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(0.23f, 0.5f, 1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            // The header strip: 6 cm in the world, whatever the scale (the content is not stretched)
            float header = Mathf.Min(size.y, 0.06f / Mathf.Max(1e-5f, Mathf.Abs(transform.lossyScale.y)));
            Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
            Gizmos.DrawCube(new Vector3(0f, size.y * 0.5f - header * 0.5f, 0f), new Vector3(size.x, header, 0f));
            // Read from the -forward side: an arrow from the reader towards the panel
            Gizmos.color = new Color(0.23f, 0.5f, 1f, 1f);
            Gizmos.DrawLine(new Vector3(0f, 0f, -size.x * 0.4f), Vector3.zero);
            Gizmos.matrix = previous;
        }
#endif
    }
}
