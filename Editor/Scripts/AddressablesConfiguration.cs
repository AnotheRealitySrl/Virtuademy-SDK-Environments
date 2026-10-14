using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The Addressables configuration the SDK's publish pipeline expects, and the means to check and
    /// apply it without opening <see cref="AddressablesManagementWindow"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A project that has never been through the window keeps Unity's defaults: the stock profile
    /// and the stock groups. A build then runs to the end and fails only afterwards, because the
    /// bundles land in <c>Library/com.unity.addressables</c> instead of <c>ServerData</c>. The
    /// window's "Fix Addressables configurations" button and the setup command line's
    /// <c>addressables</c> step both call <see cref="Apply"/>; <see cref="IsConfigured"/> is the
    /// check the build buttons and the setup report read.
    /// </para>
    /// <para>
    /// <b><see cref="Apply"/> resets the Addressables groups</b>: it removes every group but the
    /// default one and every entry of that group, and sets the default group's schema to the SDK's
    /// values. The build adds the scenes it publishes itself, so a creator's own groups and entries
    /// are not part of the flow, but nothing here asks before deleting them.
    /// </para>
    /// </remarks>
    public static class AddressablesConfiguration
    {
        /// <summary>The folder, relative to the project, a build writes the bundles to.</summary>
        public const string OutputFolder = "ServerData";

        /// <summary>The profile variable the groups and the remote catalog write to.</summary>
        public const string RemoteBuildPathVariableName = "Remote.BuildPath";

        /// <summary>The profile variable the groups and the remote catalog load from.</summary>
        public const string RemoteLoadPathVariableName = "Remote.LoadPath";

        /// <summary>The profile variable holding the active build target.</summary>
        public const string BuildTargetVariableName = "BuildTarget";

        /// <summary>The value of <see cref="BuildTargetVariableName"/>: the active build target, evaluated at build time.</summary>
        public const string BuildTargetVariableValue = "[UnityEditor.EditorUserBuildSettings.activeBuildTarget]";

        /// <summary>The profile variable holding the player version override, which names the scene's folder.</summary>
        public const string PlayerVersionOverrideVariableName = "PlayerVersionOverride";

        /// <summary>The value of <see cref="PlayerVersionOverrideVariableName"/>, read from <see cref="AddressablesBuildScript"/>.</summary>
        public const string PlayerVersionOverrideVariableValue = "[Virtuademy.SDK.Environments.Editor.AddressablesBuildScript.PlayerVersionOverride]";

        /// <summary>
        /// What <see cref="RemoteBuildPathVariableName"/> must hold:
        /// <c>ServerData/[PlayerVersionOverride]/[BuildTarget]</c>.
        /// </summary>
        public static string RemoteBuildPath { get; } = string.Join('/',
            OutputFolder,
            BuildtimeVariable(PlayerVersionOverrideVariableName),
            BuildtimeVariable(BuildTargetVariableName));

        /// <summary>
        /// What <see cref="RemoteLoadPathVariableName"/> must hold. It names its two runtime variables by
        /// <see cref="CatalogVariables"/>' constants, which are deliberately not C# types: a catalog must
        /// not depend on an identifier that can move.
        /// </summary>
        public static string RemoteLoadPath { get; } = string.Join('/',
            CatalogVariables.Placeholder(CatalogVariables.BaseUrlName),
            CatalogVariables.Placeholder(CatalogVariables.WorldIdName),
            BuildtimeVariable(PlayerVersionOverrideVariableName),
            BuildtimeVariable(BuildTargetVariableName));

        /// <summary>
        /// True when the project's Addressables settings, active profile and groups are all as the SDK
        /// expects. False when the project has no Addressables settings at all. Does not create or change
        /// anything.
        /// </summary>
        public static bool IsConfigured
        {
            get
            {
                AddressableAssetSettings settings = FindSettings();
                return settings != null && IsConfiguredFor(settings);
            }
        }

        /// <summary>
        /// Puts the project's Addressables in the configuration the SDK expects, and saves it: creates the
        /// settings if the project has none, then sets the top-level settings, the active profile and the
        /// default group. Does nothing when <see cref="IsConfigured"/> already holds. Safe to call again.
        /// </summary>
        /// <exception cref="InvalidOperationException">The project does not end up configured.</exception>
        public static void Apply()
        {
            AddressableAssetSettings settings = GetOrCreateSettings();
            if (IsConfiguredFor(settings))
                return;

            ConfigureSettings(settings);
            ConfigureProfile(settings);
            ConfigureGroups(settings);
            AssetDatabase.SaveAssets();

            if (!IsConfiguredFor(settings))
                throw new InvalidOperationException(
                    "The Addressables configuration was applied but the project still does not match it. " +
                    "Open Virtuademy > Addressables management and look at the red items under " +
                    "'Show advanced addressables configurations'.");
        }

        /// <summary>
        /// The project's Addressables settings, created with Unity's defaults when it has none.
        /// </summary>
        public static AddressableAssetSettings GetOrCreateSettings()
        {
            AddressableAssetSettings settings = FindSettings();
            if (settings != null)
                return settings;

            AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
            return FindSettings();
        }

        /// <summary>
        /// The settings asset at its default path, or null when the project has none. Unlike
        /// <see cref="AddressablesBuildScript.GetSettingsObject"/> it does not log an error.
        /// </summary>
        private static AddressableAssetSettings FindSettings() =>
            AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(AddressablesBuildScript.settings_asset);

        private static string BuildtimeVariable(string variable) => "[" + variable + "]";

        private static void SaveAsset(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        /// <summary>The value of a variable in the active profile, or null when the profile has no such variable.</summary>
        internal static string GetProfileValue(AddressableAssetSettings settings, string variableName) =>
            settings.profileSettings.GetValueByName(settings.activeProfileId, variableName);

        internal static bool IsConfiguredFor(AddressableAssetSettings settings) =>
            IsSettingsConfigured(settings) && IsProfileConfigured(settings) && AreGroupsConfigured(settings);

        #region Top-level settings

        internal static bool IsSettingsConfigured(AddressableAssetSettings settings) =>
                 settings.RemoteCatalogLoadPath.GetName(settings) == RemoteLoadPathVariableName &&
                 settings.RemoteCatalogBuildPath.GetName(settings) == RemoteBuildPathVariableName &&
                 settings.BuildRemoteCatalog &&
                 settings.EnableJsonCatalog &&
                 settings.CheckForContentUpdateRestrictionsOption
                         == CheckForContentUpdateRestrictionsOptions.ListUpdatedAssetsWithRestrictions &&
                 settings.MaxConcurrentWebRequests == 3 &&
                 settings.CatalogRequestsTimeout == 0 &&
                 !settings.IgnoreUnsupportedFilesInBuild &&
                 !settings.UniqueBundleIds &&
                 settings.ContiguousBundles &&
                 settings.NonRecursiveBuilding &&
                 settings.BuiltInBundleNaming == BuiltInBundleNaming.Custom &&
                 settings.MonoScriptBundleNaming == MonoScriptBundleNaming.Custom &&
                 !settings.DisableVisibleSubAssetRepresentations;

        private static void ConfigureSettings(AddressableAssetSettings settings)
        {
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, RemoteLoadPathVariableName);
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, RemoteBuildPathVariableName);
            settings.BuildRemoteCatalog = true;
            settings.EnableJsonCatalog = true;
            settings.CheckForContentUpdateRestrictionsOption = CheckForContentUpdateRestrictionsOptions.ListUpdatedAssetsWithRestrictions;
            settings.ContentStateBuildPath = string.Empty;
            settings.MaxConcurrentWebRequests = 3;
            settings.CatalogRequestsTimeout = 0;
            settings.IgnoreUnsupportedFilesInBuild = false;
            settings.UniqueBundleIds = false;
            settings.ContiguousBundles = true;
            settings.NonRecursiveBuilding = true;
            settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            settings.DisableVisibleSubAssetRepresentations = false;
            settings.BuildRemoteCatalog = true;
            SaveAsset(settings);
        }

        #endregion

        #region Profile

        internal static bool IsProfileConfigured(AddressableAssetSettings settings) =>
            GetProfileValue(settings, RemoteBuildPathVariableName) == RemoteBuildPath
                && GetProfileValue(settings, RemoteLoadPathVariableName) == RemoteLoadPath
                && GetProfileValue(settings, BuildTargetVariableName) == BuildTargetVariableValue
                && GetProfileValue(settings, PlayerVersionOverrideVariableName) == PlayerVersionOverrideVariableValue;

        private static void ConfigureProfile(AddressableAssetSettings settings)
        {
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteBuildPathVariableName, RemoteBuildPath);
            settings.profileSettings.SetValue(settings.activeProfileId, RemoteLoadPathVariableName, RemoteLoadPath);

            if (GetProfileValue(settings, BuildTargetVariableName) == null)
                settings.profileSettings.CreateValue(BuildTargetVariableName, BuildTargetVariableValue);
            else
                settings.profileSettings.SetValue(settings.activeProfileId, BuildTargetVariableName, BuildTargetVariableValue);

            if (GetProfileValue(settings, PlayerVersionOverrideVariableName) == null)
                settings.profileSettings.CreateValue(PlayerVersionOverrideVariableName, PlayerVersionOverrideVariableValue);
            else
                settings.profileSettings.SetValue(settings.activeProfileId, PlayerVersionOverrideVariableName, PlayerVersionOverrideVariableValue);

            SaveAsset(settings);
        }

        #endregion

        #region Groups

        internal static bool AreGroupsConfigured(AddressableAssetSettings settings)
        {
            AddressableAssetGroup defaultGroup = settings.DefaultGroup;
            if (defaultGroup == null)
                return false;

            bool configured = true;

            configured &= !settings.groups.Where(group => group != defaultGroup).Any();
            configured &= !defaultGroup.entries.Any();

            defaultGroup.Schemas.Where(schema => schema is BundledAssetGroupSchema).ToList().ForEach(schema =>
            {
                BundledAssetGroupSchema b = schema as BundledAssetGroupSchema;
                configured &=
                    b.LoadPath.GetName(settings) == RemoteLoadPathVariableName &&
                    b.BuildPath.GetName(settings) == RemoteBuildPathVariableName &&
                    b.Compression == BundledAssetGroupSchema.BundleCompressionMode.LZ4 &&
                    b.IncludeInBuild &&
                    !b.ForceUniqueProvider &&
                    b.UseAssetBundleCache &&
                    !b.UseAssetBundleCrc &&
                    !b.UseAssetBundleCrcForCachedBundles &&
                    !b.UseUnityWebRequestForLocalBundles &&
                    b.Timeout == 0 &&
                    !b.ChunkedTransfer &&
                    b.RedirectLimit == -1 &&
                    b.RetryCount == 0 &&
                    b.IncludeAddressInCatalog &&
                    b.IncludeGUIDInCatalog &&
                    b.IncludeLabelsInCatalog &&
                    b.InternalIdNamingMode == BundledAssetGroupSchema.AssetNamingMode.FullPath &&
                    b.InternalBundleIdMode == BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash &&
                    b.AssetBundledCacheClearBehavior == BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded &&
                    b.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately &&
                    b.BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash;
            });

            return configured;
        }

        private static void ConfigureGroups(AddressableAssetSettings settings)
        {
            AddressableAssetGroup defaultGroup = settings.DefaultGroup;
            if (defaultGroup == null)
                throw new InvalidOperationException("The project's Addressables settings have no default group to configure.");

            var groupsToDelete = settings.groups.Where(group => group != defaultGroup).ToList();
            foreach (var group in groupsToDelete)
                settings.RemoveGroup(group);

            defaultGroup.Schemas.Where(schema => schema is BundledAssetGroupSchema).ToList().ForEach(schema =>
            {
                BundledAssetGroupSchema b = schema as BundledAssetGroupSchema;
                b.LoadPath.SetVariableByName(settings, RemoteLoadPathVariableName);
                b.BuildPath.SetVariableByName(settings, RemoteBuildPathVariableName);
                b.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                b.IncludeInBuild = true;
                b.ForceUniqueProvider = false;
                b.UseAssetBundleCache = true;
                b.UseAssetBundleCrc = false;
                b.UseAssetBundleCrcForCachedBundles = false;
                b.UseUnityWebRequestForLocalBundles = false;
                b.Timeout = 0;
                b.ChunkedTransfer = false;
                b.RedirectLimit = -1;
                b.RetryCount = 0;
                b.IncludeAddressInCatalog = true;
                b.IncludeGUIDInCatalog = true;
                b.IncludeLabelsInCatalog = true;
                b.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.FullPath;
                b.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;
                b.AssetBundledCacheClearBehavior = BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded;
                b.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                b.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                SaveAsset(b);
            });

            var entriesToRemove = defaultGroup.entries.ToList();
            foreach (var entry in entriesToRemove)
                settings.RemoveAssetEntry(entry.guid);

            SaveAsset(settings);
            SaveAsset(defaultGroup);
            SaveAsset(settings);
        }

        #endregion
    }
}
