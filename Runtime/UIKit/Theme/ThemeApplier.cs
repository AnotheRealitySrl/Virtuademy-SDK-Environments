using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Writes the published tenant colours onto a visual tree, and takes them off again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stylesheets colour every role through <c>var(--vd-color-*)</c>, which is all a panel needs
    /// when nobody publishes a palette. A palette arrives as data at runtime, and USS variables cannot
    /// be set from code, so the published roles go on as inline styles, on exactly the elements whose
    /// classes take that role (<see cref="ThemeTokens"/>). Inline beats the stylesheet, which is the
    /// point; it also beats a <c>:hover</c> colour, which is why the SDK's hover states change scale
    /// and borders rather than the background.
    /// </para>
    /// <para>
    /// A role that is not published is cleared back to <see cref="StyleKeyword.Null"/>, so the
    /// stylesheet value shows again: applying after <see cref="VirtuademyThemeRuntime.Clear"/>
    /// restores the SDK look.
    /// </para>
    /// </remarks>
    public static class ThemeApplier
    {
        /// <summary>Applies the current palette to <paramref name="root"/> and every descendant.</summary>
        public static void Apply(VisualElement root)
        {
            if (root == null)
            {
                return;
            }
            ApplyTo(root);
            root.Query<VisualElement>().ForEach(ApplyTo);
        }

        private static void ApplyTo(VisualElement element)
        {
            // Utility classes first, component rules after: a component rule is the more specific intent.
            foreach (EThemeRole role in ThemeTokens.Roles)
            {
                if (element.ClassListContains(ThemeTokens.BackgroundClass(role)))
                {
                    Set(element, ThemeTokens.Target.Background, role);
                }
                if (element.ClassListContains(ThemeTokens.TextClass(role)))
                {
                    Set(element, ThemeTokens.Target.Text, role);
                }
                if (element.ClassListContains(ThemeTokens.BorderClass(role)))
                {
                    Set(element, ThemeTokens.Target.Border, role);
                }
            }
            foreach (ThemeTokens.ComponentRule rule in ThemeTokens.ComponentRules)
            {
                if (element.ClassListContains(rule.ClassName))
                {
                    Set(element, rule.Target, rule.Role);
                }
            }
        }

        private static void Set(VisualElement element, ThemeTokens.Target target, EThemeRole role)
        {
            StyleColor value = VirtuademyThemeRuntime.TryGetOverride(role, out Color color)
                ? new StyleColor(color)
                : new StyleColor(StyleKeyword.Null);
            IStyle style = element.style;
            switch (target)
            {
                case ThemeTokens.Target.Background:
                    style.backgroundColor = value;
                    break;
                case ThemeTokens.Target.Text:
                    style.color = value;
                    style.unityBackgroundImageTintColor = value;
                    break;
                case ThemeTokens.Target.Border:
                    style.borderTopColor = value;
                    style.borderRightColor = value;
                    style.borderBottomColor = value;
                    style.borderLeftColor = value;
                    break;
                case ThemeTokens.Target.BorderLeft:
                    style.borderLeftColor = value;
                    break;
            }
        }
    }
}
