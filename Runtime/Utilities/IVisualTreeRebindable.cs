namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// A component that binds to elements of a <see cref="UnityEngine.UIElements.UIDocument"/>'s visual
    /// tree and must bind again when that tree is replaced.
    ///
    /// <see cref="WorldSpaceUIDocumentRebuilder"/> recreates the whole tree from the UXML, so every
    /// element a sibling component cached or wrote to (a click handler, a localization key, a
    /// background image) is gone, and the fresh elements carry only what the UXML authored. After the
    /// rebuild the rebuilder calls <see cref="Rebind"/> on every implementation on its GameObject.
    /// </summary>
    public interface IVisualTreeRebindable
    {
        /// <summary>
        /// Drops whatever was bound to the old tree and binds against the document's current root,
        /// retrying on later frames if the tree is not built yet. Does nothing while the component is
        /// disabled: its own OnEnable binds.
        /// </summary>
        void Rebind();
    }
}
