using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// One scene of the registry: its name (the scene is not editable — the registry owns the list),
    /// "Include in build", the platforms, and where that scene is already published ("Published in
    /// Showroom, Academy" / "Published at tenant level" / "Not published yet", one part per connection when the
    /// project deploys to several) read from <see cref="PublishedEnvironmentsIndex"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneListScriptableObject.SceneConfiguration))]
    internal class SceneConfigurationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new();
            root.style.marginBottom = 6;
            root.style.paddingBottom = 4;
            root.style.borderBottomWidth = 1;
            root.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);

            SerializedProperty scene = property.FindPropertyRelative("scene");
            Object asset = scene.objectReferenceValue;

            VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            Label name = new(asset != null ? asset.name : "(missing scene)")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 },
                tooltip = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty
            };
            header.Add(name);
            if (asset != null)
            {
                Button select = new(() => { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; }) { text = "Show" };
                select.style.fontSize = 10;
                select.style.paddingTop = select.style.paddingBottom = 0;
                header.Add(select);
            }
            root.Add(header);

            root.Add(new PropertyField(property.FindPropertyRelative("includeInBuild"), "Include in build"));
            root.Add(new PropertyField(property.FindPropertyRelative("supportedPlatforms"), "Platforms"));

            Label published = new();
            published.style.marginLeft = 3;
            published.style.marginTop = 1;
            published.style.fontSize = 11;
            published.style.unityFontStyleAndWeight = FontStyle.Italic;
            published.style.opacity = 0.85f;
            published.style.whiteSpace = WhiteSpace.Normal;
            root.Add(published);

            void Refresh()
            {
                if (asset == null) { published.text = string.Empty; published.tooltip = string.Empty; return; }
                string key = PublishedEnvironmentsIndex.Key(asset.name);
                published.text = PublishedEnvironmentsIndex.Summary(key);
                published.tooltip = PublishedEnvironmentsIndex.AnyData ? PublishedEnvironmentsIndex.Details(key) : string.Empty;
            }

            Refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ => PublishedEnvironmentsIndex.Changed += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => PublishedEnvironmentsIndex.Changed -= Refresh);
            return root;
        }
    }
}
