using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Plays a video inside a UI Toolkit element, looked up by its UXML <c>name</c> — the video
    /// counterpart of <see cref="UIToolkitPanelImageBinder"/>. The <see cref="VideoPlayer"/> on this
    /// object renders into a RenderTexture that is pushed into the element's <c>background-image</c>.
    /// Works with any UIDocument, world-space or screen-space.
    ///
    /// The video plays while the component is bound — i.e. from when the object is enabled and the
    /// tree is built — and stops (releasing the texture) when the object is disabled. Opening and
    /// closing is therefore just <c>SetActive(true/false)</c> on this object, from any UnityEvent.
    ///
    /// When a <c>frame</c> element is named, it is sized to the largest box of the video's aspect ratio
    /// that fits in <c>maxFraction</c> of the document (letterboxing); leave the name empty to keep the
    /// size authored in USS.
    ///
    /// Use the URL on WebGL, where imported VideoClips are not supported. When both are set the clip
    /// wins. A relative URL is resolved against StreamingAssets.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(VideoPlayer))]
    public class UIToolkitVideoBinder : UIDocumentBinder
    {
        [SerializeField, Tooltip("Name of the element in the UXML that shows the video (as its " +
            "background-image).")]
        private string surfaceName = "video-surface";

        [SerializeField, Tooltip("Optional: name of the element sized to the video's aspect ratio. Leave " +
            "empty to keep the size authored in USS.")]
        private string frameName = "video-frame";

        [SerializeField, Range(0.1f, 1f), Tooltip("Largest fraction of the document the frame may fill.")]
        private float maxFraction = 0.8f;

        [SerializeField, Tooltip("Video to play. Not supported on WebGL: use the URL there.")]
        private VideoClip clip;

        [SerializeField, Tooltip("Video URL (http(s)://, file://, or a path relative to StreamingAssets). " +
            "Used when no clip is set.")]
        private string url;

        private VideoPlayer player;
        private RenderTexture renderTexture;
        private VisualElement root;
        private VisualElement surface;
        private VisualElement frame;
        private float aspect = 16f / 9f;

        /// <summary>Whether a clip or a URL is set.</summary>
        public bool HasVideo => clip != null || !string.IsNullOrWhiteSpace(url);

        /// <summary>The VideoPlayer used to play the video.</summary>
        public VideoPlayer Player => player;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.prepareCompleted += OnPrepared;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
            }
            ReleaseTexture();
        }

        /// <summary>Sets the clip to play and, if bound, restarts the video with it.</summary>
        public void SetClip(VideoClip newClip)
        {
            clip = newClip;
            if (IsBound)
            {
                Play();
            }
        }

        /// <summary>Sets the URL to play (used when no clip is set) and, if bound, restarts the video.</summary>
        public void SetUrl(string newUrl)
        {
            url = newUrl;
            if (IsBound)
            {
                Play();
            }
        }

        /// <summary>(Re)starts the video from the beginning. Does nothing when no video is set.</summary>
        public void Play()
        {
            Stop();
            if (!HasVideo || player == null)
            {
                return;
            }
            if (clip != null)
            {
                player.source = VideoSource.VideoClip;
                player.clip = clip;
            }
            else
            {
                player.source = VideoSource.Url;
                player.url = ResolveUrl(url);
            }
            player.Prepare();
        }

        /// <summary>Stops the video and clears the surface.</summary>
        public void Stop()
        {
            if (player != null)
            {
                player.Stop();
                player.targetTexture = null;
            }
            if (surface != null)
            {
                surface.style.backgroundImage = StyleKeyword.None;
            }
            ReleaseTexture();
        }

        protected override bool BindTo(VisualElement documentRoot)
        {
            surface = documentRoot.Q<VisualElement>(surfaceName);
            if (surface == null)
            {
                // Tree not built yet (or the name is wrong); retry.
                return false;
            }
            frame = string.IsNullOrEmpty(frameName) ? null : documentRoot.Q<VisualElement>(frameName);
            root = documentRoot;
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            // Re-bind (and restart) automatically if the tree gets rebuilt, e.g. when the panel
            // settings are swapped by UIDocumentVRPlacement.
            WatchForRebuild(surface);

            UpdateFrameSize();
            Play();
            return true;
        }

        protected override void UnbindFromTree()
        {
            Stop();
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root = null;
            surface = null;
            frame = null;
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!IsBound)
            {
                // Disabled while the video was still preparing.
                source.Stop();
                return;
            }
            int width = (int)source.width;
            int height = (int)source.height;
            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }
            aspect = (float)width / height;

            ReleaseTexture();
            renderTexture = new RenderTexture(width, height, 0) { name = nameof(UIToolkitVideoBinder) };
            renderTexture.Create();
            source.targetTexture = renderTexture;

            if (surface != null)
            {
                surface.style.backgroundImage = Background.FromRenderTexture(renderTexture);
            }
            UpdateFrameSize();
            source.Play();
        }

        private void OnRootGeometryChanged(GeometryChangedEvent _) => UpdateFrameSize();

        /// <summary>Sizes the frame to the largest box of the video's aspect ratio that fits.</summary>
        private void UpdateFrameSize()
        {
            if (root == null || frame == null)
            {
                return;
            }
            Rect area = root.contentRect;
            if (float.IsNaN(area.width) || area.width <= 0f || area.height <= 0f)
            {
                return;
            }
            float maxWidth = area.width * maxFraction;
            float maxHeight = area.height * maxFraction;
            float width = Mathf.Min(maxWidth, maxHeight * aspect);
            frame.style.width = width;
            frame.style.height = width / aspect;
        }

        private void ReleaseTexture()
        {
            if (renderTexture == null)
            {
                return;
            }
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        private static string ResolveUrl(string value)
        {
            value = value.Trim();
            return value.Contains("://") || System.IO.Path.IsPathRooted(value)
                ? value
                : $"{Application.streamingAssetsPath}/{value}";
        }
    }
}
