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
        }

        [Header("Panel placement")]
        [Tooltip("Desktop and web: the panel on the screen (With Player) or in the world, where this placeholder is.")]
        public ETaskPanelPlacement desktopPlacement = ETaskPanelPlacement.WithPlayer;

        [Tooltip("Mobile: the panel on the screen (With Player) or in the world, where this placeholder is.")]
        public ETaskPanelPlacement mobilePlacement = ETaskPanelPlacement.WithPlayer;

        [Tooltip("VR: the panel in the world, where this placeholder is, or with the player, low on their left.")]
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
