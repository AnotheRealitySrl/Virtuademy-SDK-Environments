
using UnityEngine;
using UnityEngine.Events;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class BigScreenPlaceholder : SceneComponentPlaceholderNetwork
    {
        [HelpBox("Do not change the value of \"IsNetworked\" field", HelpBoxMessageType.Warning)]

        [Header("Screen references. \nDo not change unless making a custom prefab.")]

        [SerializeField, Tooltip("The transform that contains the body of the media player (the screen, optional graphics, and so on). " +
            "It's recommended to put custom graphics, like a background, a logo, etc. as children of this transform, " +
            "but keep in mind that, when a media is sent to this screen, the GameObject associated with this transform will be deactivated.")]
        private Transform contentTransform;

        [SerializeField, Tooltip("The transform that represents the screen where the media is being reproduced. " +
            "Do not change its size, it will be automatically updated by using the screen settings.")]
        private Transform screenTransform;

        [SerializeField, Tooltip("The transform used by the camera in case of a pan towards the screen. " +
            "Do not change its local position, it will be automatically updated by using the screen settings.")]
        private Transform cameraPanTransform;


        [HelpBox("To resize the screen, don't modify the scale of the transforms, but use the parameters \"Screen Width\" and \"Screen Height\" " +
            "and they will adjust automatically its dimensions. The same applies to the distance of the camera pan transform.", HelpBoxMessageType.Info)]

        [Header("Screen settings")]

        [SerializeField, Tooltip("Select a user-friendly name for this screen that will be displayed to the users in the list of the available screens. " +
            "If not specified, the name of the GameObject will be used.")]
        private string screenName;

        [SerializeField, /*Range(0.5f, 10),*/ Tooltip("The width of the screen.")]
        [OnChangedCall(nameof(OnWidthChanged))]
        private float screenWidth = 1.5f;

        [SerializeField, /*Range(0.5f, 10),*/ Tooltip("The height of the screen.")]
        [OnChangedCall(nameof(OnHeightChanged))]
        private float screenHeight = 1f;

        [SerializeField/*, Range(0.5f, 10)*/, Tooltip("The distance of the transform to which the camera pans (WebGL only).")]
        [OnChangedCall(nameof(OnPanTransformChanged))]
        private float cameraPanDistance = 1f;

        [Header("Use this section to load a default media on this screen during startup.")]

        [SerializeField, Tooltip("If this flag is set, a default media is loaded in the big screen.")]
        private bool defaultMedia;

        [SerializeField, Tooltip("The type of media being loaded by default. Valid only if defaultMedia is set to true.")]
        [DrawIf(nameof(defaultMedia), true)]
        private EBigScreenMediaType mediaType;

        [SerializeField, Tooltip("The url of the media being loaded by default. Valid only if defaultMedia is set to true. " +
            "WARNING: the url must be an absolute url, with the extension of the file included!")]
        [DrawIf(nameof(defaultMedia), true)]
        private string defaultUrl;

        [SerializeField, Tooltip("If set to true, it won't be possible to change the media on this screen.")]
        [DrawIf(nameof(defaultMedia), true)]
        private bool isLocked;

        [SerializeField, Tooltip("Whether or not the video should play when spawned")]
        public bool startPaused = false;


        public Transform ScreenTransform => screenTransform;
        public Transform ContentTransform => contentTransform;
        public Transform CameraPanTransform => cameraPanTransform;
        public string ScreenName => !string.IsNullOrEmpty(screenName) ? screenName : gameObject.name;
        public bool DefaultMedia => defaultMedia;
        public EBigScreenMediaType MediaType => mediaType;
        public string DefaultUrl => defaultUrl;
        public bool IsLocked => isLocked;

        public UnityEvent onVideoPaused = default; //trigger soemthing when the video gets paused
        public UnityEvent onVideoPlayed = default; //trigger something when the video is set to play


        public void OnWidthChanged()
        {
            screenTransform.localScale = new Vector3(screenWidth, screenTransform.localScale.y, screenTransform.localScale.z);
        }

        public void OnHeightChanged()
        {
            screenTransform.localScale = new Vector3(screenTransform.localScale.x, screenHeight, screenTransform.localScale.z);
        }

        public void OnPanTransformChanged()
        {
            cameraPanTransform.localPosition = new Vector3(cameraPanTransform.localPosition.x, cameraPanTransform.localPosition.y, -cameraPanDistance);
        }
    }
}
