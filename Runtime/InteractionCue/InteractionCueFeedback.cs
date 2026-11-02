using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// The feedback objects of a cue: the check and cross mark, the flash and the sound of a right or wrong action, and
    /// the "go here" arrow. Found among the cue's feedback objects the first time they are needed; the arrow is created
    /// from the settings when the cue has none, and a cue without an audio source gets a 2D one. Driven by
    /// <see cref="InteractionCueDriver"/> on the cue's events only.
    /// </summary>
    internal sealed class InteractionCueFeedback
    {
        private readonly InteractionCuePlaceholder cue;
        private bool searched;
        private VirtuademyCheckMark mark;
        private ParticleSystem flash;
        private AudioSource audio;
        private bool markShown;

        public InteractionCueFeedback(InteractionCuePlaceholder cue)
        {
            this.cue = cue;
        }

        private void Search()
        {
            if (searched)
            {
                return;
            }
            searched = true;
            Transform root = cue.FeedbackRoot;
            if (root == null)
            {
                return;
            }
            mark = root.GetComponentInChildren<VirtuademyCheckMark>(true);
            flash = root.GetComponentInChildren<ParticleSystem>(true);
            MakeReplayable(flash);
            audio = root.GetComponentInChildren<AudioSource>(true);
        }

        /// <summary>
        /// The SDK's Flash destroys itself when it ends (Stop Action Destroy), which suits a one-off effect: this
        /// instance (a cue's own, or a procedure's shared one) is kept instead, so every right action plays it again.
        /// The Flash prefab is not changed.
        /// </summary>
        internal static void MakeReplayable(ParticleSystem flash)
        {
            if (flash == null)
            {
                return;
            }
            foreach (ParticleSystem system in flash.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        /// <summary>A right or wrong action was shown: mark, flash (right only) and sound, as the cue's Feedback says.</summary>
        public void OnResult(bool right)
        {
            Search();
            ECueFeedback what = cue.Feedback;
            if ((what & ECueFeedback.Mark) != 0 && mark != null)
            {
                mark.gameObject.SetActive(true);
                mark.ShowResult(right);
                markShown = true;
            }
            if ((what & ECueFeedback.Flash) != 0 && right && flash != null)
            {
                flash.gameObject.SetActive(true);
                flash.Play(true);
            }
            if ((what & ECueFeedback.Sound) != 0)
            {
                AudioClip clip = cue.Settings == null ? null : right ? cue.Settings.RightClip : cue.Settings.WrongClip;
                if (clip != null)
                {
                    if (audio == null)
                    {
                        audio = cue.gameObject.AddComponent<AudioSource>();
                        audio.playOnAwake = false;
                        audio.spatialBlend = 0f;
                    }
                    audio.PlayOneShot(clip);
                }
            }
        }

        /// <summary>The cue's state changed: the mark hides when the right or wrong action ends; the arrow follows
        /// <see cref="InteractionCuePlaceholder.ArrowVisible"/>.</summary>
        public void OnStateChanged(System.Func<System.Collections.Generic.List<Renderer>> targets)
        {
            EInteractionCueState state = cue.State;
            if (markShown && state != EInteractionCueState.Right && state != EInteractionCueState.Wrong)
            {
                markShown = false;
                if (mark != null)
                {
                    mark.Hide();
                }
            }
            bool showArrow = cue.ArrowVisible;
            Transform arrow = cue.Arrow;
            if (showArrow && arrow == null)
            {
                arrow = CreateArrow(targets);
            }
            if (arrow != null && arrow.gameObject.activeSelf != showArrow)
            {
                arrow.gameObject.SetActive(showArrow);
            }
        }

        private Transform CreateArrow(System.Func<System.Collections.Generic.List<Renderer>> targets)
        {
            GameObject prefab = cue.Settings == null ? null : cue.Settings.ArrowPrefab;
            if (prefab == null)
            {
                return null;
            }
            Vector3 tip;
            if (cue.ArrowAnchor != null)
            {
                tip = cue.ArrowAnchor.position;
            }
            else
            {
                Bounds? bounds = null;
                foreach (Renderer r in targets())
                {
                    if (r == null)
                    {
                        continue;
                    }
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
                Vector3 top = bounds == null ? cue.transform.position : new Vector3(bounds.Value.center.x, bounds.Value.max.y,
                    bounds.Value.center.z);
                tip = top + Vector3.up * cue.Settings.ArrowHeight;
            }
            GameObject created = Object.Instantiate(prefab, tip, Quaternion.identity, cue.transform);
            created.name = "__CueArrow";
            created.SetActive(false);
            cue.SetArrow(created.transform);
            return created.transform;
        }
    }
}
