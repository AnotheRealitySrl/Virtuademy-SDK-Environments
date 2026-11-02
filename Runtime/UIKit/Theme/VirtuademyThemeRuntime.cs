using System;
using System.Collections.Generic;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// The colours the host application publishes for the running tenant, and the one place SDK
    /// panels ask "what colour is this role now".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The application calls <see cref="Publish"/> once it knows the tenant's style; themed panels
    /// (<see cref="ThemedDocument"/>, <see cref="ThemeColorRole"/>) and <c>IUIApi.GetThemeColor</c>
    /// follow through <see cref="Changed"/>. A role the application leaves out keeps the SDK value,
    /// so a palette that carries only the brand colour restyles buttons and accents and leaves
    /// surfaces, text and the status colours as the SDK draws them.
    /// </para>
    /// <para>
    /// Static rather than a system: the SDK cannot see the application's system framework, and an
    /// environment opened in the editor without the application has to get the defaults the same way.
    /// The state lives for the whole session, across environments.
    /// </para>
    /// </remarks>
    public static class VirtuademyThemeRuntime
    {
        private static readonly Dictionary<EThemeRole, Color> overrides = new();

        /// <summary>Raised after <see cref="Publish"/> or <see cref="Clear"/>.</summary>
        public static event Action Changed;

        /// <summary>Whether the application has published at least one role.</summary>
        public static bool HasOverrides => overrides.Count > 0;

        /// <summary>
        /// Replaces the published palette with <paramref name="palette"/>. Roles missing from it fall
        /// back to the SDK theme.
        /// </summary>
        public static void Publish(IReadOnlyDictionary<EThemeRole, Color> palette)
        {
            overrides.Clear();
            if (palette != null)
            {
                foreach (KeyValuePair<EThemeRole, Color> entry in palette)
                {
                    overrides[entry.Key] = entry.Value;
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Drops every published colour: the SDK theme applies again.</summary>
        public static void Clear()
        {
            if (overrides.Count == 0)
            {
                return;
            }
            overrides.Clear();
            Changed?.Invoke();
        }

        /// <summary>The published colour of a role, if the application published one.</summary>
        public static bool TryGetOverride(EThemeRole role, out Color color) => overrides.TryGetValue(role, out color);

        /// <summary>
        /// The colour of a role now: the published one, else <paramref name="theme"/>'s, else the
        /// SDK default.
        /// </summary>
        public static Color Resolve(EThemeRole role, VirtuademyTheme theme = null)
        {
            if (overrides.TryGetValue(role, out Color color))
            {
                return color;
            }
            return theme != null ? theme.GetColor(role) : VirtuademyTheme.GetColor(VirtuademyTheme.DefaultColors, role);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReloadOff()
        {
            overrides.Clear();
            Changed = null;
        }
    }
}
