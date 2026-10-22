using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A question with a column of options: one press chooses, or, with <see cref="confirmChoice"/>,
    /// a press selects and the confirm button chooses. For branching ("what do you do next?") and
    /// for select-and-confirm steps; a scored question with right and wrong answers is the Quiz.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy Choice Panel")]
    public class VirtuademyChoicePanel : UIDocumentBinder, IScenePanel
    {
        private const string SelectedClass = "vd-choice__option--selected";

        [SerializeField] private VirtuademyPanel.LocalizedText title = new("Question");
        [SerializeField] private VirtuademyPanel.LocalizedText prompt = new(string.Empty);
        [SerializeField] private List<VirtuademyPanel.LocalizedText> options = new()
        {
            new("Option A"), new("Option B"), new("Option C"),
        };

        [SerializeField, Tooltip("A press only selects; the confirm button chooses.")]
        private bool confirmChoice;

        [SerializeField] private VirtuademyPanel.LocalizedText confirmLabel = new("Confirm");

        [SerializeField, Tooltip("Hide the panel once an option is chosen.")]
        private bool hideOnChoice = true;

        [SerializeField, Tooltip("Visible when the scene starts. Off: call Show().")]
        private bool visibleOnStart = true;

        [Header("Events")]
        [SerializeField, Tooltip("An option was selected (confirm mode), with its index.")]
        private UnityEvent<int> onOptionSelected = new();

        [SerializeField, Tooltip("An option was chosen, with its index.")]
        private UnityEvent<int> onOptionChosen = new();

        private VisualElement panel;
        private Label titleLabel;
        private Label promptLabel;
        private VisualElement optionsContainer;
        private Button confirmButton;
        private readonly List<Button> optionButtons = new();
        private int selected = -1;
        private bool visible;
        private bool listening;

        public UnityEvent<int> OnOptionSelected => onOptionSelected;
        public UnityEvent<int> OnOptionChosen => onOptionChosen;

        /// <summary>The selected option in confirm mode; -1 when none.</summary>
        public int SelectedIndex => selected;

        private void Awake()
        {
            visible = visibleOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            panel = root.Q<VisualElement>("panel");
            optionsContainer = root.Q<VisualElement>("options");
            if (panel == null || optionsContainer == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyChoicePanel)}] '{name}': the document needs 'panel' and 'options'.", this);
                return false;
            }
            titleLabel = root.Q<Label>("title");
            promptLabel = root.Q<Label>("body");
            confirmButton = root.Q<Button>("primary");
            PanelEnvironment.IgnoreExceptControls(root);
            if (confirmButton != null)
            {
                confirmButton.clicked += Confirm;
            }
            listening = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            WatchForRebuild(panel);
            Refresh();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (confirmButton != null)
            {
                confirmButton.clicked -= Confirm;
            }
            if (listening)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
                listening = false;
            }
            optionButtons.Clear();
            panel = null;
        }

        public void Show()
        {
            visible = true;
            selected = -1;
            Refresh();
        }

        public void Hide()
        {
            visible = false;
            Refresh();
        }

        /// <summary>Replaces the options with texts shown as written.</summary>
        public void SetOptions(string[] texts)
        {
            options.Clear();
            if (texts != null)
            {
                foreach (string text in texts)
                {
                    options.Add(new VirtuademyPanel.LocalizedText(text));
                }
            }
            selected = -1;
            Refresh();
        }

        void IScenePanel.SetTexts(string titleText, string promptText)
        {
            if (titleText != null)
            {
                title = new VirtuademyPanel.LocalizedText(titleText);
            }
            if (promptText != null)
            {
                prompt = new VirtuademyPanel.LocalizedText(promptText);
            }
            Refresh();
        }

        /// <summary>Chooses the selected option (confirm mode). Does nothing with none selected.</summary>
        public void Confirm()
        {
            if (selected >= 0)
            {
                Choose(selected);
            }
        }

        private void Press(int index)
        {
            if (!confirmChoice)
            {
                Choose(index);
                return;
            }
            selected = index;
            for (int i = 0; i < optionButtons.Count; i++)
            {
                optionButtons[i].EnableInClassList(SelectedClass, i == selected);
            }
            confirmButton?.SetEnabled(true);
            if (TryGetComponent(out ThemedDocument themed))
            {
                themed.Reapply();
            }
            onOptionSelected.Invoke(index);
        }

        private void Choose(int index)
        {
            onOptionChosen.Invoke(index);
            PanelEvents.RaiseButtonPressed(this, index);
            if (hideOnChoice)
            {
                Hide();
            }
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(titleLabel, title.Resolve());
            SetText(promptLabel, prompt.Resolve());

            optionsContainer.Clear();
            optionButtons.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = new Button(() => Press(index)) { text = options[i].Resolve() };
                button.AddToClassList("vd-choice__option");
                button.EnableInClassList(SelectedClass, i == selected);
                optionsContainer.Add(button);
                optionButtons.Add(button);
            }

            if (confirmButton != null)
            {
                confirmButton.style.display = confirmChoice ? DisplayStyle.Flex : DisplayStyle.None;
                confirmButton.text = confirmLabel.Resolve();
                confirmButton.SetEnabled(selected >= 0);
            }
            if (TryGetComponent(out ThemedDocument themed))
            {
                themed.Reapply();
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label == null)
            {
                return;
            }
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
