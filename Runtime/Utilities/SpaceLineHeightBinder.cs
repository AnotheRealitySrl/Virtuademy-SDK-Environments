using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Sets the line spacing of UI Toolkit text elements of a panel, looked up by their UXML
    /// <c>name</c> — the same model as <see cref="WorldSpaceButtonBinder"/> (clicks by name) and
    /// <see cref="WorldSpacePanelImageBinder"/> (images by name). Works with any UIDocument, world-space
    /// or screen-space.
    ///
    /// USS has no <c>line-height</c> property, so the spacing is applied through the rich text tag
    /// <c>&lt;line-height=…&gt;</c>, prepended to the element's text at runtime. The shared UXML/USS is
    /// never touched, and the same panel asset can use a different spacing per instance.
    ///
    /// Text that is rewritten after binding (e.g. a <c>LocalizedLabel</c> translating on attach or on a
    /// language change) is caught through its <see cref="ChangeEvent{T}"/> and gets the tag again, so
    /// the spacing survives localization. Works on any <see cref="TextElement"/> (Label, Button, the
    /// LocalizedXxx variants); give each target element a UNIQUE name in the UXML.
    ///
    /// Also runs in Edit Mode, so Inspector changes are previewed immediately; the tag only lives in
    /// the live visual tree and is never serialized into the scene or the UXML.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public class SpaceLineHeightBinder : MonoBehaviour, IVisualTreeRebindable
    {
        public enum LineHeightUnit
        {
            [Tooltip("Percentage of the font's default line height (100 = unchanged).")]
            Percent,
            [Tooltip("Absolute distance between lines, in pixels.")]
            Pixels,
            [Tooltip("Multiple of the font size.")]
            Em,
        }

        [Serializable]
        public class LineHeightBinding
        {
            [Tooltip("The 'name' of the text element (Label, Button, LocalizedLabel…) in the UXML.")]
            public string elementName = "label";

            [Tooltip("Line spacing value, interpreted according to Unit (e.g. 80 with Percent = tighter lines).")]
            public float value = 100f;

            [Tooltip("How Value is interpreted.")]
            public LineHeightUnit unit = LineHeightUnit.Percent;
        }

        [SerializeField, Tooltip("UIDocument that renders the panel. If empty, the first UIDocument on " +
            "this object or its children is used.")]
        private UIDocument document;

        [SerializeField, Tooltip("One entry per text element whose line spacing you want to change. " +
            "Reference each element by the 'name' it has in the UXML (names must be unique).")]
        private List<LineHeightBinding> lineHeights = new() { new LineHeightBinding() };

        // Maximum number of frames to wait for the UIDocument to build its visual tree.
        private const int MaxBindFrames = 120;

        // Matches a line-height tag at the very start of the text (the one this component prepends).
        private static readonly Regex LeadingTag = new(@"^<line-height=[^>]*>", RegexOptions.IgnoreCase);

        private readonly List<(TextElement element, EventCallback<ChangeEvent<string>> handler)> registered = new();
        private Coroutine bindRoutine;
        private bool bound;

#if UNITY_EDITOR
        // Edit Mode has no reliable coroutines: retries are driven by EditorApplication.update instead.
        private int editorBindFrames = MaxBindFrames;
#endif

        /// <summary>The line-height bindings, in order.</summary>
        public IReadOnlyList<LineHeightBinding> LineHeights => lineHeights;

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
            if (TryBind(logIfUnbound: false))
            {
                return;
            }
            // rootVisualElement is not always built during OnEnable on the first frame; keep trying.
            ScheduleBind();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            CancelScheduledBind();
            Unbind();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector edit (Edit or Play Mode): re-apply once the change has settled. OnValidate itself
            // runs during serialization, where touching other objects is not allowed.
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Rebind();
                }
            };
        }

        private void EditorTick()
        {
            if (Application.isPlaying || this == null)
            {
                return;
            }
            // The UIDocument recreates its tree when the UXML/USS is edited (live reload): the bound
            // elements detach, so drop them and bind the new ones.
            if (bound && registered.Exists(r => r.element.panel == null))
            {
                Unbind();
                editorBindFrames = 0;
            }
            if (!bound && editorBindFrames < MaxBindFrames)
            {
                editorBindFrames++;
                TryBind(logIfUnbound: editorBindFrames == MaxBindFrames);
            }
        }
#endif

        /// <summary>
        /// Sets (or adds) the line spacing of a named element and pushes it to the panel immediately.
        /// Handy from a UnityEvent or a Visual Scripting graph.
        /// </summary>
        public void SetLineHeight(string elementName, float value, LineHeightUnit unit)
        {
            if (string.IsNullOrEmpty(elementName))
            {
                return;
            }
            LineHeightBinding binding = lineHeights.Find(b => b != null && b.elementName == elementName);
            if (binding == null)
            {
                binding = new LineHeightBinding { elementName = elementName };
                lineHeights.Add(binding);
            }
            binding.value = value;
            binding.unit = unit;
            Rebind();
        }

        /// <summary>Sets the line spacing of a named element as a percentage (UnityEvent-friendly overload).</summary>
        public void SetLineHeightPercent(string elementName, float percent) =>
            SetLineHeight(elementName, percent, LineHeightUnit.Percent);

        /// <summary>Drops the handlers on the old tree and applies the spacing to the current one.</summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            CancelScheduledBind();
            Unbind();
            if (!TryBind(logIfUnbound: false))
            {
                ScheduleBind();
            }
        }

        private void ScheduleBind()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // EditorTick keeps retrying while not bound.
                editorBindFrames = 0;
                return;
            }
#endif
            bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void CancelScheduledBind()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
#if UNITY_EDITOR
            editorBindFrames = MaxBindFrames;
#endif
        }

        /// <summary>
        /// Removes the handlers and the tag, so a removed entry (or a disabled component) shows the
        /// original spacing again. Rebind re-applies the tag right after on the entries that remain.
        /// </summary>
        private void Unbind()
        {
            foreach ((TextElement element, EventCallback<ChangeEvent<string>> handler) in registered)
            {
                if (element == null)
                {
                    continue;
                }
                element.UnregisterCallback(handler);
                string current = element.text ?? string.Empty;
                string stripped = LeadingTag.Replace(current, string.Empty);
                if (stripped != current)
                {
                    ((INotifyValueChanged<string>)element).SetValueWithoutNotify(stripped);
                }
            }
            registered.Clear();
            bound = false;
            RepaintInEditMode();
        }

        private IEnumerator BindWhenReady()
        {
            for (int frame = 0; frame < MaxBindFrames && !bound; frame++)
            {
                yield return null;
                if (TryBind(logIfUnbound: frame == MaxBindFrames - 1))
                {
                    break;
                }
            }
            bindRoutine = null;
        }

        /// <summary>
        /// Applies every binding to the current tree. Missing elements are reported when at least one
        /// element was bound, or when <paramref name="logIfUnbound"/> is set (the last retry), so the
        /// retries do not flood the Console while the tree is still being built.
        /// </summary>
        private bool TryBind(bool logIfUnbound)
        {
            if (bound)
            {
                return true;
            }
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }
            if (document == null)
            {
                if (logIfUnbound)
                {
                    Debug.LogWarning($"[{nameof(SpaceLineHeightBinder)}] No UIDocument found on '{name}'.", this);
                }
                return false;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                // The document has not built its tree yet; the caller will retry.
                return false;
            }

            List<string> missing = null;
            foreach (LineHeightBinding binding in lineHeights)
            {
                if (binding == null || string.IsNullOrEmpty(binding.elementName))
                {
                    continue;
                }
                TextElement element = root.Q<TextElement>(binding.elementName);
                if (element == null)
                {
                    (missing ??= new List<string>()).Add(binding.elementName);
                    continue;
                }

                string tag = BuildTag(binding);
                element.enableRichText = true;
                ApplyTag(element, tag);

                // Re-apply whenever the text is rewritten (localization, scripts…).
                EventCallback<ChangeEvent<string>> handler = evt =>
                {
                    if (evt.target == element)
                    {
                        ApplyTag(element, tag);
                    }
                };
                element.RegisterCallback(handler);
                registered.Add((element, handler));
            }

            bound = registered.Count > 0;

            if (missing != null && (bound || logIfUnbound))
            {
                foreach (string elementName in missing)
                {
                    Debug.LogWarning($"[{nameof(SpaceLineHeightBinder)}] Text element " +
                        $"'{elementName}' not found in the document on '{name}'.", this);
                }
            }

            if (bound)
            {
                RepaintInEditMode();
            }
            return bound;
        }

        private static void ApplyTag(TextElement element, string tag)
        {
            string current = element.text ?? string.Empty;
            if (current.StartsWith(tag, StringComparison.Ordinal))
            {
                return;
            }
            // Replace a previous tag (e.g. after a value change) instead of stacking a second one, and
            // write without notify so the change does not bounce back into the handler.
            string tagged = tag + LeadingTag.Replace(current, string.Empty);
            ((INotifyValueChanged<string>)element).SetValueWithoutNotify(tagged);
        }

        private static string BuildTag(LineHeightBinding binding)
        {
            string number = binding.value.ToString("0.###", CultureInfo.InvariantCulture);
            string suffix = binding.unit switch
            {
                LineHeightUnit.Pixels => "px",
                LineHeightUnit.Em => "em",
                _ => "%",
            };
            return $"<line-height={number}{suffix}>";
        }

        // In Edit Mode the panels only redraw when something asks for it.
        private static void RepaintInEditMode()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
#endif
        }
    }
}
