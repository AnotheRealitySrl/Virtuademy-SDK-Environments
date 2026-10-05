using System.Collections;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Base of the components that bind to elements of a <see cref="UIDocument"/>'s visual tree (click
    /// handlers, images, localization keys, toggles…). Works with any UIDocument, world-space or
    /// screen-space.
    ///
    /// It owns the binding lifecycle every such component needs, so a subclass only says what to do
    /// with the tree (<see cref="BindTo"/>) and how to let go of it (<see cref="UnbindFromTree"/>):
    /// <list type="bullet">
    /// <item>the <c>document</c> field, falling back to the first UIDocument on this object or its
    /// children;</item>
    /// <item>lazy binding: <c>rootVisualElement</c> is not always built during the first
    /// <c>OnEnable</c>, so binding is retried every frame for up to <see cref="MaxBindFrames"/>
    /// frames;</item>
    /// <item><see cref="Rebind"/> for <see cref="WorldSpaceUIDocumentRebuilder"/>, which replaces the
    /// whole tree;</item>
    /// <item>an optional detach watch (<see cref="WatchForRebuild"/>) that re-binds by itself when the
    /// tree is rebuilt by anything else.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIDocumentBinder : MonoBehaviour, IVisualTreeRebindable
    {
        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

        /// <summary>Maximum number of frames to wait for the UIDocument to build its visual tree.</summary>
        protected const int MaxBindFrames = 120;

        private Coroutine bindRoutine;
        private VisualElement watched;

        /// <summary>The bound document (resolved on the first bind attempt when left empty).</summary>
        protected UIDocument Document => document;

        /// <summary>Whether the component is currently bound to the document's tree.</summary>
        protected bool IsBound { get; private set; }

        protected virtual void OnEnable()
        {
            if (!TryBind())
            {
                ScheduleBind();
            }
        }

        protected virtual void OnDisable()
        {
            CancelScheduledBind();
            Unbind();
        }

        /// <summary>
        /// Drops whatever was bound to the old tree and binds against the document's current root,
        /// retrying on later frames if the tree is not built yet. Does nothing while the component is
        /// disabled: its own OnEnable binds.
        /// </summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            CancelScheduledBind();
            Unbind();
            if (!TryBind())
            {
                ScheduleBind();
            }
        }

        /// <summary>
        /// Binds to <paramref name="root"/>. Return false when the elements are not in the tree yet (or
        /// a name is wrong): the bind is retried on the next frame.
        /// </summary>
        protected abstract bool BindTo(VisualElement root);

        /// <summary>Releases whatever <see cref="BindTo"/> registered on the tree.</summary>
        protected abstract void UnbindFromTree();

        /// <summary>
        /// Re-binds automatically when <paramref name="element"/> detaches from its panel, i.e. when the
        /// tree is rebuilt. Call it from <see cref="BindTo"/> with one of the bound elements.
        /// </summary>
        protected void WatchForRebuild(VisualElement element)
        {
            if (watched == element)
            {
                return;
            }
            Unwatch();
            watched = element;
            watched?.RegisterCallback<DetachFromPanelEvent>(OnWatchedDetached);
        }

        private bool TryBind()
        {
            if (IsBound)
            {
                return true;
            }
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }
            if (document == null)
            {
                Debug.LogWarning($"[{GetType().Name}] No UIDocument found on '{name}'.", this);
                return false;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                // The document has not built its tree yet; the caller will retry.
                return false;
            }

            IsBound = BindTo(root);
            return IsBound;
        }

        private void Unbind()
        {
            UnbindFromTree();
            Unwatch();
            IsBound = false;
        }

        private void ScheduleBind()
        {
            bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void CancelScheduledBind()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
        }

        private IEnumerator BindWhenReady()
        {
            for (int frame = 0; frame < MaxBindFrames && !IsBound; frame++)
            {
                yield return null;
                if (TryBind())
                {
                    break;
                }
            }
            bindRoutine = null;
        }

        private void Unwatch()
        {
            if (watched != null)
            {
                watched.UnregisterCallback<DetachFromPanelEvent>(OnWatchedDetached);
                watched = null;
            }
        }

        private void OnWatchedDetached(DetachFromPanelEvent _)
        {
            Unbind();

            // The tree also detaches when this object is disabled or destroyed — not only on a rebuild.
            // During SetActive(false) the detach runs synchronously while isActiveAndEnabled can still
            // read true, yet StartCoroutine already sees the GameObject as inactive and errors. So gate
            // on gameObject.activeInHierarchy — the exact native flag StartCoroutine tests — plus the
            // component's own enabled flag.
            if (!enabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            // Extra guard for the destroy path: only a genuine rebuild keeps the root on a live panel.
            IPanel panel = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.panel
                : null;
            if (panel == null)
            {
                return;
            }

            CancelScheduledBind();
            ScheduleBind();
        }
    }
}
