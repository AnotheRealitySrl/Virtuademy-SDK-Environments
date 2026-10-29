using System;

using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.SDK.TenantConfiguration.Editor;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The Addressables settings window (menu Virtuademy > Advanced > Addressables settings): the Unity Addressables settings the
    /// build of Publish Environments relies on, each with a check or a warning icon and, in its tooltip, what the SDK expects, and
    /// Fix build settings while anything differs. Until 2026.7 it was the "Advanced settings" section at the foot of Publish
    /// Environments, which keeps the banner that says when the settings are not the SDK's and opens this window from Details.
    /// </summary>
    /// <remarks>
    /// An environment project's window, like Publish Environments: the Virtuademy application (<c>VIRTUADEMY_APPLICATION</c>)
    /// builds its Addressables its own way, and its menu is not compiled there.
    /// </remarks>
    public sealed class AddressablesSettingsWindow : EditorWindow
    {
        private const string window_title = "Addressables settings";
        private const string styles_path = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Editor/Scripts/AddressablesConfigurationWindow.uss";

        private AddressableAssetSettings settings;
        private VisualElement fixBanner;
        private Label summary;

        // Each item of the page: its icon and value, what it reads, whether it is what the SDK expects, and what the SDK expects.
        private sealed class Item
        {
            internal VisualElement Icon;
            internal Label Value;
            internal Func<AddressableAssetSettings, string> Current;
            internal Func<AddressableAssetSettings, bool> Ok;
            internal string Expected;
        }

        private Item[] items;

#if !VIRTUADEMY_APPLICATION
        [MenuItem("Virtuademy/Advanced/Addressables settings", false, 43)]
#endif
        public static void Open()
        {
            AddressablesSettingsWindow window = GetWindow<AddressablesSettingsWindow>();
            window.titleContent = new GUIContent(window_title);
            window.minSize = new Vector2(420, 300);
        }

        /// <summary>
        /// Applies the build settings the SDK needs (<see cref="AddressablesConfiguration.Apply"/>), which resets the project's asset
        /// groups to a single default group, after a confirmation. True when they were applied.
        /// </summary>
        internal static bool FixBuildSettings()
        {
            if (!EditorUtility.DisplayDialog("Fix build settings",
                    "Apply the build settings the SDK needs? It resets the project's asset groups to a single default group " +
                    "(the scenes are added again at the next build).", "Fix", "Cancel"))
                return false;

            try
            {
                AddressablesConfiguration.Apply();
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError("[PublishEnvironments] " + ex.Message);
                EditorUtility.DisplayDialog("Fix build settings", ex.Message, "OK");
                return false;
            }
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent(window_title);
            settings = AddressablesConfiguration.GetOrCreateSettings();

            VisualElement root = rootVisualElement;
            StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(styles_path);
            if (styles != null)
                root.styleSheets.Add(styles);

            ScrollView scroll = new();
            root.Add(scroll);
            scroll.Add(VirtuademyWindowHeader.Create(window_title));

            scroll.Add(new Label("The Unity Addressables settings that Publish Environments builds with. A warning marks an item that is " +
                                 "not what the SDK expects: its tooltip says what is expected, and Fix build settings applies it.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginLeft = 8, marginRight = 8, marginBottom = 4 },
            }.WithClass("muted"));

            fixBanner = new VisualElement();
            fixBanner.AddToClassList("banner");
            fixBanner.AddToClassList("banner-warning");
            VisualElement bannerIcon = new();
            bannerIcon.AddToClassList("row-icon");
            AddressablesManagementWindow.SetIcon(bannerIcon, AddressablesManagementWindow.warning_icons);
            fixBanner.Add(bannerIcon);
            summary = new Label().WithClass("banner-text");
            fixBanner.Add(summary);
            fixBanner.Add(new Button(() =>
            {
                if (FixBuildSettings())
                    settings = AddressablesConfiguration.GetOrCreateSettings();
                Refresh();
            })
            {
                text = "Fix build settings",
                tooltip = "Applies the build settings the SDK needs. It resets the project's asset groups to a single default group.",
            });
            scroll.Add(fixBanner);

            Item profile = AddItem(Section(scroll, "Top-level settings", "Select", "Window/Asset Management/Addressables/Settings"),
                "Active addressables profile:", s => s.profileSettings.GetProfileName(s.activeProfileId), AddressablesConfiguration.IsSettingsConfigured,
                "The top-level settings the SDK sets: the remote catalog on Remote.BuildPath and Remote.LoadPath, a JSON catalog " +
                $"(with the {AddressablesConfiguration.JsonCatalogDefine} define), custom bundle naming, contiguous and non-recursive bundles.");

            VisualElement profileSection = Section(scroll, "Profile settings", "Open", "Window/Asset Management/Addressables/Profiles");
            Item buildPath = ProfileItem(profileSection, AddressablesConfiguration.RemoteBuildPathVariableName, AddressablesConfiguration.RemoteBuildPath);
            Item loadPath = ProfileItem(profileSection, AddressablesConfiguration.RemoteLoadPathVariableName, AddressablesConfiguration.RemoteLoadPath);
            Item buildTarget = ProfileItem(profileSection, AddressablesConfiguration.BuildTargetVariableName, AddressablesConfiguration.BuildTargetVariableValue);
            Item playerVersion = ProfileItem(profileSection, AddressablesConfiguration.PlayerVersionOverrideVariableName, AddressablesConfiguration.PlayerVersionOverrideVariableValue);

            VisualElement groupsSection = Section(scroll, "Groups settings", "Select", "Window/Asset Management/Addressables/Groups");
            Item groups = AddItem(groupsSection, "Default Local Group", null, AddressablesConfiguration.AreGroupsConfigured,
                "Expected: one default group, built and loaded from the remote paths. Fix build settings resets the groups to it.");
            groups.Icon.parent.Add(new Button(() => Selection.activeObject = settings.DefaultGroup) { text = "Select", tooltip = "Selects the default group in the Inspector." });

            items = new[] { profile, buildPath, loadPath, buildTarget, playerVersion, groups };

            // Read again every two seconds: someone can change the Addressables settings in their own windows.
            Refresh();
            root.schedule.Execute(Refresh).Every(2000);
        }

        private void OnFocus() => Refresh();

        /// <summary>A section of the page: its title, the button that opens Unity's own window for it, and the body the items go in.</summary>
        private static VisualElement Section(VisualElement parent, string title, string buttonText, string menuItem)
        {
            VisualElement section = new();
            section.AddToClassList("section");

            VisualElement header = new();
            header.AddToClassList("section-header");
            header.Add(new Label(title).WithClass("section-title"));
            header.Add(new Button(() => EditorApplication.ExecuteMenuItem(menuItem))
            {
                text = buttonText,
                tooltip = $"Opens {menuItem.Substring(menuItem.LastIndexOf('/') + 1).ToLowerInvariant()} in Unity's Addressables window.",
            });
            section.Add(header);

            VisualElement body = new();
            body.AddToClassList("section-body");
            section.Add(body);
            parent.Add(section);
            return body;
        }

        private Item ProfileItem(VisualElement section, string variable, string expected)
            => AddItem(section, variable + ":", s => AddressablesConfiguration.GetProfileValue(s, variable),
                s => AddressablesConfiguration.GetProfileValue(s, variable) == expected, "Expected: " + expected);

        private static Item AddItem(VisualElement section, string name, Func<AddressableAssetSettings, string> current,
            Func<AddressableAssetSettings, bool> ok, string expected)
        {
            VisualElement row = new();
            row.AddToClassList("settings-item");

            Item item = new() { Icon = new VisualElement(), Current = current, Ok = ok, Expected = expected };
            item.Icon.AddToClassList("row-icon");
            row.Add(item.Icon);
            row.Add(new Label(name) { style = { flexShrink = 0, marginRight = 4 } });
            if (current != null)
            {
                item.Value = new Label { style = { whiteSpace = WhiteSpace.Normal } }.WithClass("settings-value");
                row.Add(item.Value);
            }

            section.Add(row);
            return item;
        }

        private void Refresh()
        {
            if (items == null)
                return;

            settings ??= AddressablesConfiguration.GetOrCreateSettings();

            int toFix = 0;
            foreach (Item item in items)
            {
                bool ok = settings != null && item.Ok(settings);
                AddressablesManagementWindow.SetIcon(item.Icon, ok ? AddressablesManagementWindow.check_icons : AddressablesManagementWindow.warning_icons);
                item.Icon.tooltip = (ok ? "As the SDK expects. " : "Not what the SDK expects. ") + item.Expected;

                if (item.Value != null)
                {
                    item.Value.text = (settings != null ? item.Current(settings) : null) ?? "(missing)";
                    item.Value.tooltip = item.Icon.tooltip;
                }

                if (!ok)
                    toFix++;
            }

            // The banner follows the check the build makes, so the two never disagree.
            bool configured = AddressablesConfiguration.IsConfiguredFor(settings);
            if (!configured && toFix == 0)
                toFix = 1;

            fixBanner.style.display = configured ? DisplayStyle.None : DisplayStyle.Flex;
            summary.text = toFix == 1 ? "1 setting is not the one the SDK needs." : $"{toFix} settings are not the ones the SDK needs.";
        }
    }

    internal static class VisualElementClassExtensions
    {
        /// <summary>Adds a USS class and returns the element, for initializers.</summary>
        internal static T WithClass<T>(this T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
