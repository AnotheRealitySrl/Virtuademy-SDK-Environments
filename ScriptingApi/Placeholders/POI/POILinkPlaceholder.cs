
using System;

using TMPro;


using UnityEditor;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [Serializable]
    public class POILinkPlaceholder : POIBlockPlaceholder
    {
        [Space]
        [Header("Configurable stuff")]

        [SerializeField, Tooltip("Add a link that will be opened by the application")]
        private string link;

        [SerializeField, Tooltip("A short text that provides info to the link, like \"see more\" or \"download\".")]
        [OnChangedCall(nameof(OnTextChanged))]
        private string text;

        [SerializeField, Tooltip("Change the font size of the text.")]
        [OnChangedCall(nameof(OnFontSizeChanged))]
        private float fontSize = 1f;

        public string Link => link;

        /// <summary>The displayed link label. The TMP in the placeholder hierarchy is the source
        /// of truth (the inspector field pushes into it on edit and is pulled back on selection);
        /// the serialized field is only the fallback when the TMP is missing.</summary>
        public string Text
        {
            get
            {
                TMP_Text textTMP = GetComponentInChildren<TMP_Text>(true);
                return textTMP != null ? textTMP.text : text;
            }
        }

        public float FontSize => fontSize;


        public void OnTextChanged()
        {
#if UNITY_EDITOR
            SerializedObject so = new(transform.GetComponentInChildren<TMP_Text>());
            so.FindProperty("m_text").stringValue = text;
            so.ApplyModifiedProperties();
#endif
        }

        public void OnFontSizeChanged()
        {
#if UNITY_EDITOR
            SerializedObject so = new(transform.GetComponentInChildren<TMP_Text>());
            so.FindProperty("m_fontSize").floatValue = fontSize / 10f;
            so.ApplyModifiedProperties();
#endif
        }
    }
}
