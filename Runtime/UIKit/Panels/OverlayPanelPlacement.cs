using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Puts an overlay panel (HUD, toasts, popups) where each platform can see it: drawn over the
    /// screen on desktop, WebGL and mobile, and in VR a world-space panel that floats in front of the
    /// user and follows the head lazily, the way the application's own VR panels do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document is authored with the screen PanelSettings; in VR this component swaps in the
    /// world PanelSettings, a fixed size and the UI layer when it is enabled. The tree survives the
    /// swap: the SDK binders re-bind by themselves when it is rebuilt.
    /// </para>
    /// <para>
    /// The follow keeps the panel where it is while the head turns less than
    /// <see cref="followAngle"/> away from it, then glides it back in front, so reading it never
    /// needs a steady head and a glance aside does not drag it along.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Overlay Panel Placement")]
    public class OverlayPanelPlacement : MonoBehaviour
    {
        private const int UILayer = 5;

        [SerializeField, Tooltip("PanelSettings used on desktop, WebGL and mobile. Empty: keep the document's own.")]
        private PanelSettings screenPanelSettings;

        [SerializeField, Tooltip("World-space PanelSettings used in VR. Empty: the panel stays a screen " +
            "panel, which a headset does not show.")]
        private PanelSettings worldPanelSettings;

        [SerializeField, Tooltip("Size of the panel in VR, in panel pixels (the SDK world PanelSettings " +
            "draw one pixel per millimetre).")]
        private Vector2 vrSize = new(1600f, 1000f);

        [SerializeField, Tooltip("Distance from the eyes in VR, in metres.")]
        private float vrDistance = 1.6f;

        [SerializeField, Tooltip("Height of the panel centre relative to the eyes in VR, in metres.")]
        private float vrHeightOffset = -0.15f;

        [SerializeField, Tooltip("Sideways offset of the panel centre in VR, in metres (positive: to the right). " +
            "Keeps a HUD beside the popups instead of behind them.")]
        private float vrSideOffset;

        [SerializeField, Tooltip("In VR, follow the head. Off: placed once in front of the user when enabled.")]
        private bool vrFollowHead = true;

        [SerializeField, Range(5f, 90f), Tooltip("How far, in degrees, the head may turn before the panel follows.")]
        private float followAngle = 30f;

        [SerializeField, Tooltip("How fast the panel glides back in front, per second.")]
        private float followSpeed = 3f;

        private UIDocument document;
        private bool vr;
        private bool recentring;

        /// <summary>Whether the panel is in its VR (world-space) form.</summary>
        public bool IsWorldSpace => vr;

        /// <summary>
        /// In VR, whether the panel's colliders are on, so the controller ray can hit it. Its owner
        /// turns it on only while the panel shows something to press (an open popup, the HUD's hint
        /// button): an overlay that floats in front of the user would otherwise catch the ray meant
        /// for the application's own panels behind it (the tutorial, the tablet), whose buttons the
        /// ray reaches through colliders too. On by default for panels whose owner does not say.
        /// </summary>
        public bool BlocksRay { get; set; } = true;

        [SerializeField, Tooltip("In VR, hide the panel (and let the ray through) while the application's " +
            "help panel is open: that panel is where the user must point, and it keeps them from moving " +
            "until it closes.")]
        private bool vrHideWhileHelpOpen = true;

        private bool hiddenForHelp;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            vr = PanelEnvironment.IsVR && worldPanelSettings != null;
            if (vr)
            {
                gameObject.layer = UILayer;
                document.panelSettings = worldPanelSettings;
                document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
                document.worldSpaceSize = vrSize;
                document.pivot = Pivot.Center;
                transform.localScale = Vector3.one;
                ApplyRootSize();
                Recentre(true);
            }
            else if (screenPanelSettings != null && document.panelSettings != screenPanelSettings)
            {
                document.panelSettings = screenPanelSettings;
            }
        }

        private void LateUpdate()
        {
            if (!vr)
            {
                return;
            }
            // The tree may be rebuilt (panel swap, WorldSpaceUIDocumentRebuilder): keep its root sized.
            ApplyRootSize();
            hiddenForHelp = vrHideWhileHelpOpen && PanelEnvironment.IsHelpOpen;
            ApplyHelpVisibility();
            ApplyColliders();
            if (vrFollowHead)
            {
                Recentre(false);
            }
        }

        /// <summary>Brings the panel in front of the user now, in VR. Does nothing on a screen.</summary>
        public void RecentreNow()
        {
            if (vr)
            {
                Recentre(true);
            }
        }

        /// <summary>
        /// Gives the document's root the VR size in panel pixels. The SDK templates lay their content
        /// out in absolutely positioned layers that fill the root, so a root sized by its content (what
        /// a document gets in world space when the fixed size does not reach it) is 0×0, and every
        /// text wraps to one letter per line. Setting the size on the root itself does not depend on
        /// that.
        /// </summary>
        private void ApplyRootSize()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return;
            }
            if (root.style.width.value.value != vrSize.x || root.style.height.value.value != vrSize.y)
            {
                root.style.width = vrSize.x;
                root.style.height = vrSize.y;
            }
        }

        /// <remarks>
        /// The document's trigger collider is created and resized by UI Toolkit (the SDK world
        /// PanelSettings use MatchBoundingBox), so it is switched every frame rather than once: a
        /// collider created later is caught too.
        /// </remarks>
        private void ApplyColliders()
        {
            bool on = BlocksRay && !hiddenForHelp;
            GetComponents(colliders);
            foreach (Collider c in colliders)
            {
                if (c.enabled != on)
                {
                    c.enabled = on;
                }
            }
        }

        /// <summary>
        /// Hides the whole tree while the help panel is open. <c>display: none</c> on the root, not
        /// on the owner's elements, so it does not fight the owner's own show and hide; the content
        /// (an open popup, the HUD's count) is all there again when the help closes.
        /// </summary>
        private void ApplyHelpVisibility()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return;
            }
            DisplayStyle wanted = hiddenForHelp ? DisplayStyle.None : DisplayStyle.Flex;
            if (root.style.display != wanted)
            {
                root.style.display = wanted;
            }
        }

        private readonly System.Collections.Generic.List<Collider> colliders = new();

        private void Recentre(bool snap)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            Transform head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 targetPosition = head.position + forward * vrDistance + Vector3.up * vrHeightOffset
                                     + right * vrSideOffset;
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);

            if (snap)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                recentring = false;
                return;
            }

            Vector3 toPanel = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
            float angle = toPanel.sqrMagnitude < 0.0001f ? 180f : Vector3.Angle(forward, toPanel);
            if (angle > followAngle)
            {
                recentring = true;
            }
            if (!recentring)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, t),
                Quaternion.Slerp(transform.rotation, targetRotation, t));
            if ((transform.position - targetPosition).sqrMagnitude < 0.0004f)
            {
                recentring = false;
            }
        }
    }
}
