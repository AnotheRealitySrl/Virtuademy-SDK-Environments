using System;
using System.Collections.Generic;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// The names the theme is addressed by: USS variables and the classes that colour an element by
    /// role. One place, so the token generator, the runtime applier and the docs cannot drift.
    /// </summary>
    public static class ThemeTokens
    {
        /// <summary>Class of the top element of every SDK panel template: the tokens are defined on it.</summary>
        public const string RootClass = "vd-root";

        /// <summary>Class of the element <see cref="SafeFrame"/> insets on a screen panel.</summary>
        public const string SafeFrameClass = "vd-safe-frame";

        private static readonly EThemeRole[] roles = (EThemeRole[])Enum.GetValues(typeof(EThemeRole));

        /// <summary>Every role, in declaration order.</summary>
        public static IReadOnlyList<EThemeRole> Roles => roles;

        /// <summary><c>Surface</c> → <c>surface</c>, <c>TextSecondary</c> → <c>text-secondary</c>.</summary>
        public static string Kebab(EThemeRole role)
        {
            string name = role.ToString();
            var chars = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        chars.Append('-');
                    }
                    chars.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    chars.Append(c);
                }
            }
            return chars.ToString();
        }

        /// <summary>The USS variable of a role: <c>--vd-color-action</c>.</summary>
        public static string ColorVariable(EThemeRole role) => "--vd-color-" + Kebab(role);

        /// <summary>Class that sets the background colour from a role: <c>vd-bg-action</c>.</summary>
        public static string BackgroundClass(EThemeRole role) => "vd-bg-" + Kebab(role);

        /// <summary>Class that sets the text colour (and image tint) from a role: <c>vd-text-action</c>.</summary>
        public static string TextClass(EThemeRole role) => "vd-text-" + Kebab(role);

        /// <summary>Class that sets the four border colours from a role: <c>vd-border-action</c>.</summary>
        public static string BorderClass(EThemeRole role) => "vd-border-" + Kebab(role);

        /// <summary>Which style property a themed class drives.</summary>
        public enum Target
        {
            Background,
            Text,
            Border,
            /// <summary>Only the left border: the accent bar of a toast or a hint.</summary>
            BorderLeft,
        }

        /// <summary>A class of <c>VirtuademyComponents.uss</c> that takes a colour from a role.</summary>
        public readonly struct ComponentRule
        {
            public readonly string ClassName;
            public readonly Target Target;
            public readonly EThemeRole Role;

            public ComponentRule(string className, Target target, EThemeRole role)
            {
                ClassName = className;
                Target = target;
                Role = role;
            }
        }

        /// <summary>
        /// The component classes whose colour comes from a role, so that a tenant palette published at
        /// runtime reaches them as it reaches the <c>vd-bg-*</c> / <c>vd-text-*</c> utility classes.
        /// Keep it in step with <c>VirtuademyComponents.uss</c>.
        /// </summary>
        public static readonly ComponentRule[] ComponentRules =
        {
            new("vd-panel", Target.Background, EThemeRole.Surface),
            new("vd-button--primary", Target.Background, EThemeRole.Action),
            new("vd-button--primary", Target.Text, EThemeRole.OnAction),
            new("vd-button--secondary", Target.Border, EThemeRole.Action),
            new("vd-button--secondary", Target.Text, EThemeRole.Action),
            new("vd-panel--hint", Target.BorderLeft, EThemeRole.Info),
            new("vd-panel--warning", Target.BorderLeft, EThemeRole.Warning),
            new("vd-panel--error", Target.BorderLeft, EThemeRole.Error),
            new("vd-panel--success", Target.BorderLeft, EThemeRole.Success),
            new("vd-toast--info", Target.BorderLeft, EThemeRole.Info),
            new("vd-toast--success", Target.BorderLeft, EThemeRole.Success),
            new("vd-toast--warning", Target.BorderLeft, EThemeRole.Warning),
            new("vd-toast--error", Target.BorderLeft, EThemeRole.Error),
            new("vd-check", Target.Background, EThemeRole.Success),
            new("vd-cross", Target.Background, EThemeRole.Error),
            new("vd-progress__fill", Target.Background, EThemeRole.Action),
            new("vd-list__item--current", Target.BorderLeft, EThemeRole.Action),
            new("vd-list__marker--done", Target.Background, EThemeRole.Success),
            new("vd-choice__option--selected", Target.Border, EThemeRole.Action),
        };
    }
}
