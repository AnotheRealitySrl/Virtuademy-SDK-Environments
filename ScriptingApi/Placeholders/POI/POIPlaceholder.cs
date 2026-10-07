using Virtuademy.Environments.ScriptingApi.Placeholders;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class POIPlaceholder : SceneComponentPlaceholderBase, IAddressablePlaceholder
    {
        public enum ETitleVisibility
        {
            AlwaysHidden,
            VisibleOnHover,
            AlwaysVisible
        }

        [Space]
        [HelpBox("Quick guide on how to configure the content of a POI. " +
            "\n" + "\n" +
            "Elements of the POI: " +
            "\n" +
            "Panel: the body of the POI. A panel consists of one or more pages (see below) " +
            "and the navigation arrows to change the page. " +
            "To edit the dimensions of the panel, you can simply resize the width and height of " +
            "its RectTransform." +
            "\n" +
            "Activator: the button that opens/closes the panel of the POI." +
            "\n" +
            "Page: to add a page, you can simply duplicate the GameObject \"Page\" " +
            "as a child of the component \"Pages\". Note that the pages have the same dimensions, " +
            "which are inherited from the \"Panel\". " +
            "\n" +
            "Block: a page consists of one or more blocks. Those blocks can be added, moved and resized " +
            "within a page to add content to the POI. Note that a block cannot be positioned outside the " +
            "boundaries of the panel, otherwise it will not be visible due to masking. To edit the content " +
            "of a block, select the placeholder component provided in its prefab and follow the instructions.",
            HelpBoxMessageType.Info)]

        [Space]

        [Header("Read only references. Do no edit them.")]
        [SerializeField] private RectTransform activator;
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform pagesContainer;
        [SerializeField] private RectTransform previousPage;
        [SerializeField] private RectTransform nextPage;
        [SerializeField] private Transform panTransform;


        [Space]
        [Header("--- Configurable stuff starts here ---")]

        [Space]
        [Header("POI title configuration")]

        [SerializeField, Tooltip("Set the visibility of the title. " +
            "If AlwaysVisible, the title will always be visible. " +
            "If VisibleOnHover, the title will be visible only when hovering on the POI activator. " +
            "If AlwaysHidden, the title is deactivated")]
        private ETitleVisibility titleVisibility = ETitleVisibility.VisibleOnHover;

        [SerializeField, Tooltip("Set the text of the title.")]
        [OnChangedCall(nameof(OnPOITitleTextChanged))]
        private string titleText;

        [SerializeField, Tooltip("Change the font size of the title.")]
        [OnChangedCall(nameof(OnPOITitleFontChanged))]
        private float titleFontSize = 2;


        [Space]
        [Header("POI background configuration")]

        [HelpBox("By default the background is visible, but you can choose to hide it if you want to " +
            "arrange the content more freely in space. Be aware that, " +
            "even if the background is not visible, the content of the pages should not be placed outside " +
            "the boundaries of the panel, or they will not be visible due to masking.",
            HelpBoxMessageType.Info)]

        [SerializeField, Tooltip("Set the background visibility of the POI.")]
        [OnChangedCall(nameof(OnBackgroundVisibilityChanged))]
        private bool backgroundVisibility = true;


        [Space]
        [Header("POI audio settings")]

        [SerializeField, Tooltip("If set, the POI reproduces an audio clip while open")]
        private AudioClip audioClip;


        [Space]
        [Header("Pan settings (only WebGL)")]

        [SerializeField, Tooltip("Set the distance of the pan towards the POI panel")]
        [OnChangedCall(nameof(OnPanTransformChanged))]
        private float panDistance = 1f;

        public string AddressableKey => "POIController";


        public RectTransform Activator => activator;
        public RectTransform Title => title;
        public RectTransform Panel => panel;
        public RectTransform PagesContainer => pagesContainer;
        public RectTransform PreviousPage => previousPage;
        public RectTransform NextPage => nextPage;

        public ETitleVisibility TitleVisibility => titleVisibility;

        /// <summary>The displayed title text. The TMP under <see cref="Title"/> is the source of
        /// truth (the inspector field pushes into it on edit and is pulled back on selection);
        /// the serialized field is only the fallback when the TMP is missing.</summary>
        public string TitleText
        {
            get
            {
                TMP_Text titleTMP = title != null ? title.GetComponentInChildren<TMP_Text>(true) : null;
                return titleTMP != null ? titleTMP.text : titleText;
            }
        }

        public float TitleFontSize => titleFontSize;

        public bool BackgroundVisibility => backgroundVisibility;

        public AudioClip AudioClip => audioClip;

        public Transform PanTransform => panTransform;

        [Space]
        [Header("POI event callbacks")]
        [SerializeField] public UnityEvent OnPOIOpen = default;
        [SerializeField] public UnityEvent OnPOIClosed = default;

        [HideInInspector] public UnityEvent deselectPOI = default;

        public void OnPOITitleTextChanged()
        {
#if UNITY_EDITOR
            SerializedObject so = new(title.GetComponentInChildren<TMP_Text>());
            so.FindProperty("m_text").stringValue = titleText;
            so.ApplyModifiedProperties();
#endif
        }

        public void OnPOITitleFontChanged()
        {
#if UNITY_EDITOR
            SerializedObject so = new(title.GetComponentInChildren<TMP_Text>());
            so.FindProperty("m_fontSize").floatValue = titleFontSize / 10f;
            so.ApplyModifiedProperties();
#endif
        }

        public void OnBackgroundVisibilityChanged()
        {
            panel.GetComponentInChildren<Image>().enabled = backgroundVisibility;
        }

        public void OnPanTransformChanged()
        {
            panTransform.localPosition = new Vector3(panTransform.localPosition.x, panTransform.localPosition.y, -panDistance);
        }

        public void DeselectPOI()
        {
            deselectPOI.Invoke();
        }
    }
}
