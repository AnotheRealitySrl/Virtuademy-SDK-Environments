using UnityEngine;
using UnityEngine.UI;

using Virtuademy.Environments.ScriptingApi.UI;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// Colours a uGUI graphic (an <see cref="Image"/>, a TextMesh Pro text, any <see cref="Graphic"/>)
    /// by theme role, and follows the tenant's colours when the application publishes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The colour is written when the component is enabled and whenever the palette changes, at
    /// runtime only: in the editor the graphic keeps the colour serialized on it, so adding this
    /// component to an existing prefab changes nothing until the scene plays. Use
    /// <c>Apply theme colour now</c> in the component's context menu to preview it in the editor.
    /// </para>
    /// <para>
    /// The SDK's uGUI prefabs do not carry it: published environments keep their colours as they
    /// were. It is for panels a creator builds or keeps in uGUI.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Theme Color Role")]
    public class ThemeColorRole : MonoBehaviour
    {
        [SerializeField, Tooltip("The role whose colour the graphic takes.")]
        private EThemeRole role = EThemeRole.Action;

        [SerializeField, Range(0f, 1f), Tooltip("Multiplies the role's alpha, for a tinted background.")]
        private float alpha = 1f;

        [SerializeField, Tooltip("Theme to read the role from when the application publishes nothing " +
            "for it. Empty: the SDK defaults.")]
        private VirtuademyTheme theme;

        private Graphic graphic;

        /// <summary>The role, settable from code; the colour follows at once.</summary>
        public EThemeRole Role
        {
            get => role;
            set
            {
                role = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            VirtuademyThemeRuntime.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            VirtuademyThemeRuntime.Changed -= Apply;
        }

        [ContextMenu("Apply theme colour now")]
        private void Apply()
        {
            if (graphic == null && !TryGetComponent(out graphic))
            {
                return;
            }
            Color color = VirtuademyThemeRuntime.Resolve(role, theme);
            color.a *= alpha;
            graphic.color = color;
        }
    }
}
