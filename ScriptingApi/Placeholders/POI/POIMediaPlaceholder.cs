
using UnityEditor;

using UnityEngine;
using UnityEngine.UI;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class POIMediaPlaceholder : POIBlockPlaceholder
    {
        public enum EPOIBlockMediaType
        {
            Image = 1,
            Video = 2
        }

        [SerializeField] private GameObject imagePlaceholder;
        [SerializeField] private GameObject videoPlaceholder;


        [Space]
        [Header("Configurable stuff")]

        [SerializeField, Tooltip("select the type of this media POI block.")]
        [OnChangedCall(nameof(OnPoiBlockMediaTypeChanged))]
        private EPOIBlockMediaType poiBlockMediaType = EPOIBlockMediaType.Image;

        [Header("Image settings")]
        [SerializeField, Tooltip("An image can be referenced with a sprite.")]
        [OnChangedCall(nameof(OnImageChanged))/*, DrawIf(nameof(poiBlockMediaType), 1)*/]
        private Sprite image;

        [Header("Video settings")]
        [SerializeField, Tooltip("A video must be provided with a public, non authenticated url.")]
        //[DrawIf(nameof(poiBlockMediaType), 2)]
        private string url;


        public override string AddressableKey => addressableKeys[(int)poiBlockMediaType - 1];
        public Sprite Image => image;
        public string Url => url;


        public void OnPoiBlockMediaTypeChanged()
        {
#if UNITY_EDITOR
            imagePlaceholder.SetActive(poiBlockMediaType == EPOIBlockMediaType.Image);
            videoPlaceholder.SetActive(poiBlockMediaType == EPOIBlockMediaType.Video);
#endif
        }

        public void OnImageChanged()
        {
#if UNITY_EDITOR
            SerializedObject so = new(imagePlaceholder.GetComponentInChildren<Image>());
            so.FindProperty("m_Sprite").objectReferenceValue = image;
            so.ApplyModifiedProperties();
#endif
        }
    }
}
