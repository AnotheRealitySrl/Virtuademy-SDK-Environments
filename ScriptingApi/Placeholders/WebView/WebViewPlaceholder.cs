using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;
using UnityEngine.UI;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public enum QuerystringParameter
    {
        UserId,
        NickName,
        Role,
        ExperienceID,
        ExperienceLabel,
        TrackID,
        TrackLabel
    }

    public class WebViewPlaceholder : SceneComponentPlaceholderBase
    {
        [Header("Prefab instantiation")]
        [SerializeField] private string addressableKey;

        [Header("2D WebView settings")]
        [SerializeField] private RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        [SerializeField] private int rectWidth;
        [SerializeField] private int rectHeight;
        [SerializeField] private CanvasScaler.ScaleMode uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        [SerializeField] private float referenceResolutionX = 1280f;
        [SerializeField] private float referenceResolutionY = 720f;
        [SerializeField] private CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        [SerializeField] private float match = 0.5f;
        [SerializeField] private CanvasScaler.Unit physicalUnit = CanvasScaler.Unit.Points;
        [SerializeField] private float fallbackScreenDPI = 96f;
        [SerializeField] private float defaultSpriteDPI = 96f;
        [SerializeField] private float referencePixelPerUnit = 100f;

        [Header("3D WebView settings")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private RectTransform canvasTransform;
        [SerializeField] private RectTransform uiTransform;

        [Header("General settings")]
        [SerializeField] private string urlSandbox;
        [SerializeField] private string urlProduction;
        [SerializeField] private float resolution = 1.5f;
        [SerializeField] private bool native2DMode = true;
        [SerializeField] private float pixelDensity = 2f;

        [Header("WebView controller settings")]
        [SerializeField] private bool startWebViewVisible;

        [Header("Canvas Group settings")]
        [SerializeField] private bool ignoreParentGroups = true;

        [Header("Querystring formation")]
        [SerializeField] private WebViewQuerystringScriptable querystringMappings;


        public string AddressableKey => addressableKey;

        public RenderMode RenderMode => renderMode;
        public int RectWidth => rectWidth;
        public int RectHeight => rectHeight;
        public CanvasScaler.ScaleMode UiScaleMode => uiScaleMode;
        public float ReferenceResolutionX => referenceResolutionX;
        public float ReferenceResolutionY => referenceResolutionY;
        public CanvasScaler.ScreenMatchMode ScreenMatchMode => screenMatchMode;
        public float Match => match;
        public CanvasScaler.Unit PhysicalUnit => physicalUnit;
        public float FallbackScreenDPI => fallbackScreenDPI;
        public float DefaultSpriteDPI => defaultSpriteDPI;
        public float ReferencePixelPerUnit => referencePixelPerUnit;


        public Transform TargetTransform => targetTransform;
        public RectTransform CanvasTransform => canvasTransform;
        public RectTransform UiTransform => uiTransform;

        public string UrlSandbox => urlSandbox;
        public string UrlProduction => urlProduction;
        public float Resolution => resolution;
        public bool Native2DMode => native2DMode;
        public float PixelDensity => pixelDensity;

        public bool StartWebViewVisible => startWebViewVisible;

        public bool IgnoreParentGroups => ignoreParentGroups;

        public WebViewQuerystringScriptable QuerystringMappings => querystringMappings;
    }
}


