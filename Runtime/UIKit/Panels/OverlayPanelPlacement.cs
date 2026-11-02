using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Puts an overlay panel (HUD, toasts, popups) where each platform can see it: drawn over the
    /// screen on desktop, WebGL and mobile, and in VR a world-space panel in front of the user that
    /// goes with them, the way the application's own VR task panel does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document is authored with the screen PanelSettings; in VR this component swaps in the
    /// world PanelSettings, a fixed size and the UI layer when it is enabled. The tree survives the
    /// swap: the SDK binders re-bind by themselves when it is rebuilt, and so does this component
    /// (<see cref="IVisualTreeRebindable"/>).
    /// </para>
    /// <para>
    /// In VR the panel is placed in front of the head when it is enabled and on <see cref="RecentreNow"/>,
    /// then a <see cref="ParentConstraint"/> on the player's rig carries it with their body (locomotion,
    /// teleport, snap turn), not with their head, so reading it never needs a steady head. Nothing of ours
    /// runs per frame (2026-10-29, review B2): the root's size, the colliders and the help state are applied
    /// on the events that change them.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Overlay Panel Placement")]
    public class OverlayPanelPlacement : MonoBehaviour, IVisualTreeRebindable
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

        [SerializeField, Tooltip("In VR, go with the player's body (locomotion, teleport, turns) after being placed " +
            "in front of them. Off: placed once in front of the user when enabled.")]
        private bool vrFollowHead = true;

        private UIDocument document;
        private VisualElement root;
        private bool vr;
        private bool blocksRay = true;
        private bool helpSubscribed;

        /// <summary>Whether the panel is in its VR (world-space) form.</summary>
        public bool IsWorldSpace => vr;

        /// <summary>
        /// In VR, whether the panel's colliders are on, so the controller ray can hit it. Its owner
        /// turns it on only while the panel shows something to press (an open popup, the HUD's hint
        /// button): an overlay that floats in front of the user would otherwise catch the ray meant
        /// for the application's own panels behind it (the tutorial, the tablet), whose buttons the
        /// ray reaches through colliders too. On by default for panels whose owner does not say.
        /// </summary>
        public bool BlocksRay
        {
            get => blocksRay;
            set
            {
                if (blocksRay == value)
                {
                    return;
                }
                blocksRay = value;
                ApplyColliders();
            }
        }

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
                Rebind();
                if (vrHideWhileHelpOpen)
                {
                    helpSubscribed = PanelEnvironment.SubscribeHelp(OnHelpChanged, OnHelpChanged);
                }
                OnHelpChanged();
                Recentre();
            }
            else if (screenPanelSettings != null && document.panelSettings != screenPanelSettings)
            {
                document.panelSettings = screenPanelSettings;
            }
        }

        private void OnDisable()
        {
            if (helpSubscribed)
            {
                PanelEnvironment.UnsubscribeHelp(OnHelpChanged, OnHelpChanged);
                helpSubscribed = false;
            }
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root = null;
        }

        /// <summary>The tree was rebuilt (panel swap, WorldSpaceUIDocumentRebuilder): its root sized again.</summary>
        public void Rebind()
        {
            if (!vr || !isActiveAndEnabled)
            {
                return;
            }
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return;
            }
            // Laid out (or re-laid out): UI Toolkit creates and resizes the document's collider then
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            ApplyRootSize();
            ApplyHelpVisibility();
            ApplyColliders();
            ApplyCollidersOnceMade();
        }

        private void OnRootGeometryChanged(GeometryChangedEvent e)
        {
            ApplyRootSize();
            ApplyColliders();
            ApplyCollidersOnceMade();
        }

        /// <summary>
        /// UI Toolkit makes the document's collider, switched on, in the frame after the tree is laid out, and makes a
        /// new one each time the object is enabled again: when the layout event (or <see cref="Rebind"/>) runs there is
        /// none yet to switch. Measured on 6000.3.21f1 (2026-11-02): the notification host kept a collider on 1.4 m in
        /// front of the player with no popup open, and took the VR ray. So the switch runs once more on the root's
        /// scheduler, which runs after UI Toolkit has made it (one item per layout, nothing per frame).
        /// </summary>
        private void ApplyCollidersOnceMade()
        {
            root?.schedule.Execute(ApplyColliders);
        }

        private void OnHelpChanged()
        {
            hiddenForHelp = vrHideWhileHelpOpen && PanelEnvironment.IsHelpOpen;
            ApplyHelpVisibility();
            ApplyColliders();
        }

        /// <summary>Brings the panel in front of the user now, in VR. Does nothing on a screen.</summary>
        public void RecentreNow()
        {
            if (vr)
            {
                Recentre();
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
        /// PanelSettings use MatchBoundingBox) when the tree is laid out: switched then, when the
        /// owner changes <see cref="BlocksRay"/> and when the help opens or closes.
        /// </remarks>
        private void ApplyColliders()
        {
            if (!vr)
            {
                return;
            }
            bool on = BlocksRay && !hiddenForHelp;
            GetComponents(colliders);
            foreach (Collider c in colliders)
            {
                if (c.enabled != on)
                {
                    c.enabled = on;
                }
                if (c.enabled != on)
                {
                    // Platform errors fail loudly: a collider that will not switch off would take the ray
                    Debug.LogError($"[{nameof(OverlayPanelPlacement)}] '{name}': its collider {c.GetType().Name} did not switch " +
                        (on ? "on" : "off") + ".", this);
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

        // In front of the head, then (vrFollowHead) carried by the player's rig with a ParentConstraint: the engine
        // moves it with the body, no code of ours runs per frame
        private void Recentre()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[{nameof(OverlayPanelPlacement)}] '{name}': no main camera to place the VR panel in front of", this);
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

            ParentConstraint follow = GetComponent<ParentConstraint>();
            if (follow != null)
            {
                follow.constraintActive = false;
            }
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            Transform body = head.root;
            if (!vrFollowHead || body == head || body == transform.root)
            {
                return;
            }
            follow = follow != null ? follow : gameObject.AddComponent<ParentConstraint>();
            while (follow.sourceCount > 0)
            {
                follow.RemoveSource(0);
            }
            follow.AddSource(new ConstraintSource { sourceTransform = body, weight = 1f });
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            follow.SetTranslationOffset(0, inverse * (targetPosition - body.position));
            follow.SetRotationOffset(0, (inverse * targetRotation).eulerAngles);
            follow.translationAtRest = transform.localPosition;
            follow.rotationAtRest = transform.localEulerAngles;
            follow.locked = true;
            follow.constraintActive = true;
        }
    }
}
