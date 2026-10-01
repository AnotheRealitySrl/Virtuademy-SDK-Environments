using System.Collections;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using UnityEngine.XR;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Full-view video player opened by <see cref="WorldSpacePOIVideoButton"/>: a semi-transparent
    /// black backdrop, the video centred and letterboxed to its aspect ratio, and a close (X) button
    /// at the video's top-right corner.
    ///
    /// The same UXML is shown in two ways, picked each time a video opens:
    /// <list type="bullet">
    /// <item>desktop / mobile — on a screen-space <see cref="PanelSettings"/>, as a full-screen overlay;</item>
    /// <item>VR (<see cref="XRSettings.isDeviceActive"/>) — on a world-space <see cref="PanelSettings"/>,
    /// as a panel placed in front of the camera, because screen-space UI is not drawn in the headset.</item>
    /// </list>
    ///
    /// One overlay serves the whole scene: <see cref="GetOrCreate"/> returns the existing one or
    /// instantiates the prefab the first time. Opening a video while another plays replaces it.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [DisallowMultipleComponent]
    public class SkyboxVideoOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("UIDocument (on a child object) that renders the overlay. Its object " +
            "is enabled while a video is open and disabled otherwise.")]
        private UIDocument document;

        [SerializeField, Tooltip("Screen-space panel settings, used on desktop and mobile.")]
        private PanelSettings screenPanelSettings;

        [SerializeField, Tooltip("World-space panel settings, used in VR.")]
        private PanelSettings worldPanelSettings;

        [Header("VR")]
        [SerializeField, Tooltip("Distance in metres from the camera at which the panel is placed in VR.")]
        private float vrDistance = 1.8f;

        [SerializeField, Tooltip("Size of the VR panel (backdrop included), in panel pixels.")]
        private Vector2 vrPanelSize = new(1200f, 750f);

        [Header("Layout")]
        [SerializeField, Range(0.1f, 1f), Tooltip("Largest fraction of the overlay the video may fill.")]
        private float videoMaxFraction = 0.8f;

        [Header("UXML names")]
        [SerializeField] private string frameName = "video-frame";
        [SerializeField] private string surfaceName = "video-surface";
        [SerializeField] private string closeButtonName = "close-button";

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxBindFrames = 120;

        private static SkyboxVideoOverlay instance;

        private VideoPlayer player;
        private RenderTexture renderTexture;
        private Coroutine bindRoutine;
        private VisualElement root;
        private VisualElement frame;
        private VisualElement surface;
        private Button closeButton;
        private float aspect = 16f / 9f;

        /// <summary>Whether a video is currently open.</summary>
        public bool IsOpen => document != null && document.gameObject.activeSelf;

        /// <summary>
        /// Returns the overlay of the scene, instantiating <paramref name="prefab"/> when there is
        /// none. Returns null if there is none and no prefab is given.
        /// </summary>
        public static SkyboxVideoOverlay GetOrCreate(SkyboxVideoOverlay prefab)
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SkyboxVideoOverlay>(FindObjectsInactive.Include);
            }
            if (instance == null && prefab != null)
            {
                instance = Instantiate(prefab);
            }
            return instance;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            player = GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.prepareCompleted += OnPrepared;

            if (document != null)
            {
                document.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
            }
            ReleaseTexture();
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Opens the overlay and plays <paramref name="clip"/>, or <paramref name="url"/> when no clip
        /// is given. A relative URL is resolved against StreamingAssets.
        /// </summary>
        public void Open(VideoClip clip, string url)
        {
            if (document == null)
            {
                Debug.LogWarning($"[{nameof(SkyboxVideoOverlay)}] No UIDocument set on '{name}'.", this);
                return;
            }
            if (clip == null && string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            if (!gameObject.activeSelf)
            {
                // An overlay left disabled in the scene: enabling it runs Awake.
                gameObject.SetActive(true);
            }

            StopVideo();
            ShowDocument(XRSettings.isDeviceActive);

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

        /// <summary>Stops the video and hides the overlay.</summary>
        public void Close()
        {
            StopVideo();
            Unbind();
            if (document != null)
            {
                document.gameObject.SetActive(false);
            }
        }

        private void ShowDocument(bool vr)
        {
            GameObject documentObject = document.gameObject;
            if (documentObject.activeSelf)
            {
                Unbind();
                documentObject.SetActive(false);
            }

            document.panelSettings = vr ? worldPanelSettings : screenPanelSettings;
            if (vr)
            {
                document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
                document.worldSpaceSize = vrPanelSize;
                document.pivot = Pivot.Center;
                PlaceInFrontOfCamera(document.transform);
            }

            documentObject.SetActive(true);
            if (!TryBind())
            {
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        private void PlaceInFrontOfCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            // Level the direction so the panel stays upright even if the user is looking up or down.
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            }
            forward.Normalize();
            target.SetPositionAndRotation(cam.transform.position + forward * vrDistance,
                Quaternion.LookRotation(forward, Vector3.up));
            target.localScale = Vector3.one;
        }

        private IEnumerator BindWhenReady()
        {
            for (int frameIndex = 0; frameIndex < MaxBindFrames; frameIndex++)
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
            if (root != null)
            {
                return true;
            }
            VisualElement documentRoot = document.rootVisualElement;
            VisualElement foundFrame = documentRoot?.Q<VisualElement>(frameName);
            VisualElement foundSurface = documentRoot?.Q<VisualElement>(surfaceName);
            Button foundClose = documentRoot?.Q<Button>(closeButtonName);
            if (foundFrame == null || foundSurface == null || foundClose == null)
            {
                return false;
            }

            root = documentRoot;
            frame = foundFrame;
            surface = foundSurface;
            closeButton = foundClose;

            closeButton.clicked += Close;
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            if (renderTexture != null)
            {
                surface.style.backgroundImage = Background.FromRenderTexture(renderTexture);
            }
            UpdateFrameSize();
            return true;
        }

        private void Unbind()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            if (closeButton != null)
            {
                closeButton.clicked -= Close;
            }
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root = null;
            frame = null;
            surface = null;
            closeButton = null;
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!IsOpen)
            {
                // Closed while the video was still preparing.
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
            renderTexture = new RenderTexture(width, height, 0) { name = "SkyboxVideoOverlay" };
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

        /// <summary>Sizes the video frame to the largest box of the video's aspect ratio that fits.</summary>
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
            float maxWidth = area.width * videoMaxFraction;
            float maxHeight = area.height * videoMaxFraction;
            float width = Mathf.Min(maxWidth, maxHeight * aspect);
            frame.style.width = width;
            frame.style.height = width / aspect;
        }

        private void StopVideo()
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

        private static string ResolveUrl(string url)
        {
            url = url.Trim();
            return url.Contains("://") || System.IO.Path.IsPathRooted(url)
                ? url
                : $"{Application.streamingAssetsPath}/{url}";
        }
    }
}
