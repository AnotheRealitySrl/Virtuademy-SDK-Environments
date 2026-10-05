using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class WebViewPlaceholder3D : SceneComponentPlaceholderBase
    {
        [Header("Prefab instantiation")]
        [SerializeField] private string addressableKey;

        [Header("3D WebView settings")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private RectTransform canvasTransform;
        [SerializeField] private RectTransform uiTransform;
        [SerializeField] private float resolution;
        [SerializeField] private bool keyboardEnabled;

        [Header("General settings")]
        [SerializeField] private string urlSandbox;
        [SerializeField] private string urlProduction;

        [Header("Querystring formation")]
        [SerializeField] private WebViewQuerystringScriptable querystringMappings;


        public string AddressableKey => addressableKey;

        public Transform TargetTransform => targetTransform;
        public RectTransform CanvasTransform => canvasTransform;
        public RectTransform UiTransform => uiTransform;
        public float Resolution => resolution;
        public bool KeyboardEnabled => keyboardEnabled;

        public string UrlSandbox => urlSandbox;
        public string UrlProduction => urlProduction;

        public WebViewQuerystringScriptable QuerystringMappings => querystringMappings;
    }
}


