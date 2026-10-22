using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

using Virtuademy.Environments.ScriptingApi.Placeholders;

using Object = UnityEngine.Object;

namespace Virtuademy.SDK.Environments.Editor.UIKit
{
    /// <summary>
    /// Lists the localization keys the open scenes use and the Localization File does not translate:
    /// <c>Virtuademy/Localization/Check missing translations</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keys are read from the components that look a key up at runtime: <c>LocalizationPlaceholder</c>,
    /// <c>LocalizeTaskPlaceholder</c> (when it localizes the description), <c>LocalizedUIBinder</c>
    /// slots, and every localized text of the SDK's UI Toolkit panels. A key is reported when the
    /// file has no row for it, and, per language, when its cell is empty.
    /// </para>
    /// <para>
    /// The file is the CSV the Back Office takes as the environment's Localization File: a header
    /// row with <c>Key</c> first, optionally <c>Type</c> and <c>Desc</c>, then one column per
    /// language (<c>English [en]</c>), comma-separated, quoted where a cell holds a comma, a quote
    /// or a line break.
    /// </para>
    /// </remarks>
    public class LocalizationCheckWindow : EditorWindow
    {
        private struct Use
        {
            public string Key;
            public Object Owner;
            public string Where;
        }

        private struct Finding
        {
            public string Key;
            public string Problem;
            public Object Owner;
            public string Where;
        }

        [SerializeField] private TextAsset csv;
        private readonly List<Finding> findings = new();
        private string summary;
        private Vector2 scroll;

        [MenuItem("Virtuademy/Localization/Check missing translations")]
        private static void Open()
        {
            GetWindow<LocalizationCheckWindow>("Localization check").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Checks the keys the open scenes use against the environment's Localization " +
                "File (the CSV you upload in the Back Office).", MessageType.None);
            csv = (TextAsset)EditorGUILayout.ObjectField("Localization File", csv, typeof(TextAsset), false);
            using (new EditorGUI.DisabledScope(csv == null))
            {
                if (GUILayout.Button("Check open scenes"))
                {
                    Run();
                }
            }
            if (!string.IsNullOrEmpty(summary))
            {
                EditorGUILayout.HelpBox(summary, findings.Count == 0 ? MessageType.Info : MessageType.Warning);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Finding f in findings)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel($"{f.Key}  —  {f.Problem}  ({f.Where})",
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (f.Owner != null && GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = f.Owner;
                        EditorGUIUtility.PingObject(f.Owner);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            findings.Clear();
            if (!TryParse(csv.text, out List<string> languages, out Dictionary<string, string[]> rows, out string error))
            {
                summary = error;
                return;
            }

            List<Use> uses = CollectUses();
            foreach (Use use in uses)
            {
                if (!rows.TryGetValue(use.Key, out string[] cells))
                {
                    findings.Add(new Finding { Key = use.Key, Problem = "not in the file", Owner = use.Owner, Where = use.Where });
                    continue;
                }
                for (int i = 0; i < languages.Count; i++)
                {
                    if (i >= cells.Length || string.IsNullOrWhiteSpace(cells[i]))
                    {
                        findings.Add(new Finding { Key = use.Key, Problem = $"no {languages[i]} translation", Owner = use.Owner, Where = use.Where });
                    }
                }
            }

            int distinct = uses.Select(u => u.Key).Distinct().Count();
            summary = findings.Count == 0
                ? $"{distinct} keys used, every one translated in {languages.Count} languages."
                : $"{findings.Count} problems on {distinct} keys used ({languages.Count} languages in the file).";
            var log = new StringBuilder("[Localization] " + summary);
            foreach (Finding f in findings)
            {
                log.Append("\n  ").Append(f.Key).Append(": ").Append(f.Problem).Append(" (").Append(f.Where).Append(')');
            }
            Debug.Log(log.ToString());
        }

        private static List<Use> CollectUses()
        {
            var uses = new List<Use>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        Collect(component, uses);
                    }
                }
            }
            return uses;
        }

        private static void Collect(MonoBehaviour component, List<Use> uses)
        {
            string where = $"{component.gameObject.scene.name}: {Path(component.transform)} ({component.GetType().Name})";
            var so = new SerializedObject(component);

            if (component is LocalizationPlaceholder placeholder)
            {
                Add(uses, placeholder.Key, component, where);
                return;
            }
            if (component is LocalizeTaskPlaceholder task)
            {
                if (task.localizeDescription)
                {
                    Add(uses, task.descriptionKey, component, where);
                }
                return;
            }

            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                // The SDK panels: every VirtuademyPanel.LocalizedText field.
                if (it.propertyType == SerializedPropertyType.Generic && it.type == "LocalizedText")
                {
                    SerializedProperty key = it.FindPropertyRelative("key");
                    if (key != null)
                    {
                        Add(uses, key.stringValue, component, where);
                    }
                }
                // LocalizedUIBinder: slots whose attribute is a localization key (locKey, locKeyLabel…).
                if (it.propertyType == SerializedPropertyType.Generic && it.type == "KeySlot")
                {
                    SerializedProperty attribute = it.FindPropertyRelative("attribute");
                    SerializedProperty value = it.FindPropertyRelative("value");
                    if (attribute != null && value != null
                        && attribute.stringValue.StartsWith("locKey", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string key in value.stringValue.Split(','))
                        {
                            Add(uses, key.Trim(), component, where);
                        }
                    }
                }
            }
        }

        private static void Add(List<Use> uses, string key, Object owner, string where)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            key = key.Trim();
            // The application files the environment's terms under this category; a key may be written with it.
            const string category = "External/CreatorKit/";
            if (key.StartsWith(category, StringComparison.Ordinal))
            {
                key = key.Substring(category.Length);
            }
            uses.Add(new Use { Key = key, Owner = owner, Where = where });
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent)
            {
                parts.Insert(0, t.name);
            }
            return string.Join("/", parts);
        }

        /// <summary>
        /// Reads a Localization File: languages from the header (after <c>Key</c>, and <c>Type</c> /
        /// <c>Desc</c> when present), one row per key with the cells in the same order.
        /// </summary>
        public static bool TryParse(string text, out List<string> languages, out Dictionary<string, string[]> rows,
                                    out string error)
        {
            languages = new List<string>();
            rows = new Dictionary<string, string[]>();
            error = null;
            List<List<string>> table = ParseCsv(text);
            if (table.Count == 0 || table[0].Count < 2 || !string.Equals(table[0][0].Trim(), "Key", StringComparison.OrdinalIgnoreCase))
            {
                error = "The file does not start with a header row whose first column is Key.";
                return false;
            }
            int first = 1;
            while (first < table[0].Count && (Is(table[0][first], "Type") || Is(table[0][first], "Desc") || Is(table[0][first], "Description")))
            {
                first++;
            }
            for (int c = first; c < table[0].Count; c++)
            {
                languages.Add(table[0][c].Trim().TrimStart('$'));
            }
            if (languages.Count == 0)
            {
                error = "The header names no language column.";
                return false;
            }
            for (int r = 1; r < table.Count; r++)
            {
                List<string> row = table[r];
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
                {
                    continue;
                }
                rows[row[0].Trim()] = row.Skip(first).ToArray();
            }
            return true;
        }

        private static bool Is(string cell, string name) => string.Equals(cell.Trim(), name, StringComparison.OrdinalIgnoreCase);

        private static List<List<string>> ParseCsv(string text)
        {
            var table = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        table.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                table.Add(row);
            }
            return table;
        }
    }
}
