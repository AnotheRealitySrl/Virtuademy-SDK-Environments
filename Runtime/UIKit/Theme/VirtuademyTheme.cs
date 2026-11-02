using System;

using TMPro;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// The source of the Virtuademy theme: colour roles, type scale, radii and spacing. The SDK's
    /// <c>VirtuademyTokens.uss</c> is generated from the asset that ships in
    /// <c>Common/Theme/</c> (<c>Virtuademy/UI/Generate theme tokens</c>), and <see cref="ThemeColorRole"/>
    /// reads it to colour uGUI graphics by role.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The defaults written here are the values of that asset, so code that has no asset at hand
    /// (the runtime fallback of <see cref="VirtuademyThemeRuntime"/>) agrees with it. Change both
    /// together, then regenerate the tokens.
    /// </para>
    /// <para>
    /// Sizes are in panel pixels. A screen panel is laid out at 1920×1080 and scaled to the screen;
    /// a world panel of the SDK's world PanelSettings draws one pixel per millimetre.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Virtuademy/UI/Theme", fileName = "VirtuademyTheme")]
    public class VirtuademyTheme : ScriptableObject
    {
        [Serializable]
        public struct ColorRoles
        {
            public Color surface;
            public Color surfaceRaised;
            public Color border;
            public Color text;
            public Color textSecondary;
            public Color action;
            public Color onAction;
            public Color success;
            public Color warning;
            public Color error;
            public Color info;
            public Color scrim;
        }

        [Serializable]
        public struct TypeScale
        {
            [Tooltip("Captions, labels under icons.")] public float caption;
            [Tooltip("Body text.")] public float body;
            [Tooltip("Panel subtitles, list headings.")] public float subtitle;
            [Tooltip("Panel titles.")] public float title;
            [Tooltip("Big numbers and welcome headings.")] public float display;
        }

        [Serializable]
        public struct Fonts
        {
            [Tooltip("UI Toolkit: the font file of each weight.")]
            public Font regular;
            public Font semiBold;
            public Font bold;

            [Tooltip("uGUI / TextMesh Pro: the font asset of each weight.")]
            public TMP_FontAsset tmpRegular;
            public TMP_FontAsset tmpSemiBold;
            public TMP_FontAsset tmpBold;
        }

        [Serializable]
        public struct Metrics
        {
            public float radiusSmall;
            public float radiusMedium;
            public float radiusLarge;
            public float spaceXS;
            public float spaceS;
            public float spaceM;
            public float spaceL;
            public float spaceXL;
            public float borderWidth;

            [Tooltip("Seconds of the fade in and out of popups and toasts.")]
            public float motionFade;
        }

        public static readonly ColorRoles DefaultColors = new()
        {
            surface = new Color32(18, 20, 26, 235),
            surfaceRaised = new Color32(36, 40, 50, 255),
            border = new Color32(255, 255, 255, 31),
            text = new Color32(255, 255, 255, 255),
            textSecondary = new Color32(184, 188, 198, 255),
            // Decided for 2026.7: the blue of the new Virtuademy brand.
            action = new Color32(0, 110, 245, 255),
            onAction = new Color32(255, 255, 255, 255),
            // The green, orange and red of the SDK's uGUI panels, kept so old and new panels agree.
            success = new Color32(0, 195, 138, 255),
            warning = new Color32(219, 127, 60, 255),
            error = new Color32(209, 72, 88, 255),
            info = new Color32(90, 169, 255, 255),
            scrim = new Color32(0, 0, 0, 153),
        };

        public static readonly TypeScale DefaultTypeScale = new()
        {
            caption = 22,
            body = 28,
            subtitle = 34,
            title = 44,
            display = 60,
        };

        public static readonly Metrics DefaultMetrics = new()
        {
            radiusSmall = 8,
            radiusMedium = 16,
            radiusLarge = 28,
            spaceXS = 4,
            spaceS = 8,
            spaceM = 16,
            spaceL = 24,
            spaceXL = 40,
            borderWidth = 2,
            motionFade = 0.2f,
        };

        [SerializeField] private ColorRoles colors = DefaultColors;
        [SerializeField] private TypeScale typeScale = DefaultTypeScale;
        [SerializeField] private Fonts fonts;
        [SerializeField] private Metrics metrics = DefaultMetrics;

        public ColorRoles Colors => colors;
        public TypeScale Type => typeScale;
        public Fonts FontSet => fonts;
        public Metrics Sizes => metrics;

        /// <summary>The colour this asset gives <paramref name="role"/>.</summary>
        public Color GetColor(EThemeRole role) => GetColor(colors, role);

        /// <summary>The colour <paramref name="set"/> gives <paramref name="role"/>.</summary>
        public static Color GetColor(ColorRoles set, EThemeRole role) => role switch
        {
            EThemeRole.Surface => set.surface,
            EThemeRole.SurfaceRaised => set.surfaceRaised,
            EThemeRole.Border => set.border,
            EThemeRole.Text => set.text,
            EThemeRole.TextSecondary => set.textSecondary,
            EThemeRole.Action => set.action,
            EThemeRole.OnAction => set.onAction,
            EThemeRole.Success => set.success,
            EThemeRole.Warning => set.warning,
            EThemeRole.Error => set.error,
            EThemeRole.Info => set.info,
            EThemeRole.Scrim => set.scrim,
            _ => Color.magenta,
        };
    }
}
