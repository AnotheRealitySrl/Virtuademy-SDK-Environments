using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.UIKit
{
    /// <summary>
    /// A round check mark (right) or cross (wrong) that pops in at a point of the world: the UI
    /// Toolkit successor of the SDK's uGUI <c>CheckCompleted</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Virtuademy/UI/Virtuademy Check Mark")]
    public class VirtuademyCheckMark : UIDocumentBinder, IScenePanel
    {
        public enum EState
        {
            Hidden = 0,
            Correct = 1,
            Wrong = 2,
        }

        [SerializeField] private EState stateOnStart = EState.Hidden;

        [SerializeField, Min(0f), Tooltip("Seconds before it hides by itself; 0 keeps it shown.")]
        private float autoHideSeconds;

        [Header("Events")]
        [SerializeField] private UnityEvent onShownCorrect = new();
        [SerializeField] private UnityEvent onShownWrong = new();
        [SerializeField] private UnityEvent onHidden = new();

        private VisualElement check;
        private VisualElement cross;
        private EState state;
        private IVisualElementScheduledItem hideItem;

        public UnityEvent OnShownCorrect => onShownCorrect;
        public UnityEvent OnShownWrong => onShownWrong;
        public UnityEvent OnHidden => onHidden;

        public EState State => state;

        private void Awake()
        {
            state = stateOnStart;
        }

        protected override bool BindTo(VisualElement root)
        {
            check = root.Q<VisualElement>("check");
            cross = root.Q<VisualElement>("cross");
            if (check == null || cross == null)
            {
                Debug.LogWarning($"[{nameof(VirtuademyCheckMark)}] '{name}': the document needs 'check' and 'cross'.", this);
                return false;
            }
            PanelEnvironment.IgnoreExceptControls(root);
            WatchForRebuild(check);
            Refresh(animate: false);
            return true;
        }

        protected override void UnbindFromTree()
        {
            hideItem?.Pause();
            hideItem = null;
            check = null;
            cross = null;
        }

        /// <summary>Shows the check mark.</summary>
        public void ShowCorrect() => SetState(EState.Correct);

        /// <summary>Shows the cross.</summary>
        public void ShowWrong() => SetState(EState.Wrong);

        /// <summary>Shows the check mark when <paramref name="correct"/>, else the cross.</summary>
        public void ShowResult(bool correct) => SetState(correct ? EState.Correct : EState.Wrong);

        /// <summary>Hides it.</summary>
        public void Hide() => SetState(EState.Hidden);

        /// <summary>Shows the check mark (as <see cref="IScenePanel"/>, which has no result to give).</summary>
        void IScenePanel.Show() => ShowCorrect();

        void IScenePanel.SetTexts(string title, string text)
        {
        }

        private void SetState(EState value)
        {
            state = value;
            Refresh(animate: true);
            switch (state)
            {
                case EState.Correct: onShownCorrect.Invoke(); break;
                case EState.Wrong: onShownWrong.Invoke(); break;
                default: onHidden.Invoke(); break;
            }
        }

        private void Refresh(bool animate)
        {
            if (check == null)
            {
                return;
            }
            hideItem?.Pause();
            hideItem = null;
            Show(check, state == EState.Correct, animate);
            Show(cross, state == EState.Wrong, animate);
            if (state != EState.Hidden && autoHideSeconds > 0f)
            {
                hideItem = check.schedule.Execute(Hide).StartingIn((long)(autoHideSeconds * 1000f));
            }
        }

        private static void Show(VisualElement element, bool on, bool animate)
        {
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on || !animate)
            {
                return;
            }
            // A short pop: start small, let the USS transition bring it to full size.
            element.style.scale = new Scale(new Vector3(0.6f, 0.6f, 1f));
            element.schedule.Execute(() => element.style.scale = new Scale(Vector3.one)).StartingIn(16);
        }
    }
}
