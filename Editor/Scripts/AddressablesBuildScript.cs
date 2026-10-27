using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    public static class AddressablesBuildScript
    {
        public static string build_script
            = "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset";
        public static string settings_asset
            = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";

        private static AddressableAssetSettings settings;

        public static string PlayerVersionOverride { get; private set; }

        public static AddressableAssetSettings GetSettingsObject(string settingsAsset)
        {
            // This step is optional, you can also use the default settings:
            //settings = AddressableAssetSettingsDefaultObject.Settings;

            settings
                = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsAsset)
                as AddressableAssetSettings;

            if (settings == null)
                Debug.LogError($"{settingsAsset} couldn't be found or isn't " +
                               $"a settings object.");
            return settings;
        }

        static void SetProfile(string profile)
        {
            string profileId = settings.profileSettings.GetProfileId(profile);
            if (string.IsNullOrEmpty(profileId))
                Debug.LogWarning($"Couldn't find a profile named, {profile}, " +
                                 $"using current profile instead.");
            else
                settings.activeProfileId = profileId;
        }

        static void SetBuilder(IDataBuilder builder)
        {
            int index = settings.DataBuilders.IndexOf((ScriptableObject)builder);

            if (index > 0)
                settings.ActivePlayerDataBuilderIndex = index;
            else
                Debug.LogWarning($"{builder} must be added to the " +
                                 $"DataBuilders list before it can be made " +
                                 $"active. Using last run builder instead.");
        }

        /// <summary>
        /// Addressables asks, in a modal dialog, whether to turn on "Debug Build Layout" for its Build Report, once per
        /// project, the first time content is built from the editor (never in batch mode). The publish pipeline does not use
        /// the report, and a dialog left behind the editor stops the build until someone finds it. Recording that the user
        /// was informed skips it; "Debug Build Layout" itself stays as it is (Edit &gt; Preferences &gt; Addressables).
        /// The flag is internal to Addressables 2.x, hence the reflection: when it is not found, the dialog may appear.
        /// </summary>
        static void SkipBuildReportPrompt()
        {
            System.Reflection.PropertyInfo informed = typeof(ProjectConfigData).GetProperty(
                "UserHasBeenInformedAboutBuildReportSettingPreBuild",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (informed == null || !informed.CanWrite)
                return;

            if (informed.GetValue(null) is bool done && !done)
                informed.SetValue(null, true);
        }

        static bool BuildAddressableContent()
        {
            SkipBuildReportPrompt();

            AddressableAssetSettings
                .BuildPlayerContent(out AddressablesPlayerBuildResult result);
            bool success = string.IsNullOrEmpty(result.Error);

            if (!success)
            {
                Debug.LogError("[PublishEnvironments] The build failed: " + result.Error);
            }
            return success;
        }

        public static bool BuildAddressables()
        {
            if (GetSettingsObject(settings_asset) == null)
                return false;

            // Addressables builds the settings registered as the project's default, not the asset loaded above. A
            // project whose settings asset is not registered (EditorBuildSettings has no config object for it) made
            // every build fail with "Addressable Asset Settings does not exist": register it.
            if (AddressableAssetSettingsDefaultObject.Settings != settings)
                AddressableAssetSettingsDefaultObject.Settings = settings;

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(build_script) is not IDataBuilder builderScript)
            {
                Debug.LogError(build_script + " couldn't be found or isn't a build script.");
                return false;
            }

            SetBuilder(builderScript);

            PlayerVersionOverride = settings.OverridePlayerVersion;

            return BuildAddressableContent();
        }

        /// <summary>
        /// Builds the Addressables for Android, WebGL and Windows in turn. Stops at the first target
        /// that cannot be switched to or fails to build, and switches back to the editor's original
        /// build target whatever happens: it ignored both results before, so a failed platform left
        /// a previous build's output in place and the editor on whichever target came last.
        /// </summary>
        public static void BuildAddressablesForAllPlatforms()
        {
            (BuildTargetGroup group, BuildTarget target)[] targets =
            {
                (BuildTargetGroup.Android, BuildTarget.Android),
                (BuildTargetGroup.WebGL, BuildTarget.WebGL),
                (BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64),
            };

            BuildTarget originalTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup originalGroup = BuildPipeline.GetBuildTargetGroup(originalTarget);

            try
            {
                foreach ((BuildTargetGroup group, BuildTarget target) in targets)
                {
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                    {
                        Debug.LogError($"[PublishEnvironments] Could not switch the active build target to {target}: the remaining platforms were not built.");
                        return;
                    }

                    if (!BuildAddressables())
                    {
                        Debug.LogError($"[PublishEnvironments] The build failed for {target} (see the errors above): the remaining platforms were not built.");
                        return;
                    }
                }
            }
            finally
            {
                if (EditorUserBuildSettings.activeBuildTarget != originalTarget
                    && !EditorUserBuildSettings.SwitchActiveBuildTarget(originalGroup, originalTarget))
                {
                    Debug.LogWarning($"[PublishEnvironments] Could not switch the active build target back to {originalTarget}.");
                }
            }
        }
    }
}
