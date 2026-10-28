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
    /// scene in the Project window), the platforms as three toggles, and, under it, where that scene is already published
    /// ("Published in Showroom, Academy" / "Published at tenant level" / "Not published yet", one part per connection when
    /// the project deploys to several) read from <see cref="PublishedEnvironmentsIndex"/>.
    /// </summary>
    /// <remarks>
    /// The fields are written through the serialized object and announce the change with a
    /// <see cref="SerializedPropertyChangeEvent"/>, which the window listens to (platform warnings, deploy previews).
    /// </remarks>
    [CustomPropertyDrawer(typeof(SceneListScriptableObject.SceneConfiguration))]
    internal class SceneConfigurationDrawer : PropertyDrawer
    {
        private static readonly (ESupportedPlatform Flag, string Text, string Tooltip)[] platforms =
        {
            (ESupportedPlatform.VR, "VR", "Build this scene for the VR headsets (Android)."),
            (ESupportedPlatform.WebGL, "WebGL", "Build this scene for the browser."),
            (ESupportedPlatform.Mobile, "Mobile", "Build this scene for phones and tablets (Android and iOS)."),
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

            foreach ((ESupportedPlatform flag, string text, string tooltip) in platforms)
            {
                Button chip = new() { text = text, tooltip = tooltip };
                chip.AddToClassList("chip-toggle");

                void RefreshChip()
                {
                    bool on = (Find("supportedPlatforms").intValue & (int)flag) != 0;
                    chip.EnableInClassList("chip-toggle--on", on);
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

            root.Add(row);

            Label published = new() { style = { marginLeft = 22, fontSize = 11, color = new Color(0.6f, 0.6f, 0.6f), whiteSpace = WhiteSpace.Normal } };
            root.Add(published);

            void Refresh()
            {
                if (asset == null) { published.text = string.Empty; published.tooltip = string.Empty; published.style.display = DisplayStyle.None; return; }
                string key = PublishedEnvironmentsIndex.Key(asset.name);
                published.text = PublishedEnvironmentsIndex.Summary(key);
                published.tooltip = PublishedEnvironmentsIndex.AnyData ? PublishedEnvironmentsIndex.Details(key) : string.Empty;
                published.style.display = string.IsNullOrEmpty(published.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            Refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ => PublishedEnvironmentsIndex.Changed += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => PublishedEnvironmentsIndex.Changed -= Refresh);
            return root;
        }
    }
}
