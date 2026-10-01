using System.Collections;

using UnityEngine;
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
    /// Use the URL on WebGL, where imported VideoClips are not supported. When both are set the clip
    /// wins. Binds lazily and re-binds if the visual tree is rebuilt (e.g. by
    /// <see cref="WorldSpaceUIDocumentRebuilder"/>), like the sibling utilities.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpacePOIVideoButton : MonoBehaviour, IVisualTreeRebindable
    {
        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

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

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxBindFrames = 120;

        private Coroutine bindRoutine;
        private Button button;
        private bool bound;

        /// <summary>Whether a clip or a URL is set.</summary>
        public bool HasVideo => clip != null || !string.IsNullOrWhiteSpace(url);

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

        /// <summary>Drops the old tree's button and binds the current one.</summary>
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
            overlay.Open(clip, url);
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
                Debug.LogWarning($"[{nameof(WorldSpacePOIVideoButton)}] No UIDocument found on '{name}'.", this);
                return false;
            }

            VisualElement root = document.rootVisualElement;
            button = root?.Q<Button>(buttonName);
            if (button == null)
            {
                // Tree not built yet (or the name is wrong); retry.
                return false;
            }

            button.style.display = HasVideo ? DisplayStyle.Flex : DisplayStyle.None;
            button.clicked += OpenVideo;
            bound = true;
            return true;
        }

        private void Unbind()
        {
            if (button != null)
            {
                button.clicked -= OpenVideo;
                button = null;
            }
            bound = false;
        }
    }
}
