using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.Environments.ScriptingApi.UI;
using Virtuademy.SDK.Environments.InteractionCue;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// Shows a procedure's results: the right and wrong texts (a toast or a panel; in Assessed mode a neutral text, the
    /// real one waits for the summary), the sounds a cue does not already play, the hint (text, and the task's targets
    /// highlighted for a moment), and the mark and flash for targets whose cue has no feedback objects of its own.
    /// Added by the runtime with <see cref="ProcedureRun"/>; driven by its events only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ProcedureFeedbackPresenter : MonoBehaviour
    {
        private const string RecordedKey = "Procedure/Feedback/Recorded";
        private const float HintTargetSeconds = 3f;

        private ProcedureRun run;
        private ProcedurePlaceholder procedure;
        private AudioSource audioSource;

        internal void Bind(ProcedureRun procedureRun)
        {
            run = procedureRun;
            procedure = run.Procedure;
            run.ResultScored += OnResultScored;
            run.HintGiven += OnHint;
        }

        private void OnDestroy()
        {
            if (run != null)
            {
                run.ResultScored -= OnResultScored;
                run.HintGiven -= OnHint;
            }
        }

        private void OnResultScored(ProcedureRun.Step step, TaskResult result, float lost, ProcedureRun.EResultKind kind)
        {
            bool wrong = result.Outcome == ETaskOutcome.Wrong;
            // A right attempt is progress only: nothing to show.
            if (kind == ProcedureRun.EResultKind.Attempt && !wrong)
            {
                return;
            }
            IReadOnlyList<InteractionCuePlaceholder> cues = step != null
                ? InteractionCueTaskBinding.CuesOf(step.Task)
                : System.Array.Empty<InteractionCuePlaceholder>();
            ShowSharedFeedback(cues, result.Source, !wrong);
            if (procedure.PlaySounds && !CueSounds(cues))
            {
                PlaySound(wrong ? procedure.Sounds?.WrongClip
                    : kind == ProcedureRun.EResultKind.Completion && step != null && step.Task.Node.TasksDependingOnThis.Count > 0
                        ? procedure.Sounds?.RightClip
                        : procedure.Sounds?.TaskCompletedClip);
            }
            string text = procedure.Mode == EProcedureMode.Assessed
                ? PanelEnvironment.Text(RecordedKey, "Recorded")
                : wrong
                    ? (step != null ? ProcedureRun.WrongTextOf(step, result.Reason) : result.Reason)
                    : (step != null ? ProcedureRun.RightTextOf(step) : "");
            if (!result.IsLocalActor && result.ActorName.Length > 0)
            {
                text = result.ActorName + ": " + text;
            }
            ShowText(text, !wrong);
        }

        private void OnHint(ProcedureRun.Step step)
        {
            string hint = ProcedureRun.HintOf(step);
            if (hint.Length > 0)
            {
                ShowText(hint, true, EToastSeverity.Info);
            }
            if (procedure.HintShowsTarget)
            {
                foreach (InteractionCuePlaceholder cue in InteractionCueTaskBinding.CuesOf(step.Task))
                {
                    cue.ShowTargetFor(cue.FeedbackSeconds > HintTargetSeconds ? cue.FeedbackSeconds : HintTargetSeconds);
                }
            }
        }

        /// <summary>Plays the summary's sound (the summary panel calls it when it shows).</summary>
        internal void PlaySummarySound()
        {
            if (procedure.PlaySounds)
            {
                PlaySound(procedure.Sounds?.SummaryClip);
            }
        }

        // A bound cue that plays its own sound: the presenter plays none, so a result is heard once.
        private static bool CueSounds(IReadOnlyList<InteractionCuePlaceholder> cues)
        {
            foreach (InteractionCuePlaceholder cue in cues)
            {
                if ((cue.Feedback & ECueFeedback.Sound) != 0 && cue.Settings != null)
                {
                    return true;
                }
            }
            return false;
        }

        // The procedure's shared mark and flash, moved above the first bound cue (or the result's source) that has no
        // feedback objects of its own.
        private void ShowSharedFeedback(IReadOnlyList<InteractionCuePlaceholder> cues, GameObject source, bool right)
        {
            Transform shared = procedure.SharedFeedback;
            if (shared == null)
            {
                return;
            }
            Transform target = null;
            foreach (InteractionCuePlaceholder cue in cues)
            {
                if (cue.FeedbackRoot == null)
                {
                    target = cue.transform;
                    break;
                }
            }
            if (target == null && cues.Count == 0 && source != null)
            {
                target = source.transform;
            }
            if (target == null)
            {
                return;
            }
            Bounds? bounds = null;
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    if (bounds == null)
                    {
                        bounds = r.bounds;
                    }
                    else
                    {
                        Bounds b = bounds.Value;
                        b.Encapsulate(r.bounds);
                        bounds = b;
                    }
                }
            }
            shared.position = bounds == null ? target.position + Vector3.up * 0.3f
                : new Vector3(bounds.Value.center.x, bounds.Value.max.y + 0.1f, bounds.Value.center.z);
            shared.gameObject.SetActive(true);
            VirtuademyCheckMark mark = shared.GetComponentInChildren<VirtuademyCheckMark>(true);
            if (mark != null)
            {
                mark.gameObject.SetActive(true);
                mark.ShowResult(right);
            }
            if (right)
            {
                ParticleSystem flash = shared.GetComponentInChildren<ParticleSystem>(true);
                if (flash != null)
                {
                    flash.gameObject.SetActive(true);
                    flash.Play(true);
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(clip);
        }

        private void ShowText(string text, bool right, EToastSeverity? severity = null)
        {
            if (string.IsNullOrEmpty(text) || procedure.FeedbackPresentation == EFeedbackPresentation.None)
            {
                return;
            }
            if (procedure.FeedbackPresentation == EFeedbackPresentation.Panel)
            {
                GameObject panelObject = right ? procedure.SuccessPanel : procedure.ErrorPanel;
                if (panelObject != null && panelObject.TryGetComponent(out VirtuademyPanel panel))
                {
                    panel.SetText(text);
                    panel.Show();
                    return;
                }
            }
            if (IVirtuademyGameplay.IsAvailable && IVirtuademyGameplay.Current.UI != null && IVirtuademyGameplay.Current.UI.IsAvailable)
            {
                IVirtuademyGameplay.Current.UI.ShowToast(text, severity ?? (right ? EToastSeverity.Success : EToastSeverity.Error));
            }
            else
            {
                // A creator's play mode without the application's toasts.
                Debug.Log("[Procedure] " + (right ? "right: " : "wrong: ") + text, procedure);
            }
        }
    }
}
