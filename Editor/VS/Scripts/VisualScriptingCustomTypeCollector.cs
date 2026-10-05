#if UNITY_EDITOR

using System;
using System.Collections.Generic;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [CreateAssetMenu(menuName = "Virtuademy/Editor/VisualScriptingCustomTypeCollector", fileName = "VisualScriptingCustomTypeCollector")]
    public class VisualScriptingCustomTypeCollector : ScriptableObject
    {
        [SerializeField]
        private TextAsset[] customTypeTexts = new TextAsset[0];

        [SerializeField]
        private string[] customTypeNames = new string[0];

        public TextAsset[] CustomTypeTexts { get => customTypeTexts; set => customTypeTexts = value; }
        public string[] CustomTypeNames { get => customTypeNames; set => customTypeNames = value; }

        public List<Type> GetCustomTypes()
        {
            List<Type> types = new List<Type>();
            foreach (TextAsset textAsset in customTypeTexts)
            {
                // If the textAsset is null or missing, skip it without feedback.
                if (!textAsset)
                    continue;

                foreach (var typeName in textAsset.GetTextTypes())
                {
                    types.Add(TypeExtensions.GetTypeFromString(typeName));
                }
            }

            foreach (string customTypeNames in customTypeNames)
            {
                types.Add(TypeExtensions.GetTypeFromString(customTypeNames));
            }

            return types;
        }
    }
}
#endif