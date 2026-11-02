using System.Collections.Generic;
using System.IO;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Virtuademy.SDK.Environments.Editor.UIKit
{
    /// <summary>
    /// The SDK's Poppins for TextMesh Pro: the dynamic font assets that back the two static ones with
    /// every glyph Poppins has, and the TextMesh Pro Essential Resources a creator project needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The static <c>Poppins-Regular SDF</c> and <c>Poppins-Bold SDF</c> hold 211 Latin glyphs each,
    /// with no fallback: an accented letter outside them (Polish, Czech, Romanian, Turkish…) renders
    /// as a box. <c>Virtuademy/Advanced/Developer/Create Poppins font fallbacks</c> (enabled only where the package
    /// is embedded) creates
    /// <c>Poppins-Regular Dynamic SDF</c>, <c>Poppins-SemiBold SDF</c> and
    /// <c>Poppins-Bold Dynamic SDF</c> beside them (dynamic atlases cleared on build, so the package
    /// stays small) and sets the dynamic ones as the static ones' fallback. Run it once in a checkout
    /// where the package is embedded, then commit the new assets (not done yet: until then the static assets have no
    /// fallback). UI Toolkit panels do not need it:
    /// they use the font files directly, with every glyph.
    /// </para>
    /// <para>
    /// Scripts Poppins does not cover (Cyrillic, Greek, Arabic, CJK) still need a font asset of their
    /// own in the fallback list.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class VirtuademyFontSetup
    {
        private const string Fonts = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/Common/Fonts/";
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string EssentialsSessionKey = "Virtuademy.TmpEssentialsChecked";

        static VirtuademyFontSetup()
        {
            // Not in batch mode (review S5): an EnvironmentsCli build or deploy would import a package, and maybe reload
            // the domain, in the middle of its run. The setup's configure step imports them there.
            if (Application.isBatchMode)
            {
                return;
            }
            // Without the Essential Resources (TMP Settings and the TextMesh Pro shaders) every SDK
            // text renders magenta in a creator project. Import them once, quietly, the way the setup
            // would: TextMesh Pro's own prompt only appears when a TMP component is created.
            if (SessionState.GetBool(EssentialsSessionKey, false))
            {
                return;
            }
            SessionState.SetBool(EssentialsSessionKey, true);
            EditorApplication.delayCall += ImportEssentialsIfMissing;
        }

        private static void ImportEssentialsIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return;
            }
            if (!File.Exists(Path.GetFullPath(EssentialsPackage)))
            {
                Debug.LogWarning("[Fonts] TextMesh Pro Essential Resources are missing and their package was not found " +
                    $"at {EssentialsPackage}: import them from Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }
            Debug.Log("[Fonts] Importing TextMesh Pro Essential Resources: the SDK's texts need them.");
            AssetDatabase.ImportPackage(EssentialsPackage, false);
        }


        /// <summary>
        /// Whether this package is embedded (the SDK's own checkout), the only place the developer menus can write: in a
        /// creator project it comes from the registry and is read-only (review S4).
        /// </summary>
        internal static bool PackageIsEmbedded()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtuademyFontSetup).Assembly);
            return package != null && package.source == UnityEditor.PackageManager.PackageSource.Embedded;
        }

        [MenuItem("Virtuademy/Advanced/Developer/Create Poppins font fallbacks", true)]
        private static bool CanCreateFallbacks() => PackageIsEmbedded();

        [MenuItem("Virtuademy/Advanced/Developer/Create Poppins font fallbacks", false, 100)]
        private static void CreateFallbacks()
        {
            TMP_FontAsset regular = Dynamic("Poppins-Regular.ttf", "Poppins-Regular Dynamic SDF.asset");
            TMP_FontAsset semiBold = Dynamic("Poppins-SemiBold.ttf", "Poppins-SemiBold SDF.asset");
            TMP_FontAsset bold = Dynamic("Poppins-Bold.ttf", "Poppins-Bold Dynamic SDF.asset");

            AddFallback("Poppins-Regular SDF.asset", regular);
            AddFallback("Poppins-Bold SDF.asset", bold);
            // SemiBold is dynamic itself: it needs no fallback for Poppins' own glyphs.
            if (semiBold == null)
            {
                Debug.LogWarning("[Fonts] Poppins-SemiBold SDF was not created.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Fonts] Poppins fallbacks ready. Commit the new font assets in Common/Fonts.");
        }

        private static TMP_FontAsset Dynamic(string fontFile, string assetName)
        {
            string path = Fonts + assetName;
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                return existing;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + fontFile);
            if (font == null)
            {
                Debug.LogError($"[Fonts] {Fonts + fontFile} not found.");
                return null;
            }
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetName);
            AssetDatabase.CreateAsset(asset, path);
            // Not public API: the inspector's "Clear Dynamic Data On Build", so the package ships an
            // empty atlas that fills in at runtime.
            var serialized = new SerializedObject(asset);
            SerializedProperty clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null)
            {
                clear.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AddFallback(string assetName, TMP_FontAsset fallback)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + assetName);
            if (asset == null || fallback == null || asset == fallback)
            {
                return;
            }
            asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!asset.fallbackFontAssetTable.Contains(fallback))
            {
                asset.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
