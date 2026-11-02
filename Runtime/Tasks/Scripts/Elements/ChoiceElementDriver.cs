using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Choice element: an interaction opens the confirmation; confirming reports the choice.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ChoiceElementDriver : ElementDriver
    {
        private InteractablePlaceholder interactable;
        private VirtuademyPanel confirm;

        private ChoiceElementPlaceholder Choice => (ChoiceElementPlaceholder)Element;

        protected override void OnBind()
        {
            interactable = GetComponent<InteractablePlaceholder>();
            if (interactable == null)
            {
                Debug.LogWarning("[ChoiceElement] " + name + " needs an Interactable placeholder: it cannot be chosen.", this);
            }
            else
            {
                UseOneClick(false);
                interactable.OnInteract += OnPick;
            }
            if (Choice.ConfirmPanel != null && Choice.ConfirmPanel.TryGetComponent(out confirm))
            {
                confirm.OnPrimary.AddListener(Confirm);
                confirm.OnSecondary.AddListener(Cancel);
            }
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.OnInteract -= OnPick;
            }
            if (confirm != null)
            {
                confirm.OnPrimary.RemoveListener(Confirm);
                confirm.OnSecondary.RemoveListener(Cancel);
            }
        }

        private void OnPick(InteractionEventArgs args)
        {
            if (confirm == null)
            {
                Confirm();
                return;
            }
            confirm.gameObject.SetActive(true);
            confirm.Show();
        }

        // Hidden and inactive: a hidden panel that stays active keeps its document's collider over the choice, which
        // took the clicks meant for it (2026-11-02, E3 on web)
        private void Cancel()
        {
            if (confirm != null)
            {
                confirm.Hide();
                confirm.gameObject.SetActive(false);
            }
        }

        /// <summary>Reports the choice: right completes, wrong is an attempt (completes wrong with One Shot).</summary>
        public void Confirm()
        {
            Cancel();
            if (Choice.IsCorrect)
            {
                Report(TaskResult.Right(gameObject), true);
            }
            else
            {
                Report(TaskResult.Wrong(gameObject, Choice.WrongReason), Choice.OneShot);
            }
        }
    }
}
