using System.Globalization;
using System.IO;
using System.Text;

using UnityEditor;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.UI;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Editor.UIKit
{
    /// <summary>
    /// Writes the <c>--vd-*</c> tokens USS from a <see cref="VirtuademyTheme"/> asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Virtuademy/UI/Generate theme tokens</c> regenerates the SDK's own
    /// <c>Common/Theme/VirtuademyTokens.uss</c> from <c>Common/Theme/VirtuademyTheme.asset</c>: run it
    /// in a checkout where the package is embedded (a creator project gets the package read-only).
    /// </para>
    /// <para>
    /// The context menu of any theme asset (<c>Generate tokens USS</c>) writes
    /// <c>&lt;asset name&gt;Tokens.uss</c> beside it, with the same variables under the class
    /// <c>vd-root</c>: import it after the SDK sheets to restyle a panel of your own.
    /// </para>
    /// </remarks>
    public static class VirtuademyThemeTokensGenerator
    {
        private const string PackageRoot = "Packages/com.anotherealitysrl.virtuademy-sdk-environments/";
        private const string SdkTheme = PackageRoot + "Common/Theme/VirtuademyTheme.asset";
        private const string SdkTokens = PackageRoot + "Common/Theme/VirtuademyTokens.uss";

        [MenuItem("Virtuademy/UI/Generate theme tokens")]
        private static void GenerateSdkTokens()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VirtuademyTheme>(SdkTheme);
            if (theme == null)
            {
                Debug.LogError($"[Theme] {SdkTheme} not found.");
                return;
            }
            Write(theme, SdkTokens, ":root, .vd-root");
        }

        [MenuItem("CONTEXT/VirtuademyTheme/Generate tokens USS")]
        private static void GenerateFromContext(MenuCommand command)
        {
            var theme = (VirtuademyTheme)command.context;
            string assetPath = AssetDatabase.GetAssetPath(theme);
            string ussPath = Path.Combine(Path.GetDirectoryName(assetPath) ?? "Assets", theme.name + "Tokens.uss")
                .Replace('\\', '/');
            Write(theme, ussPath, ".vd-root");
        }

        /// <summary>The tokens USS for <paramref name="theme"/>, under <paramref name="selector"/>.</summary>
        public static string Build(VirtuademyTheme theme, string selector)
        {
            var css = new StringBuilder();
            css.AppendLine($"/* Generated from {theme.name}.asset by Virtuademy/UI/Generate theme tokens. Do not edit by hand:");
            css.AppendLine("   change the asset and generate again.");
            css.AppendLine();
            css.AppendLine("   The Virtuademy theme tokens. Every SDK panel imports this sheet first and reads its colours,");
            css.AppendLine("   type scale, radii and spacing from these variables. Override a variable on a panel's own root");
            css.AppendLine("   to restyle that panel; the tenant's colours, when the application publishes them, are applied");
            css.AppendLine("   at runtime by ThemedDocument on top of these values. */");
            css.AppendLine();
            css.AppendLine(selector + " {");
            foreach (EThemeRole role in ThemeTokens.Roles)
            {
                css.AppendLine($"    {ThemeTokens.ColorVariable(role)}: {Css(theme.GetColor(role))};");
            }
            css.AppendLine();
            AppendFont(css, "--vd-font-regular", theme.FontSet.regular);
            AppendFont(css, "--vd-font-semibold", theme.FontSet.semiBold);
            AppendFont(css, "--vd-font-bold", theme.FontSet.bold);
            css.AppendLine();
            css.AppendLine($"    --vd-font-size-caption: {Px(theme.Type.caption)};");
            css.AppendLine($"    --vd-font-size-body: {Px(theme.Type.body)};");
            css.AppendLine($"    --vd-font-size-subtitle: {Px(theme.Type.subtitle)};");
            css.AppendLine($"    --vd-font-size-title: {Px(theme.Type.title)};");
            css.AppendLine($"    --vd-font-size-display: {Px(theme.Type.display)};");
            css.AppendLine();
            css.AppendLine($"    --vd-radius-s: {Px(theme.Sizes.radiusSmall)};");
            css.AppendLine($"    --vd-radius-m: {Px(theme.Sizes.radiusMedium)};");
            css.AppendLine($"    --vd-radius-l: {Px(theme.Sizes.radiusLarge)};");
            css.AppendLine();
            css.AppendLine($"    --vd-space-xs: {Px(theme.Sizes.spaceXS)};");
            css.AppendLine($"    --vd-space-s: {Px(theme.Sizes.spaceS)};");
            css.AppendLine($"    --vd-space-m: {Px(theme.Sizes.spaceM)};");
            css.AppendLine($"    --vd-space-l: {Px(theme.Sizes.spaceL)};");
            css.AppendLine($"    --vd-space-xl: {Px(theme.Sizes.spaceXL)};");
            css.AppendLine();
            css.AppendLine($"    --vd-border-width: {Px(theme.Sizes.borderWidth)};");
            css.AppendLine();
            css.AppendLine($"    --vd-motion-fade: {theme.Sizes.motionFade.ToString("0.###", CultureInfo.InvariantCulture)}s;");
            css.AppendLine("}");
            return css.ToString();
        }

        private static void Write(VirtuademyTheme theme, string path, string selector)
        {
            File.WriteAllText(path, Build(theme, selector).Replace("\r\n", "\n"));
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[Theme] Wrote {path} from {AssetDatabase.GetAssetPath(theme)}.");
        }

        private static void AppendFont(StringBuilder css, string variable, Font font)
        {
            if (font == null)
            {
                return;
            }
            string path = AssetDatabase.GetAssetPath(font);
            string guid = AssetDatabase.AssetPathToGUID(path);
            css.AppendLine($"    {variable}: url(\"project://database/{path}?fileID=12800000&guid={guid}&type=3#{font.name}\");");
        }

        private static string Px(float value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "px";

        private static string Css(Color color)
        {
            Color32 c = color;
            if (c.a == 255)
            {
                return $"rgb({c.r}, {c.g}, {c.b})";
            }
            string alpha = (c.a / 255f).ToString("0.###", CultureInfo.InvariantCulture);
            return $"rgba({c.r}, {c.g}, {c.b}, {alpha})";
        }
    }
}
