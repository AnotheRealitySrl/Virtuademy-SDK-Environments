using System;

using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.VisualScripting;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Forces a world-space <see cref="UIDocument"/> to rebuild its visual tree once the platform
    /// scene setup has completed.
    ///
    /// When a panel prefab is streamed in through Addressables, the <see cref="UIDocument"/> can
    /// attach to its panel before the source asset has been applied, which leaves the panel empty:
    /// the quad is there (correct layer, correct <c>PanelSettings</c>) but nothing is drawn. In the
    /// editor this is what re-dragging the UXML into the <c>Source Asset</c> field fixes, because the
    /// setter calls the document's internal <c>RecreateUI()</c>. This component does the same thing
    /// at runtime, driven by the platform's own lifecycle rather than by hand.
    ///
    /// The trigger is the Visual Scripting event <c>OnSceneSetupCompletedEvent</c> that
    /// <c>AppManager</c> broadcasts on the <see cref="EventBus"/> once the scene is ready — the same
    /// event the "Virtuademy Scene: On Setup Completed" node listens to — so no reference to the
    /// application assembly is needed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class WorldSpaceUIDocumentRebuilder : MonoBehaviour
    {
        [SerializeField, Tooltip("Document to rebuild. If empty, the UIDocument on this object is used.")]
        private UIDocument document;

        [SerializeField, Tooltip("Also rebuild once shortly after this component is enabled. Safety " +
            "net for panels instantiated AFTER scene setup completed, which would otherwise miss the " +
            "one-shot event. The rebuild is idempotent, so running it twice is harmless.")]
        private bool rebuildOnEnable = true;

        private readonly EventHook hook = new EventHook(OnSceneSetupCompletedEventNode.EventName);
        private Action<string> handler;

        private void Awake()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }
        }

        private void OnEnable()
        {
            handler = _ => Rebuild();
            EventBus.Register(hook, handler);

            if (rebuildOnEnable)
            {
                // Next frame: the document has had a chance to attach so RecreateUI() rebuilds a real
                // tree, and a late-instantiated panel that already missed the event still gets fixed.
                Invoke(nameof(Rebuild), 0f);
            }
        }

        private void OnDisable()
        {
            if (handler != null)
            {
                EventBus.Unregister(hook, handler);
                handler = null;
            }
            CancelInvoke(nameof(Rebuild));
        }

        /// <summary>
        /// Rebuilds the document's visual tree and re-binds every <see cref="IVisualTreeRebindable"/>
        /// on this object. Public so it can also be wired to a UnityEvent or invoked from a Visual
        /// Scripting graph.
        /// </summary>
        public void Rebuild()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }
            if (document == null)
            {
                Debug.LogWarning($"[{nameof(WorldSpaceUIDocumentRebuilder)}] No UIDocument on '{name}'.", this);
                return;
            }

            // Re-assigning the source always calls UIDocument.RecreateUI() internally — the runtime
            // equivalent of re-dragging the UXML onto the Source Asset field in the inspector.
            VisualTreeAsset source = document.visualTreeAsset;
            if (source != null)
            {
                document.visualTreeAsset = source;
            }

            // The tree is now a fresh instance holding only what the UXML authored: click handlers,
            // localization keys and images written onto the old elements are gone with them.
            foreach (IVisualTreeRebindable rebindable in GetComponents<IVisualTreeRebindable>())
            {
                rebindable.Rebind();
            }
        }
    }
}
