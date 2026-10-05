using System.Collections;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Adds an optional video to a world-space POI panel. When a <see cref="VideoClip"/> or a URL is
    /// set, the <c>video-button</c> element of the UXML is shown and clicking it opens the video in
    /// the shared <see cref="SkyboxVideoOverlay"/>; with neither set the button is hidden
    /// (<c>display: none</c>), so the panel looks exactly like a POI without video.
    ///
    /// The button label is a <c>LocalizedButton</c>: its key is assigned per instance through
    /// <see cref="LocalizedUIBinder"/>, like the POI title and description.
    ///
    /// <c>onVideoOpen</c> fires when this POI opens its video; <c>onVideoClose</c> fires when that
    /// video goes away (closed with the X, or replaced by a video opened from another POI).
    ///
    /// Use the URL on WebGL, where imported VideoClips are not supported. When both are set the clip
    /// wins. Binds lazily and re-binds if the visual tree is rebuilt (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), like the sibling utilities.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpacePOIVideoButton : UIDocumentBinder
    {
        [SerializeField, Tooltip("Name of the video button element in the UXML.")]
        private string buttonName = "video-button";

        [SerializeField, Tooltip("Video to play. Not supported on WebGL: use the URL there.")]
        private VideoClip clip;

        [SerializeField, Tooltip("Video URL (http(s)://, file://, or a path relative to StreamingAssets). " +
            "Used when no clip is set.")]
        private string url;

        [SerializeField, Tooltip("Overlay prefab instantiated the first time a video is opened, if the " +
            "scene does not already contain a SkyboxVideoOverlay.")]
        private SkyboxVideoOverlay overlayPrefab;

        [SerializeField, Tooltip("If true, the video opens by itself when the scene starts (once, not on " +
            "every enable). If several POIs have it, the last one to start wins.")]
        private bool startOpen = false;

        [SerializeField, Tooltip("Invoked when this POI opens its video.")]
        private UnityEvent onVideoOpen = new();

        [SerializeField, Tooltip("Invoked when the video opened by this POI is closed (X) or replaced by " +
            "another POI's video.")]
        private UnityEvent onVideoClose = new();

        private Button button;

        // The overlay showing this POI's video; null when this POI's video is not open.
        private SkyboxVideoOverlay openOverlay;

        /// <summary>Whether a clip or a URL is set.</summary>
        public bool HasVideo => clip != null || !string.IsNullOrWhiteSpace(url);

        private IEnumerator Start()
        {
            if (!startOpen || !HasVideo)
            {
                yield break;
            }
            // One frame later, so the camera and the rest of the scene have run their own Start.
            yield return null;
            OpenVideo();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopListening();
        }

        /// <summary>Opens the video in the overlay. Does nothing when no video is set.</summary>
        public void OpenVideo()
        {
            if (!HasVideo)
            {
                return;
            }
            SkyboxVideoOverlay overlay = SkyboxVideoOverlay.GetOrCreate(overlayPrefab);
            if (overlay == null)
            {
                Debug.LogWarning($"[{nameof(WorldSpacePOIVideoButton)}] No SkyboxVideoOverlay in the scene " +
                    $"and no overlay prefab set on '{name}'.", this);
                return;
            }
            // Open first: if this POI's previous video was still showing, the overlay reports it
            // closed (onVideoClose) before the new one counts as opened.
            overlay.Open(clip, url);
            if (!overlay.IsOpen)
            {
                return;
            }

            openOverlay = overlay;
            openOverlay.Closed += OnOverlayClosed;
            onVideoOpen?.Invoke();
        }

        private void OnOverlayClosed()
        {
            StopListening();
            onVideoClose?.Invoke();
        }

        private void StopListening()
        {
            if (openOverlay != null)
            {
                openOverlay.Closed -= OnOverlayClosed;
                openOverlay = null;
            }
        }

        protected override bool BindTo(VisualElement root)
        {
            button = root.Q<Button>(buttonName);
            if (button == null)
            {
                // Tree not built yet (or the name is wrong); retry.
                return false;
            }

            button.style.display = HasVideo ? DisplayStyle.Flex : DisplayStyle.None;
            button.clicked += OpenVideo;
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (button != null)
            {
                button.clicked -= OpenVideo;
                button = null;
            }
        }
    }
}
