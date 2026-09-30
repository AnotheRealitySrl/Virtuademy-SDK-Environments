using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Assigns images to UI Toolkit elements of a world-space panel, looked up by their UXML
    /// <c>name</c> — the same model as <see cref="WorldSpaceButtonBinder"/> (clicks by name) and
    /// <c>LocalizedUIBinder</c> (localization keys by name). Each entry pairs an element name with a
    /// <see cref="Sprite"/>, which is pushed into that element's <c>background-image</c> at runtime, so
    /// the shared UXML/USS is never touched and the same panel asset can show different images per
    /// instance. This is the UI Toolkit equivalent of "swap the sprite reference on the instance" in
    /// uGUI.
    ///
    /// Because the lookup is <c>root.Q(name)</c> (the first match), give each target element a UNIQUE
    /// name in the UXML — e.g. rename the four ButtonChoicePanel thumbnails <c>thumb-0 … thumb-3</c> and
    /// add one entry per name to show four different images.
    ///
    /// Empty (null) sprites are skipped, so the element keeps whatever the USS authored. Re-applies
    /// automatically if the panel is rebuilt at runtime (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), which replaces the visual tree.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpacePanelImageBinder : MonoBehaviour, IVisualTreeRebindable
    {
        [Serializable]
        public class ImageBinding
        {
            [Tooltip("The 'name' of the element in the UXML to assign the image to.")]
            public string elementName = "thumb";

            [Tooltip("The image assigned to that element's background-image. Import it as Sprite (2D and UI).")]
            public Sprite image;
        }

        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

        [SerializeField, Tooltip("One entry per element you want to set an image on. Reference each " +
            "element by the 'name' it has in the UXML (names must be unique).")]
        private List<ImageBinding> images = new() { new ImageBinding() };

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxApplyFrames = 120;

        private Coroutine applyRoutine;
        private VisualElement watched;
        private bool applied;

        /// <summary>The image bindings, in order.</summary>
        public IReadOnlyList<ImageBinding> Images => images;

        private void OnEnable()
        {
            if (TryApply())
            {
                return;
            }
            // rootVisualElement is not always built during OnEnable on the first frame; keep trying.
            applyRoutine = StartCoroutine(ApplyWhenReady());
        }

        private void OnDisable()
        {
            if (applyRoutine != null)
            {
                StopCoroutine(applyRoutine);
                applyRoutine = null;
            }
            Unwatch();
            applied = false;
        }

        /// <summary>
        /// Sets (or adds) the image for a named element and pushes it to the panel immediately. Handy
        /// from a UnityEvent or a Visual Scripting graph when the content changes at runtime.
        /// </summary>
        public void SetImage(string elementName, Sprite image)
        {
            if (string.IsNullOrEmpty(elementName))
            {
                return;
            }
            ImageBinding binding = images.Find(b => b != null && b.elementName == elementName);
            if (binding == null)
            {
                binding = new ImageBinding { elementName = elementName };
                images.Add(binding);
            }
            binding.image = image;
            Apply();
        }

        /// <summary>Forces a re-apply of every binding against the current visual tree.</summary>
        public void Apply()
        {
            applied = false;
            if (!TryApply() && isActiveAndEnabled)
            {
                if (applyRoutine != null)
                {
                    StopCoroutine(applyRoutine);
                }
                applyRoutine = StartCoroutine(ApplyWhenReady());
            }
        }

        /// <summary>
        /// Stops watching the old tree and re-applies against the current one. The detach watch
        /// already covers a rebuild; this makes it explicit for <see cref="WorldSpaceUIDocumentRebuilder"/>.
        /// </summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            Unwatch();
            Apply();
        }

        private IEnumerator ApplyWhenReady()
        {
            for (int frame = 0; frame < MaxApplyFrames && !applied; frame++)
            {
                yield return null;
                if (TryApply())
                {
                    break;
                }
            }
            applyRoutine = null;
        }

        private bool TryApply()
        {
            if (applied)
            {
                return true;
            }
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }
            if (document == null)
            {
                Debug.LogWarning($"[{nameof(WorldSpacePanelImageBinder)}] No UIDocument found on '{name}'.", this);
                return false;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                // The document has not built its tree yet; the caller will retry.
                return false;
            }

            VisualElement firstBound = null;
            foreach (ImageBinding binding in images)
            {
                if (binding == null || string.IsNullOrEmpty(binding.elementName))
                {
                    continue;
                }
                VisualElement element = root.Q<VisualElement>(binding.elementName);
                if (element == null)
                {
                    Debug.LogWarning($"[{nameof(WorldSpacePanelImageBinder)}] Element " +
                        $"'{binding.elementName}' not found in the document on '{name}'.", this);
                    continue;
                }
                firstBound ??= element;
                if (binding.image != null)
                {
                    element.style.backgroundImage = new StyleBackground(binding.image);
                }
            }

            if (firstBound == null)
            {
                // Tree exists but none of the named elements are in yet; retry.
                return false;
            }

            // Re-apply automatically if the tree gets rebuilt (the elements detach from the panel).
            Watch(firstBound);

            applied = true;
            return true;
        }

        private void Watch(VisualElement element)
        {
            if (watched == element)
            {
                return;
            }
            Unwatch();
            watched = element;
            watched.RegisterCallback<DetachFromPanelEvent>(OnElementDetached);
        }

        private void Unwatch()
        {
            if (watched != null)
            {
                watched.UnregisterCallback<DetachFromPanelEvent>(OnElementDetached);
                watched = null;
            }
        }

        private void OnElementDetached(DetachFromPanelEvent _)
        {
            Unwatch();
            applied = false;

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

            if (applyRoutine != null)
            {
                StopCoroutine(applyRoutine);
            }
            applyRoutine = StartCoroutine(ApplyWhenReady());
        }
    }
}
