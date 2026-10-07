using Virtuademy.Environments.ScriptingApi.Placeholders;

using System;

using TMPro;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class LocalizationPlaceholder : SceneComponentPlaceholderBase
    {
        #region Inspector info

        [SerializeField, Tooltip("String key relative to the text description")]
        private string key;

        [SerializeField, Tooltip("the text of the textMeshPro where you want to display the description")]
        private TextMeshProUGUI textField;

        #endregion

        #region Public Events

        public Action<string> onKeyChanged;

        #endregion

        #region Properties

        public string Key => key;
        public TextMeshProUGUI TextField => textField;

        #endregion

        #region Public Methods

        public void SetKey(string newKey)
        {
            key = newKey;
            onKeyChanged?.Invoke(key);
        }

        #endregion
    }
}
