using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A titled list of items, each with a done marker and an optional current-item accent: the UI
    /// Toolkit successor of the SDK's uGUI <c>ListItemCanvas</c>, and the checklist of a "find N
    /// things" step.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy List Panel")]
    public class VirtuademyListPanel : UIDocumentBinder, IScenePanel
    {
        [Serializable]
        public class Item
        {
            public VirtuademyPanel.LocalizedText text = new("Item");
            public bool done;
        }

        [SerializeField] private VirtuademyPanel.LocalizedText title = new("Checklist");
        [SerializeField] private List<Item> items = new() { new Item(), new Item(), new Item() };

        [SerializeField, Tooltip("Index of the item to accent as the current one; -1: none.")]
        private int currentIndex = -1;

        [SerializeField, Tooltip("Visible when the scene starts. Off: call Show().")]
        private bool visibleOnStart = true;

        [Header("Events")]
        [SerializeField, Tooltip("An item was marked done, with its index.")]
        private UnityEvent<int> onItemDone = new();

        [SerializeField, Tooltip("Every item is done.")]
        private UnityEvent onAllDone = new();

        private VisualElement panel;
        private Label titleLabel;
        private VisualElement list;
        private bool visible;
        private bool listening;

        public UnityEvent<int> OnItemDone => onItemDone;
        public UnityEvent OnAllDone => onAllDone;

        public int ItemCount => items.Count;

        private void Awake()
        {
            visible = visibleOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            panel = root.Q<VisualElement>("panel");
            list = root.Q<VisualElement>("list");
            if (panel == null || list == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyListPanel)}] '{name}': the document needs 'panel' and 'list'.", this);
                return false;
            }
            titleLabel = root.Q<Label>("title");
            PanelEnvironment.IgnoreExceptControls(root);
            listening = PanelEnvironment.SubscribeLanguage(OnLanguageChanged);
            WatchForRebuild(panel);
            Refresh();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (listening)
            {
                PanelEnvironment.UnsubscribeLanguage(OnLanguageChanged);
                listening = false;
            }
            panel = null;
            list = null;
        }

        public void Show()
        {
            visible = true;
            Refresh();
        }

        public void Hide()
        {
            visible = false;
            Refresh();
        }

        /// <summary>Marks an item done (or not), and raises the events when that completes the list.</summary>
        public void SetDone(int index, bool done = true)
        {
            if (index < 0 || index >= items.Count || items[index].done == done)
            {
                return;
            }
            items[index].done = done;
            Refresh();
            if (!done)
            {
                return;
            }
            onItemDone.Invoke(index);
            if (items.TrueForAll(i => i.done))
            {
                onAllDone.Invoke();
            }
        }

        /// <summary>Marks an item done.</summary>
        public void MarkDone(int index) => SetDone(index, true);

        /// <summary>Accents one item as the current one; -1 clears the accent.</summary>
        public void SetCurrent(int index)
        {
            currentIndex = index;
            Refresh();
        }

        /// <summary>Replaces the items with texts shown as written; all not done.</summary>
        public void SetItems(string[] texts)
        {
            items.Clear();
            if (texts != null)
            {
                foreach (string text in texts)
                {
                    items.Add(new Item { text = new VirtuademyPanel.LocalizedText(text) });
                }
            }
            Refresh();
        }

        void IScenePanel.SetTexts(string titleText, string text)
        {
            if (titleText != null)
            {
                title = new VirtuademyPanel.LocalizedText(titleText);
                Refresh();
            }
        }

        /// <summary>Clears every done marker.</summary>
        public void ResetItems()
        {
            items.ForEach(i => i.done = false);
            Refresh();
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (titleLabel != null)
            {
                titleLabel.text = title.Resolve();
            }

            list.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("vd-list__item");
                row.EnableInClassList("vd-list__item--done", items[i].done);
                row.EnableInClassList("vd-list__item--current", i == currentIndex);

                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("vd-list__marker");
                marker.EnableInClassList("vd-list__marker--done", items[i].done);
                row.Add(marker);

                var label = new Label(items[i].text.Resolve()) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("vd-list__text");
                row.Add(label);

                list.Add(row);
            }
            ThemeApplier.Apply(list);
        }
    }
}
