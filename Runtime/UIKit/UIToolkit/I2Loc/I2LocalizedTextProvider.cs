// ============================================================
// I2LocalizedTextProvider.cs
//
// Belongs to Virtuademy.SDK.Environments.I2Loc — compiled only when
// I2LOC is defined (I2Loc package present in the project).
//
// Bridges I2Loc to the LocalizedXxx elements defined in
// Virtuademy.SDK.Environments: it registers itself as their
// ILocalizedTextProvider and forwards I2's language-change event.
// It defines no element types, so UXML stays identical with and
// without I2Loc.
// ============================================================
using I2.Loc;

using UnityEngine;
using UnityEngine.Scripting;

namespace Virtuademy.LocalizedComponents
{
    public sealed class I2LocalizedTextProvider : ILocalizedTextProvider
    {
        private static I2LocalizedTextProvider instance;

        /// <summary>Registers I2Loc as the translation backend of the LocalizedXxx elements. Idempotent.</summary>
        public static void Register()
        {
            instance ??= new I2LocalizedTextProvider();

            // Subscribed on every call, not once: I2 sets OnLocalizeEvent to null when the Editor
            // exits play mode. Removing first keeps a single subscription.
            LocalizationManager.OnLocalizeEvent -= LocalizationHelper.NotifyLanguageChanged;
            LocalizationManager.OnLocalizeEvent += LocalizationHelper.NotifyLanguageChanged;

            if (LocalizationHelper.Provider != instance)
            {
                LocalizationHelper.SetProvider(instance);
            }
        }

        // The only entry point of this assembly in a player: AlwaysLinkAssembly (AssemblyInfo.cs)
        // keeps the assembly, Preserve keeps the method.
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtRuntime() => Register();

#if UNITY_EDITOR
        // Edit mode too, so the UI Builder preview and the inspectors show the translations.
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            Register();
            // Back in edit mode I2 has dropped the subscription (see Register): take it again.
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                Register();
            }
        }
#endif

        public string Translate(string key) => LocalizationManager.GetTranslation(key);
    }
}
