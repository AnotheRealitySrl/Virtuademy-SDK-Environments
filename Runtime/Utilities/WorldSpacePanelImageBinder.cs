using System;
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
    public class WorldSpacePanelImageBinder : UIDocumentBinder
    {
        [Serializable]
        public class ImageBinding
        {
            [Tooltip("The 'name' of the element in the UXML to assign the image to.")]
            public string elementName = "thumb";

            [Tooltip("The image assigned to that element's background-image. Import it as Sprite (2D and UI).")]
            public Sprite image;
        }

        [SerializeField, Tooltip("One entry per element you want to set an image on. Reference each " +
            "element by the 'name' it has in the UXML (names must be unique).")]
        private List<ImageBinding> images = new() { new ImageBinding() };

        /// <summary>The image bindings, in order.</summary>
        public IReadOnlyList<ImageBinding> Images => images;

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

        /// <summary>
        /// Forces a re-apply of every binding against the current visual tree. While the component is
        /// disabled it does nothing: its OnEnable applies.
        /// </summary>
        public void Apply() => Rebind();

        // Images are written into the elements' inline style; there is nothing to release.
        protected override void UnbindFromTree() { }

        protected override bool BindTo(VisualElement root)
        {
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
            WatchForRebuild(firstBound);
            return true;
        }
    }
}
