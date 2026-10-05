using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Turns the image element of a world-space panel into a small carousel with previous/next arrows.
    ///
    /// The FIRST image is still the one assigned through <see cref="WorldSpacePanelImageBinder"/> for
    /// the same element (so existing panels keep working untouched); the images in
    /// <c>extraImages</c> follow it. With a single image the arrows stay hidden
    /// (<c>display: none</c>), so the panel looks exactly like a plain image panel.
    ///
    /// Clicking an arrow pushes the next/previous sprite into the element's <c>background-image</c>.
    /// Binds lazily and re-binds if the visual tree is rebuilt (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), starting again from the first image.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpacePanelImageCarousel : MonoBehaviour, IVisualTreeRebindable
    {
        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

        [SerializeField, Tooltip("Name of the image element in the UXML. Its first image is the one set " +
            "for this name on the WorldSpacePanelImageBinder.")]
        private string mediaName = "media";

        [SerializeField, Tooltip("Name of the 'previous' arrow button in the UXML.")]
        private string previousName = "media-prev";

        [SerializeField, Tooltip("Name of the 'next' arrow button in the UXML.")]
        private string nextName = "media-next";

        [SerializeField, Tooltip("Images shown after the first one (the WorldSpacePanelImageBinder image). " +
            "Import them as Sprite (2D and UI).")]
        private List<Sprite> extraImages = new();

        [SerializeField, Tooltip("If true, 'next' on the last image goes back to the first (and vice versa). " +
            "If false, the arrow at the end is hidden.")]
        private bool loop = true;

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxBindFrames = 120;

        private readonly List<Sprite> sequence = new();
        private Coroutine bindRoutine;
        private VisualElement media;
        private Button previousButton;
        private Button nextButton;
        private int index;
        private bool bound;

        /// <summary>Number of images in the carousel (first image included).</summary>
        public int Count
        {
            get
            {
                BuildSequence();
                return sequence.Count;
            }
        }

        /// <summary>Index of the image currently shown.</summary>
        public int CurrentIndex => index;

        private void OnEnable()
        {
            if (!TryBind())
            {
                // rootVisualElement is not always built during OnEnable on the first frame; keep trying.
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        private void OnDisable()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            Unbind();
        }

        /// <summary>Drops the old tree's elements and binds the current one, back on the first image.</summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            Unbind();
            if (!TryBind())
            {
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        /// <summary>Shows the next image (wraps around when looping).</summary>
        public void Next()
        {
            ShowImage(loop ? (index + 1) % Mathf.Max(sequence.Count, 1) : index + 1);
        }

        /// <summary>Shows the previous image (wraps around when looping).</summary>
        public void Previous()
        {
            ShowImage(loop ? (index - 1 + sequence.Count) % Mathf.Max(sequence.Count, 1) : index - 1);
        }

        /// <summary>Shows the image at the given index (clamped to the available images).</summary>
        public void ShowImage(int imageIndex)
        {
            if (sequence.Count == 0)
            {
                return;
            }
            index = Mathf.Clamp(imageIndex, 0, sequence.Count - 1);
            if (media != null)
            {
                media.style.backgroundImage = new StyleBackground(sequence[index]);
            }
            RefreshArrows();
        }

        private void BuildSequence()
        {
            sequence.Clear();
            if (TryGetComponent(out WorldSpacePanelImageBinder binder))
            {
                foreach (WorldSpacePanelImageBinder.ImageBinding binding in binder.Images)
                {
                    if (binding != null && binding.elementName == mediaName && binding.image != null)
                    {
                        sequence.Add(binding.image);
                        break;
                    }
                }
            }
            foreach (Sprite sprite in extraImages)
            {
                if (sprite != null)
                {
                    sequence.Add(sprite);
                }
            }
        }

        private void RefreshArrows()
        {
            bool many = sequence.Count > 1;
            SetVisible(previousButton, many && (loop || index > 0));
            SetVisible(nextButton, many && (loop || index < sequence.Count - 1));
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private IEnumerator BindWhenReady()
        {
            for (int frame = 0; frame < MaxBindFrames && !bound; frame++)
            {
                yield return null;
                if (TryBind())
                {
                    break;
                }
            }
            bindRoutine = null;
        }

        private bool TryBind()
        {
            if (bound)
            {
                return true;
            }
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }
            if (document == null)
            {
                Debug.LogWarning($"[{nameof(WorldSpacePanelImageCarousel)}] No UIDocument found on '{name}'.", this);
                return false;
            }

            VisualElement root = document.rootVisualElement;
            media = root?.Q<VisualElement>(mediaName);
            if (media == null)
            {
                // Tree not built yet (or the name is wrong); retry.
                return false;
            }
            previousButton = root.Q<Button>(previousName);
            nextButton = root.Q<Button>(nextName);

            if (previousButton != null)
            {
                previousButton.clicked += Previous;
            }
            if (nextButton != null)
            {
                nextButton.clicked += Next;
            }

            BuildSequence();
            index = 0;
            if (sequence.Count > 0)
            {
                ShowImage(0);
            }
            else
            {
                // No image at all: leave the element to whatever the USS authored.
                RefreshArrows();
            }

            bound = true;
            return true;
        }

        private void Unbind()
        {
            if (previousButton != null)
            {
                previousButton.clicked -= Previous;
                previousButton = null;
            }
            if (nextButton != null)
            {
                nextButton.clicked -= Next;
                nextButton = null;
            }
            media = null;
            bound = false;
        }
    }
}
