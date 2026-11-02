using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Whether a panel's world document catches rays and clicks: only while the panel is shown. UI Toolkit gives a
    /// world document a collider sized to it and keeps it while the panel is hidden, so a hidden panel stayed in front of
    /// the objects behind it and took their rays and clicks (2026-11-02: the procedure's feedback panels in VR, a
    /// choice's confirmation on web). In VR with an <see cref="OverlayPanelPlacement"/> this sets its
    /// <see cref="OverlayPanelPlacement.BlocksRay"/>; on any other world document it switches the document's colliders,
    /// again after each layout of the tree (UI Toolkit makes the collider then). Screen panels have no collider.
    /// Nothing runs per frame.
    /// </summary>
    internal sealed class PanelRayBlocking
    {
        private readonly Component owner;
        private readonly List<Collider> colliders = new();
        private VisualElement root;
        private bool on = true;

        public PanelRayBlocking(Component owner)
        {
            this.owner = owner;
        }

        /// <summary>Follows the tree's layout, which (re)makes the document's collider.</summary>
        public void Bind(VisualElement treeRoot)
        {
            Unbind();
            root = treeRoot;
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Apply();
            root.schedule.Execute(Apply);
        }

        public void Unbind()
        {
            root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root = null;
        }

        /// <summary>Shown (with something to press): catches rays and clicks. Hidden: lets them through.</summary>
        public void Set(bool blocks)
        {
            on = blocks;
            Apply();
            root?.schedule.Execute(Apply);
        }

        // UI Toolkit makes the collider in the frame after the layout (and a new one each time the object is enabled):
        // applied again on the root's scheduler, which runs after it (see OverlayPanelPlacement.ApplyCollidersOnceMade)
        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            Apply();
            root?.schedule.Execute(Apply);
        }

        private void Apply()
        {
            if (owner == null)
            {
                return;
            }
            if (owner.TryGetComponent(out OverlayPanelPlacement placement) && placement.IsWorldSpace)
            {
                placement.BlocksRay = on;
                return;
            }
            if (!owner.TryGetComponent(out UIDocument document) || document.panelSettings == null
                || document.panelSettings.renderMode != PanelRenderMode.WorldSpace)
            {
                return;
            }
            owner.GetComponents(colliders);
            foreach (Collider c in colliders)
            {
                if (c.enabled != on)
                {
                    c.enabled = on;
                }
            }
        }
    }
}
