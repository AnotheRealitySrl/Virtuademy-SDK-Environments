using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Per-instance authoring surface for the localized UI Toolkit elements
    /// (<c>Virtuademy.LocalizedComponents.LocalizedLabel</c>, <c>LocalizedButton</c>, …).
    ///
    /// Its custom Inspector lists every localized element found in the bound <see cref="UIDocument"/>'s
    /// UXML and lets you assign each element's localization key(s) on THIS instance, without editing
    /// the shared <c>.uxml</c>. At runtime the keys are pushed onto the matching elements (looked up by
    /// their UXML <c>name</c>), so the same panel asset can be reused in several places with different
    /// keys set from the Inspector.
    ///
    /// The value field shows I2's term dropdown when I2Loc is present and a plain string field
    /// otherwise — the same convention the <c>LocalizedXxx</c> elements themselves use. When I2Loc is
    /// absent the key is only stored (nothing extra is displayed); it becomes visible once I2Loc is
    /// installed and the elements translate it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LocalizedUIBinder : UIDocumentBinder
    {
        [Serializable]
        public class KeySlot
        {
            [Tooltip("The localized attribute this value is written to (e.g. locKey, locKeyLabel).")]
            public string attribute;

            [Tooltip("The localization key (or, with I2, the term) assigned to that attribute.")]
            public string value;
        }

        [Serializable]
        public class LocalizedElementBinding
        {
            [Tooltip("The 'name' of the localized element in the UXML.")]
            public string elementName;

            [Tooltip("The element's type — display only.")]
            public string elementType;

            public List<KeySlot> slots = new();
        }

        [SerializeField, Tooltip("One entry per localized element found in the UXML. Populated " +
            "automatically in the Inspector.")]
        private List<LocalizedElementBinding> bindings = new();

        public IReadOnlyList<LocalizedElementBinding> Bindings => bindings;

        /// <summary>
        /// Prepended to every key this binder writes (e.g. "Key/gimmi/Title" → "External/CreatorKit/Key/gimmi/Title"),
        /// the same namespacing the LocalizationPlaceholder keys get. Set by the host application at boot;
        /// empty in creator projects, so creators author and preview their keys as written.
        /// </summary>
        public static string KeyPrefix { get; set; } = string.Empty;

        // Rebind (e.g. after WorldSpaceUIDocumentRebuilder) pushes the keys again onto the current tree:
        // a rebuilt tree holds fresh elements with only the UXML-authored keys, so without it the panel
        // would keep its authored text in every language.
        protected override bool BindTo(VisualElement root)
        {
            foreach (LocalizedElementBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.elementName))
                {
                    continue;
                }
                VisualElement element = root.Q(binding.elementName);
                if (element == null)
                {
                    Debug.LogWarning($"[{nameof(LocalizedUIBinder)}] Element '{binding.elementName}' " +
                        $"not found in the document on '{name}'.", this);
                    continue;
                }
                ApplySlots(element, binding);
            }

            return true;
        }

        // The keys are written into the elements themselves; there is nothing to release.
        protected override void UnbindFromTree() { }

        // Writes each slot's value onto the matching localized attribute via reflection. Setting the
        // attribute triggers the element's own logic (it translates through I2 when present).
        private static void ApplySlots(VisualElement element, LocalizedElementBinding binding)
        {
            Type type = element.GetType();
            foreach (KeySlot slot in binding.slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.attribute) || string.IsNullOrEmpty(slot.value))
                {
                    // Empty value → keep whatever the UXML authored for this attribute.
                    continue;
                }
                PropertyInfo prop = type.GetProperty(slot.attribute,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(element, WithPrefix(slot.value));
                }
            }
        }

        // Applies KeyPrefix to a key, or to each key of a comma-separated list (locKeyChoices).
        // Keys that already carry the prefix are left as they are.
        private static string WithPrefix(string value)
        {
            if (string.IsNullOrEmpty(KeyPrefix))
            {
                return value;
            }
            string[] keys = value.Split(',');
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i].Trim();
                keys[i] = key.Length == 0 || key.StartsWith(KeyPrefix, StringComparison.Ordinal)
                    ? key
                    : KeyPrefix + key;
            }
            return string.Join(",", keys);
        }
    }
}
