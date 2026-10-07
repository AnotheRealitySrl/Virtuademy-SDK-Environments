using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Virtuademy.SDK.Environments.Utilities.Editor
{
    /// <summary>
    /// Shared, I2-agnostic logic behind the <c>LocalizedUIBinder</c> inspector: it scans a UXML for the
    /// localized UI Toolkit elements, keeps the serialized binding list in sync with them, and draws the
    /// list. The two concrete inspectors (with / without I2Loc) reuse this and only differ in how a
    /// single value field is drawn (term dropdown vs plain string).
    /// </summary>
    public static class LocalizedUIBinderEditorCore
    {
        // Every localized UI Toolkit element lives in this namespace (both the stub and the I2 versions).
        private const string LocalizedNamespace = "Virtuademy.LocalizedComponents";

        public class ScannedElement
        {
            public string name;
            public string type;
            public List<string> attributes = new();
        }

        public static VisualTreeAsset GetVisualTreeAsset(SerializedProperty documentProp, Object target)
        {
            UIDocument doc = documentProp.objectReferenceValue as UIDocument;
            if (doc == null && target is Component component)
            {
                doc = component.GetComponentInChildren<UIDocument>(true);
            }
            return doc != null ? doc.visualTreeAsset : null;
        }

        /// <summary>
        /// Clones the UXML off-screen and returns every localized element that has a name, together with
        /// its localization attributes. Named elements only, because the binder targets them by name.
        /// </summary>
        public static List<ScannedElement> Scan(VisualTreeAsset vta, out int unnamedCount)
        {
            unnamedCount = 0;
            List<ScannedElement> result = new();
            if (vta == null)
            {
                return result;
            }

            VisualElement root;
            try
            {
                root = vta.Instantiate();
            }
            catch
            {
                return result;
            }

            int localUnnamed = 0;
            root.Query<VisualElement>().ForEach(el =>
            {
                Type t = el.GetType();
                if (t.Namespace != LocalizedNamespace)
                {
                    return;
                }
                List<string> attrs = LocalizedAttributes(t);
                if (attrs.Count == 0)
                {
                    return;
                }
                if (string.IsNullOrEmpty(el.name))
                {
                    localUnnamed++;
                    return;
                }
                result.Add(new ScannedElement { name = el.name, type = t.Name, attributes = attrs });
            });
            unnamedCount = localUnnamed;
            return result;
        }

        // Public instance string properties named locKey* (locKey, locKeyLabel, locKeyPlaceholder, …).
        private static List<string> LocalizedAttributes(Type t)
        {
            return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite
                            && p.Name.StartsWith("locKey", StringComparison.Ordinal))
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>True when the serialized bindings no longer match the scanned elements/attributes.</summary>
        public static bool NeedsSync(SerializedProperty bindingsProp, List<ScannedElement> scanned)
        {
            if (bindingsProp.arraySize != scanned.Count)
            {
                return true;
            }
            for (int i = 0; i < scanned.Count; i++)
            {
                SerializedProperty b = bindingsProp.GetArrayElementAtIndex(i);
                if (b.FindPropertyRelative("elementName").stringValue != scanned[i].name)
                {
                    return true;
                }
                SerializedProperty slots = b.FindPropertyRelative("slots");
                if (slots.arraySize != scanned[i].attributes.Count)
                {
                    return true;
                }
                for (int s = 0; s < slots.arraySize; s++)
                {
                    if (slots.GetArrayElementAtIndex(s).FindPropertyRelative("attribute").stringValue
                        != scanned[i].attributes[s])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Rebuilds the binding list to exactly match the scanned elements, preserving any value already
        /// assigned to a given (elementName, attribute) pair.
        /// </summary>
        public static void Sync(SerializedProperty bindingsProp, List<ScannedElement> scanned)
        {
            // Snapshot existing values so renames/reorders in the UXML don't lose assigned keys.
            Dictionary<string, Dictionary<string, string>> existing = new();
            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                SerializedProperty b = bindingsProp.GetArrayElementAtIndex(i);
                string nm = b.FindPropertyRelative("elementName").stringValue;
                if (!existing.TryGetValue(nm, out Dictionary<string, string> map))
                {
                    map = existing[nm] = new Dictionary<string, string>();
                }
                SerializedProperty slots = b.FindPropertyRelative("slots");
                for (int s = 0; s < slots.arraySize; s++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(s);
                    map[slot.FindPropertyRelative("attribute").stringValue] =
                        slot.FindPropertyRelative("value").stringValue;
                }
            }

            bindingsProp.ClearArray();
            for (int i = 0; i < scanned.Count; i++)
            {
                bindingsProp.InsertArrayElementAtIndex(i);
                SerializedProperty b = bindingsProp.GetArrayElementAtIndex(i);
                b.FindPropertyRelative("elementName").stringValue = scanned[i].name;
                b.FindPropertyRelative("elementType").stringValue = scanned[i].type;

                SerializedProperty slots = b.FindPropertyRelative("slots");
                slots.ClearArray();
                for (int s = 0; s < scanned[i].attributes.Count; s++)
                {
                    slots.InsertArrayElementAtIndex(s);
                    SerializedProperty slot = slots.GetArrayElementAtIndex(s);
                    string attr = scanned[i].attributes[s];
                    slot.FindPropertyRelative("attribute").stringValue = attr;
                    string prev = existing.TryGetValue(scanned[i].name, out Dictionary<string, string> m)
                                  && m.TryGetValue(attr, out string v) ? v : string.Empty;
                    slot.FindPropertyRelative("value").stringValue = prev;
                }
            }
        }

        /// <summary>
        /// Draws one box per localized element, delegating each value field to <paramref name="drawValueField"/>
        /// (so the I2 and non-I2 inspectors can render it differently).
        /// </summary>
        public static void DrawBindings(SerializedProperty bindingsProp,
            Action<GUIContent, SerializedProperty> drawValueField)
        {
            if (bindingsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Nessun elemento localized trovato nella UXML.", MessageType.Info);
                return;
            }

            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                SerializedProperty b = bindingsProp.GetArrayElementAtIndex(i);
                string nm = b.FindPropertyRelative("elementName").stringValue;
                string type = b.FindPropertyRelative("elementType").stringValue;
                SerializedProperty slots = b.FindPropertyRelative("slots");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{nm}  ({type})", EditorStyles.boldLabel);
                    for (int s = 0; s < slots.arraySize; s++)
                    {
                        SerializedProperty slot = slots.GetArrayElementAtIndex(s);
                        string attr = slot.FindPropertyRelative("attribute").stringValue;
                        SerializedProperty valueProp = slot.FindPropertyRelative("value");
                        drawValueField(new GUIContent(PrettyLabel(attr), attr), valueProp);
                    }
                }
            }
        }

        public static string PrettyLabel(string attribute)
        {
            switch (attribute)
            {
                case "locKey": return "Key";
                case "locKeyLabel": return "Label";
                case "locKeyPlaceholder": return "Placeholder";
                case "locKeyChoices": return "Choices";
                default:
                    return attribute.StartsWith("locKey", StringComparison.Ordinal) && attribute.Length > 6
                        ? attribute.Substring(6)
                        : attribute;
            }
        }
    }
}
