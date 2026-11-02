using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.SDK.Core.ApplicationManagement;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// One scene of the registry, on one row: "include in build" as a tick, the scene's name (a double click selects the
    /// scene in the Project window), the default thumbnail and the default localization file, the platforms as three toggles, and
    /// the <c>i</c> button that opens the Environment Details window on the scene. Under the row, one line says where the scene is already
    /// published ("Published in 4 places on 2 connections" / "Not published yet"), read from <see cref="PublishedEnvironmentsIndex"/>, with
    /// one part per connection in its tooltip.
    /// </summary>
    /// <remarks>
    /// The fields are written through the serialized object and announce the change with a
    /// <see cref="SerializedPropertyChangeEvent"/>, which the window listens to (platform warnings, deploy previews). The two defaults
    /// are the ones a deploy sends the first time it makes the scene's environment in a place (<see cref="SceneDefaults"/>); a drop of an
    /// asset on the thumbnail slot or on the Localization chip sets one, a click opens the details.
    /// </remarks>
    [CustomPropertyDrawer(typeof(SceneListScriptableObject.SceneConfiguration))]
    internal class SceneConfigurationDrawer : PropertyDrawer
    {
        private static readonly (ESupportedPlatform Flag, string Text, string On, string Off)[] platforms =
        {
            (ESupportedPlatform.VR, "VR", "Built for the VR headsets. Click to leave it out.", "Not built for VR. Click to include it."),
            (ESupportedPlatform.WebGL, "WebGL", "Built for the browser. Click to leave it out.", "Not built for the browser. Click to include it."),
            (ESupportedPlatform.Mobile, "Mobile", "Built for phones and tablets. Click to leave it out.", "Not built for phones and tablets. Click to include it."),
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            SerializedProperty Find(string relative) => serializedObject.FindProperty(path + "." + relative);

            VisualElement root = new() { style = { paddingTop = 1, paddingBottom = 2 } };

            void Changed(SerializedProperty changed)
            {
                serializedObject.ApplyModifiedProperties();
                using SerializedPropertyChangeEvent evt = SerializedPropertyChangeEvent.GetPooled();
                evt.changedProperty = changed;
                evt.target = root;
                root.SendEvent(evt);
            }

            Object asset = Find("scene").objectReferenceValue;
            string assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;

            VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            Toggle include = new() { value = Find("includeInBuild").boolValue, tooltip = "Include this scene in the build and in the deploy." };
            include.style.marginRight = 4;
            row.Add(include);

            Label name = new(asset != null ? asset.name : "(missing scene)")
            {
                tooltip = asset != null ? assetPath + "\nClick to show it in the Project window, double-click to open it." : string.Empty,
                style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis },
            };
            if (asset != null)
            {
                name.AddToClassList("scene-name");
                name.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    if (evt.clickCount == 2 && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(assetPath);
                });
            }
            row.Add(name);

            void RefreshInclude(bool included) => name.style.color = included ? StyleKeyword.Null : new Color(0.55f, 0.55f, 0.55f);
            RefreshInclude(include.value);
            include.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty prop = Find("includeInBuild");
                prop.boolValue = evt.newValue;
                RefreshInclude(evt.newValue);
                Changed(prop);
            });

            // The defaults: what a deploy sends the first time it makes this scene's environment in a place.
            Label refusal = new() { style = { marginLeft = 22, fontSize = 11, whiteSpace = WhiteSpace.Normal, display = DisplayStyle.None } };
            refusal.AddToClassList("default-refusal");
            IVisualElementScheduledItem hideRefusal = null;

            void ShowRefusal(string message)
            {
                refusal.text = message;
                refusal.style.display = DisplayStyle.Flex;
                hideRefusal?.Pause();
                hideRefusal = refusal.schedule.Execute(() => refusal.style.display = DisplayStyle.None).StartingIn(4000);
            }

            DefaultSlot slot = new();
            row.Add(slot);

            Button localization = new() { text = "Localization" };
            localization.AddToClassList("default-chip");
            row.Add(localization);

            void OpenDetails()
            {
                if (asset != null)
                    EnvironmentDetailsEntry.Open(PublishedEnvironmentsIndex.Key(asset.name));
            }

            void SetDefault(FileKind kind, UnityEngine.Object offered)
            {
                SerializedProperty prop = Find(kind == FileKind.Thumbnail ? "defaultThumbnail" : "defaultLocalization");
                prop.objectReferenceValue = offered;
                Changed(prop);
                AssetDatabase.SaveAssetIfDirty(serializedObject.targetObject);
                PublishedFiles.Invalidate();
                SceneDefaults.NotifyChanged();
            }

            void AcceptDrops(VisualElement target, FileKind kind)
            {
                target.RegisterCallback<DragEnterEvent>(_ => target.AddToClassList("default-drop"));
                target.RegisterCallback<DragLeaveEvent>(_ => target.RemoveFromClassList("default-drop"));
                target.RegisterCallback<DragExitedEvent>(_ => target.RemoveFromClassList("default-drop"));
                target.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    bool any = DragAndDrop.objectReferences.Any(o => o != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o)));
                    DragAndDrop.visualMode = any ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    evt.StopPropagation();
                });
                target.RegisterCallback<DragPerformEvent>(evt =>
                {
                    target.RemoveFromClassList("default-drop");
                    DragAndDrop.AcceptDrag();
                    UnityEngine.Object accepted = SceneDefaults.Accept(kind, DragAndDrop.objectReferences, out string why);
                    if (accepted != null)
                        SetDefault(kind, accepted);
                    else if (why != null)
                        ShowRefusal(why);
                    evt.StopPropagation();
                });
            }

            AcceptDrops(slot, FileKind.Thumbnail);
            AcceptDrops(localization, FileKind.Localization);
            slot.RegisterCallback<ClickEvent>(_ => OpenDetails());
            localization.clicked += OpenDetails;

            void RefreshDefaults()
            {
                // After an undo the list may have another shape: a row built before keeps a property path that no longer exists.
                if (serializedObject.targetObject == null)
                    return;

                serializedObject.Update();
                SerializedProperty thumbnailProperty = Find("defaultThumbnail");
                SerializedProperty csvProperty = Find("defaultLocalization");
                if (thumbnailProperty == null || csvProperty == null)
                    return;

                Texture2D thumbnail = thumbnailProperty.objectReferenceValue as Texture2D;
                TextAsset csv = csvProperty.objectReferenceValue as TextAsset;
                string thumbnailName = thumbnail != null ? Path.GetFileName(AssetDatabase.GetAssetPath(thumbnail)) : null;
                string csvName = csv != null ? Path.GetFileName(AssetDatabase.GetAssetPath(csv)) : null;

                slot.SetImage(thumbnail);
                slot.tooltip = thumbnail != null
                    ? $"Default thumbnail: {thumbnailName}. Used the first time this scene is published in a place. Drop an image asset here, or click to open the details."
                    : "No default thumbnail. Drop an image asset here, or click to open the details.";

                localization.EnableInClassList("default-chip--empty", csv == null);
                localization.tooltip = csv != null
                    ? $"Default localization file: {csvName}. Used the first time this scene is published in a place."
                    : "No default localization file. Drop a .csv asset here, or click to open the details.";
            }

            foreach ((ESupportedPlatform flag, string text, string onTooltip, string offTooltip) in platforms)
            {
                Button chip = new() { text = text };
                chip.AddToClassList("chip-toggle");

                void RefreshChip()
                {
                    bool on = (Find("supportedPlatforms").intValue & (int)flag) != 0;
                    chip.EnableInClassList("chip-toggle--on", on);
                    chip.tooltip = on ? onTooltip : offTooltip;
                }

                chip.clicked += () =>
                {
                    SerializedProperty prop = Find("supportedPlatforms");
                    prop.intValue ^= (int)flag;
                    RefreshChip();
                    Changed(prop);
                };
                RefreshChip();
                row.Add(chip);
            }

            Button details = new(OpenDetails)
            {
                text = "i",
                tooltip = "Details: where this scene is published, its thumbnail, localization file and experiences.",
            };
            details.AddToClassList("info-button");
            row.Add(details);

            root.Add(row);
            root.Add(refusal);

            Label published = new() { style = { marginLeft = 22, fontSize = 11, color = new Color(0.6f, 0.6f, 0.6f), whiteSpace = WhiteSpace.Normal } };
            root.Add(published);

            void Refresh()
            {
                if (asset == null) { published.text = string.Empty; published.tooltip = string.Empty; published.style.display = DisplayStyle.None; return; }
                string key = PublishedEnvironmentsIndex.Key(asset.name);
                published.text = SummaryLine(key);
                published.tooltip = PublishedEnvironmentsIndex.AnyData ? PublishedEnvironmentsIndex.Summary(key) + "\n" + PublishedEnvironmentsIndex.Details(key) : string.Empty;
                published.style.display = string.IsNullOrEmpty(published.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            RefreshDefaults();
            Refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                PublishedEnvironmentsIndex.Changed += Refresh;
                SceneDefaults.Changed += RefreshDefaults;
                Undo.undoRedoPerformed += RefreshDefaults;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                PublishedEnvironmentsIndex.Changed -= Refresh;
                SceneDefaults.Changed -= RefreshDefaults;
                Undo.undoRedoPerformed -= RefreshDefaults;
            });
            return root;
        }

        /// <summary>
        /// "Published in 4 places on 2 connections" or "Not published yet" once a connection has been read; the index's own sentence while it is
        /// still being read or when nothing was read. A place is a world or a tenant level, counted once per connection.
        /// </summary>
        private static string SummaryLine(string key)
        {
            var connections = PublishedEnvironmentsIndex.ConnectionsWithData;
            if (connections.Count == 0)
                return PublishedEnvironmentsIndex.Summary(key);

            int places = 0;
            int where = 0;
            foreach (string connection in connections)
            {
                int count = PublishedEnvironmentsIndex.Get(connection, key).Count;
                places += count;
                if (count > 0)
                    where++;
            }

            return places == 0
                ? "Not published yet"
                : $"Published in {AddressablesManagementWindow.Plural(places, "place")} on {AddressablesManagementWindow.Plural(where, "connection")}";
        }

        /// <summary>
        /// The default thumbnail of a scene row: a 32 x 18 preview of the image, or a dashed slot with a plus when there is none. The dashes are
        /// painted, because a style sheet has no dashed border.
        /// </summary>
        private sealed class DefaultSlot : VisualElement
        {
            private readonly Image preview = new() { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            private readonly Label plus = new("+") { pickingMode = PickingMode.Ignore };
            private bool empty = true;

            public DefaultSlot()
            {
                AddToClassList("default-slot");
                preview.AddToClassList("default-slot-image");
                plus.AddToClassList("default-slot-plus");
                Add(preview);
                Add(plus);
                generateVisualContent += Paint;
                SetImage(null);
            }

            public void SetImage(Texture2D image)
            {
                empty = image == null;
                preview.image = image;
                preview.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
                plus.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
                EnableInClassList("default-slot--empty", empty);
                MarkDirtyRepaint();
            }

            private void Paint(MeshGenerationContext context)
            {
                if (!empty)
                    return;

                Rect rect = contentRect;
                if (rect.width < 2 || rect.height < 2)
                    return;

                const float dash = 3f;
                const float gap = 2f;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = resolvedStyle.borderTopColor;

                float left = 0.5f, top = 0.5f, right = rect.width - 0.5f, bottom = rect.height - 0.5f;
                Dashes(painter, new Vector2(left, top), new Vector2(right, top), dash, gap);
                Dashes(painter, new Vector2(right, top), new Vector2(right, bottom), dash, gap);
                Dashes(painter, new Vector2(right, bottom), new Vector2(left, bottom), dash, gap);
                Dashes(painter, new Vector2(left, bottom), new Vector2(left, top), dash, gap);
            }

            private static void Dashes(Painter2D painter, Vector2 from, Vector2 to, float dash, float gap)
            {
                Vector2 direction = (to - from).normalized;
                float length = (to - from).magnitude;
                for (float at = 0f; at < length; at += dash + gap)
                {
                    painter.BeginPath();
                    painter.MoveTo(from + direction * at);
                    painter.LineTo(from + direction * Mathf.Min(at + dash, length));
                    painter.Stroke();
                }
            }
        }
    }
}
