#if !VIRTUADEMY_APPLICATION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    // The small pieces every part of the window is drawn with: text, buttons, banners, sections, skeletons, a spinner, and the previews' textures.
    internal sealed partial class EnvironmentDetailsWindow
    {
        private enum BannerKind
        {
            Info,
            Warning,
            Error,
        }

        private static Label Text(string text, params string[] classes)
        {
            Label label = new(text);
            foreach (string name in classes)
                label.AddToClassList(name);
            return label;
        }

        private static Button Btn(string text, System.Action clicked, string tooltip = null, bool primary = false, bool enabled = true)
        {
            Button button = new(clicked) { text = text, tooltip = tooltip ?? string.Empty };
            button.style.flexShrink = 0;
            if (primary)
                button.AddToClassList("vd-primary");
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>
        /// One line of text with some parts in bold, wrapping as the window narrows: a single label with rich text, so the spaces between the parts stay.
        /// The text of the parts is never read as markup (a "&lt;" in a file or world name is replaced).
        /// </summary>
        private static Label Rich(params (string Text, bool Bold)[] parts)
        {
            StringBuilder text = new();
            foreach ((string part, bool bold) in parts)
            {
                string safe = (part ?? string.Empty).Replace('<', '‹');
                text.Append(bold ? "<b>" + safe + "</b>" : safe);
            }

            Label label = new(text.ToString()) { enableRichText = true };
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            return label;
        }

        private static VisualElement Row(params VisualElement[] children)
        {
            VisualElement row = new();
            row.AddToClassList("vd-row");
            foreach (VisualElement child in children)
                row.Add(child);
            return row;
        }

        /// <summary>A banner: a round badge (i, !, x), the text, and the buttons that go with it.</summary>
        private static VisualElement Banner(BannerKind kind, string text, params VisualElement[] actions)
        {
            VisualElement banner = new();
            banner.AddToClassList("vd-banner");
            banner.AddToClassList(kind == BannerKind.Error ? "vd-banner--err" : kind == BannerKind.Warning ? "vd-banner--warn" : "vd-banner--info");

            Label badge = new(kind == BannerKind.Error ? "×" : kind == BannerKind.Warning ? "!" : "i");
            badge.AddToClassList("vd-badge");
            badge.AddToClassList(kind == BannerKind.Error ? "vd-badge--err" : kind == BannerKind.Warning ? "vd-badge--warn" : "vd-badge--info");
            banner.Add(badge);

            banner.Add(Text(text, "vd-banner-text"));
            foreach (VisualElement action in actions)
                if (action != null)
                    banner.Add(action);
            return banner;
        }

        /// <summary>A section of a pane, drawn like the sections of Publish Environments: a title with an optional note, and a body.</summary>
        private static VisualElement Section(string title, string note, out VisualElement body, string tooltip = null)
        {
            VisualElement section = new();
            section.AddToClassList("section");

            VisualElement header = new();
            header.AddToClassList("section-header");
            Label name = Text(title, "section-title");
            if (!string.IsNullOrEmpty(tooltip))
                name.tooltip = tooltip;
            header.Add(name);
            if (!string.IsNullOrEmpty(note))
                header.Add(Text(note, "section-note"));
            section.Add(header);

            body = new VisualElement();
            body.AddToClassList("section-body");
            section.Add(body);
            return section;
        }

        private static VisualElement Spinner()
        {
            VisualElement spinner = new() { pickingMode = PickingMode.Ignore };
            spinner.AddToClassList("vd-spinner");
            float angle = 0f;
            spinner.schedule.Execute(() =>
            {
                angle = (angle + 45f) % 360f;
                spinner.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }).Every(90);
            return spinner;
        }

        private static VisualElement Skeleton(float widthPercent, float height = 10f)
        {
            VisualElement bar = new() { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("vd-skel");
            bar.style.width = Length.Percent(widthPercent);
            bar.style.height = height;
            return bar;
        }

        /// <summary>
        /// A rectangle that shows an image, or an empty frame when there is none. The texture belongs to the window's cache
        /// (<see cref="PreviewOf"/>): it is destroyed there, never here.
        /// </summary>
        private static VisualElement Thumb(Texture2D texture, float width, float height)
        {
            VisualElement frame = new() { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("vd-thumb");
            frame.style.width = width;
            frame.style.height = height;
            if (texture != null)
            {
                Image image = new() { image = texture, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
                image.style.flexGrow = 1;
                frame.Add(image);
            }
            else
            {
                frame.AddToClassList("vd-thumb--none");
            }

            return frame;
        }

        private static Label Chip(string text, string tooltip = null, params string[] classes)
        {
            Label chip = new(text) { tooltip = tooltip ?? string.Empty };
            chip.AddToClassList("chip");
            foreach (string name in classes)
                chip.AddToClassList(name);
            return chip;
        }

        private static VisualElement Hidden(VisualElement element)
        {
            element.style.display = DisplayStyle.None;
            return element;
        }

        private static void SetShown(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        #region Previews

        // The pictures the window shows, by the file they come from. Each is a Texture2D the window made, so each is destroyed by it: when the
        // scene changes, on Refresh and when the window closes. Never a file under Assets/.
        private readonly Dictionary<string, Texture2D> previews = new();

        /// <summary>The image a file holds, as a texture; null when the file is not there or is not an image.</summary>
        private Texture2D PreviewOf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (previews.TryGetValue(path, out Texture2D known))
                return known;

            Texture2D texture = null;
            try
            {
                if (File.Exists(path))
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (!texture.LoadImage(File.ReadAllBytes(path), true))
                    {
                        DestroyImmediate(texture);
                        texture = null;
                    }
                }
            }
            catch (Exception)
            {
                texture = null;
            }

            previews[path] = texture;
            return texture;
        }

        /// <summary>The image of bytes the SDK is about to send (a prepared file), as a texture, kept under <paramref name="key"/>.</summary>
        private Texture2D PreviewOfBytes(string key, byte[] bytes)
        {
            if (bytes == null)
                return null;

            if (previews.TryGetValue(key, out Texture2D known))
                return known;

            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!texture.LoadImage(bytes, true))
            {
                DestroyImmediate(texture);
                texture = null;
            }

            previews[key] = texture;
            return texture;
        }

        /// <summary>Lets go of the pictures made from files about to be sent (<see cref="PreviewOfBytes"/>): the asset they came from changed or the flow ended.</summary>
        private void ReleaseNewPreviews()
        {
            foreach (string key in previews.Keys.Where(k => k.StartsWith("new|", StringComparison.Ordinal)).ToList())
            {
                if (previews[key] != null)
                    DestroyImmediate(previews[key]);
                previews.Remove(key);
            }
        }

        private void ClearPreviews()
        {
            foreach (Texture2D texture in previews.Values)
                if (texture != null)
                    DestroyImmediate(texture);

            previews.Clear();
        }

        // What a cached online localization file says about itself: how many keys, which languages. Read from the file in the cache folder.
        private readonly Dictionary<string, LocalizationCsvCheck> contents = new();

        private LocalizationCsvCheck ContentOf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (contents.TryGetValue(path, out LocalizationCsvCheck known))
                return known;

            LocalizationCsvCheck check = null;
            try
            {
                if (File.Exists(path))
                    check = LocalizationCsvCheck.Check(File.ReadAllBytes(path), null);
            }
            catch (Exception)
            {
                check = null;
            }

            contents[path] = check;
            return check;
        }

        /// <summary>How a project asset is named in a line: under the publish folder, <c>PublishFiles/&lt;file&gt;</c>; anywhere else its path.</summary>
        private static string ProjectName(string asset)
        {
            const string prefix = EnvironmentFilesService.PublishFolder + "/";
            return asset.StartsWith(prefix, StringComparison.Ordinal) ? "PublishFiles/" + asset.Substring(prefix.Length) : asset;
        }

        #endregion
    }
}
#endif
