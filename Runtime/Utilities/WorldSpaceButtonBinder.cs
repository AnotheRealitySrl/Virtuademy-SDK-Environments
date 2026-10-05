using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Ready-made bridge for a world-space UI Toolkit button panel. Every UI Toolkit
    /// <see cref="Button"/> is exposed to the Inspector as a <see cref="UnityEvent"/> (looked up by
    /// its UXML <c>name</c>), so a world-author can react to a click without writing any code.
    ///
    /// Hover and any other visual feedback are authored entirely in USS (e.g.
    /// <c>.wsb-button:hover { scale: 1.2 1.2; }</c>) — this component does not touch the visuals.
    ///
    /// Click and hover are delivered by Unity's native world-space UI Toolkit picking (the built-in
    /// <c>WorldDocumentRaycaster</c> plus the scene's EventSystem/input module), which works the same
    /// on VR (XR ray), WebGL desktop (mouse) and Mobile (touch). No XR Interaction Toolkit and no
    /// platform interaction system are referenced here, so the prefab works everywhere from a single
    /// asset with no extra packages. On Mobile there is no hover concept, so a <c>:hover</c> effect
    /// simply does not show there — a tap still fires the click.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WorldSpaceButtonBinder : UIDocumentBinder
    {
        [Serializable]
        public class ButtonBinding
        {
            [Tooltip("The 'name' of the Button element in the UXML to bind to.")]
            public string buttonName = "button";

            [Tooltip("Invoked when that button is clicked (mouse / touch / VR ray).")]
            public UnityEvent onClick = new();
        }

        [SerializeField, Tooltip("One entry per button you want to expose to the Inspector. Reference " +
            "each button by the 'name' it has in the UXML.")]
        private List<ButtonBinding> buttons = new() { new ButtonBinding() };

        private readonly List<(Button button, Action handler)> registered = new();

        public IReadOnlyList<ButtonBinding> Buttons => buttons;

        protected override void UnbindFromTree()
        {
            foreach ((Button button, Action handler) in registered)
            {
                if (button != null)
                {
                    button.clicked -= handler;
                }
            }
            registered.Clear();
        }

        protected override bool BindTo(VisualElement root)
        {
            foreach (ButtonBinding binding in buttons)
            {
                if (binding == null || string.IsNullOrEmpty(binding.buttonName))
                {
                    continue;
                }
                Button button = root.Q<Button>(binding.buttonName);
                if (button == null)
                {
                    Debug.LogWarning($"[{nameof(WorldSpaceButtonBinder)}] Button '{binding.buttonName}' " +
                        $"not found in the document on '{name}'.", this);
                    continue;
                }
                UnityEvent onClick = binding.onClick;
                Action handler = () => onClick?.Invoke();
                button.clicked += handler;
                registered.Add((button, handler));
            }

            return registered.Count > 0;
        }
    }
}
