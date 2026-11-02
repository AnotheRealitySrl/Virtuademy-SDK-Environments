using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Keeps a <see cref="UIDocument"/> in the tenant's colours: applies the palette the application
    /// publishes (<see cref="VirtuademyThemeRuntime"/>) when the tree is built, when it is rebuilt and
    /// whenever the palette changes.
    /// </summary>
    /// <remarks>
    /// Every SDK panel prefab carries one. Add it to a panel of your own built on the SDK classes
    /// (<c>vd-panel</c>, <c>vd-button--primary</c>, <c>vd-bg-action</c>…) for it to follow the
    /// tenant too; without it the panel keeps the SDK colours, which is also what every panel shows
    /// when the application publishes nothing.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Themed Document")]
    public class ThemedDocument : UIDocumentBinder
    {
        private VisualElement boundRoot;

        protected override bool BindTo(VisualElement root)
        {
            boundRoot = root;
            ThemeApplier.Apply(root);
            VirtuademyThemeRuntime.Changed += Reapply;
            WatchForRebuild(root.childCount > 0 ? root[0] : root);
            return true;
        }

        protected override void UnbindFromTree()
        {
            VirtuademyThemeRuntime.Changed -= Reapply;
            boundRoot = null;
        }

        /// <summary>
        /// Applies the palette again. Call it after adding elements to the tree from code; the SDK's
        /// own components (toast, popup, list) do.
        /// </summary>
        public void Reapply()
        {
            if (boundRoot != null)
            {
                ThemeApplier.Apply(boundRoot);
            }
        }
    }
}
