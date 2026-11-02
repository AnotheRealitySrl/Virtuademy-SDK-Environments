using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// Draws an <see cref="InteractionCuePlaceholder"/>: added by the runtime to every cue
    /// (<see cref="InteractionCueRuntime"/>), never by a creator. Reacts to the cue's state changes only; it has no
    /// per-frame code.
    /// </summary>
    /// <remarks>
    /// The rim is drawn on shells: one child renderer per target renderer, with the same mesh (or the same skinned mesh,
    /// bones and bounds) and the cue's material on every sub-mesh. Shells are created the first time the rim shows and
    /// afterwards only enabled, disabled and given another of the settings' shared materials.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class InteractionCueDriver : MonoBehaviour, SPACS.Tasks.ITaskReactorExempt
    {
        private const string ShellName = "__Cue";

        // Material instances for cues that override their colour: one per source material and colour, shared by every
        // cue with that colour.
        private static readonly Dictionary<(Material, Color), Material> tintedMaterials = new();

        private InteractionCuePlaceholder cue;
        private readonly List<Renderer> shells = new();
        private readonly List<int> shellSubMeshes = new();
        private bool shellsBuilt;
        private Material shownMaterial;
        private bool reportedMissingSettings;

        private InteractablePlaceholder interactable;
        private int hoveringControllers;
        private InteractionCueTaskBinding taskBinding;
        private InteractionCueFeedback feedback;
        private List<Renderer> targets;
        private System.Func<List<Renderer>> targetsProvider;

        // The Interactable block this cue holds while completed (Block Interaction When Completed). Only a flag this
        // cue set is cleared by it.
        private const IInteractable.EBlockedState CompletedBlock = IInteractable.EBlockedState.BlockedByTaskCompletion;
        private IInteractable blockedInteractable;

        private VirtuademyLabel label;
        private bool labelSearched;
        private bool labelShown;
        private bool reportedMissingLabel;

        internal void Bind(InteractionCuePlaceholder placeholder)
        {
            cue = placeholder;
            cue.VisualChanged += Draw;
            feedback = new InteractionCueFeedback(cue);
            cue.ResultShown += feedback.OnResult;
            BindInteractable();
            taskBinding = InteractionCueTaskBinding.Create(cue);
            Draw();
        }

        // The Interactable of the object: on the cue's object, or the nearest one above it. Its events come from the
        // application only (none in a creator's play mode).
        private void BindInteractable()
        {
            interactable = cue.GetComponentInParent<InteractablePlaceholder>(true);
            if (interactable == null)
            {
                return;
            }
            interactable.OnHoverEnter += OnHoverEnter;
            interactable.OnHoverExit += OnHoverExit;
            interactable.OnSelectEnter += OnSelectEnter;
            interactable.OnSelectExit += OnSelectExit;
            interactable.OnBlockedChanged += OnBlockedChanged;
            if (interactable.IsBlocked)
            {
                cue.SetInteractableBlocked(true);
            }
        }

        // One enter and one exit per controller (mouse, each ray, each hand, the touch): hovered while any is on it.
        private void OnHoverEnter(InteractionEventArgs args)
        {
            hoveringControllers++;
            if (hoveringControllers == 1)
            {
                cue.SetInteractableHovered(true);
            }
        }

        private void OnHoverExit(InteractionEventArgs args)
        {
            if (hoveringControllers == 0)
            {
                return;
            }
            hoveringControllers--;
            if (hoveringControllers == 0)
            {
                cue.SetInteractableHovered(false);
            }
        }

        private void OnSelectEnter(InteractionEventArgs args) => cue.SetInteractableSelected(true);

        private void OnSelectExit(InteractionEventArgs args) => cue.SetInteractableSelected(false);

        // The block this cue holds while completed does not lock it (the cue shows Completed): only other reasons do.
        private void OnBlockedChanged(bool blocked)
        {
            if (blocked && blockedInteractable != null && (blockedInteractable.CurrentBlockedState & ~CompletedBlock) == 0)
            {
                return;
            }
            cue.SetInteractableBlocked(blocked);
        }

        private void HoldCompletedBlock(bool hold)
        {
            if (hold == (blockedInteractable != null))
            {
                return;
            }
            if (hold)
            {
                IInteractable target = interactable != null ? interactable.GetComponent<IInteractable>() : null;
                if (target == null || (target.CurrentBlockedState & CompletedBlock) != 0)
                {
                    // No Interactable yet (a creator's play mode), or already blocked this way by someone else.
                    return;
                }
                blockedInteractable = target;
                target.CurrentBlockedState |= CompletedBlock;
                return;
            }
            IInteractable held = blockedInteractable;
            blockedInteractable = null;
            if (held as Object == null)
            {
                return;
            }
            held.CurrentBlockedState &= ~CompletedBlock;
            // While this cue held its block, other reasons came and went without an event: read what is left.
            cue.SetInteractableBlocked(held.CurrentBlockedState != 0);
        }

        /// <summary>
        /// Called by the application in multiplayer for a networked cue: from now on a right or wrong action shown by a
        /// script or node on this client is also sent to the others through <paramref name="bridge"/>.
        /// </summary>
        public void AttachNetwork(IInteractionCueNetworkBridge bridge)
        {
            cue.ResultBroadcast = bridge == null ? null : bridge.SendResult;
        }

        /// <summary>A right or wrong action received from another user: shown here, never sent on.</summary>
        public void ShowReceivedResult(bool right)
        {
            cue.ShowResultLocally(right);
        }

        private void OnDestroy()
        {
            if (cue != null)
            {
                cue.VisualChanged -= Draw;
                if (feedback != null)
                {
                    cue.ResultShown -= feedback.OnResult;
                }
            }
            taskBinding?.Dispose();
            taskBinding = null;
            HoldCompletedBlock(false);
            if (interactable != null)
            {
                interactable.OnHoverEnter -= OnHoverEnter;
                interactable.OnHoverExit -= OnHoverExit;
                interactable.OnSelectEnter -= OnSelectEnter;
                interactable.OnSelectExit -= OnSelectExit;
                interactable.OnBlockedChanged -= OnBlockedChanged;
            }
            // Shells under renderers outside this object (renderers listed by the creator) do not go with it.
            foreach (Renderer shell in shells)
            {
                if (shell != null)
                {
                    Destroy(shell.gameObject);
                }
            }
            shells.Clear();
        }

        private void Draw()
        {
            HoldCompletedBlock(cue.BlockInteractionWhenCompleted && cue.BaseState == EInteractionCueState.Completed);
            DrawLabel();
            feedback.OnStateChanged(targetsProvider ??= Targets);
            Material material = MaterialFor(cue.RimState);
            if (material == shownMaterial)
            {
                return;
            }
            shownMaterial = material;
            if (material == null)
            {
                foreach (Renderer shell in shells)
                {
                    if (shell != null)
                    {
                        shell.enabled = false;
                    }
                }
                return;
            }
            if (!shellsBuilt)
            {
                BuildShells();
            }
            for (int i = 0; i < shells.Count; i++)
            {
                Renderer shell = shells[i];
                if (shell == null)
                {
                    continue;
                }
                Material[] materials = new Material[shellSubMeshes[i]];
                for (int m = 0; m < materials.Length; m++)
                {
                    materials[m] = material;
                }
                shell.sharedMaterials = materials;
                shell.enabled = true;
            }
        }

        // The hover label is a VirtuademyLabel among the cue's feedback objects (its children). The text is resolved
        // each time the label shows, so a language change applies from the next hover.
        private void DrawLabel()
        {
            bool show = cue.LabelVisible;
            if (show == labelShown && !show)
            {
                return;
            }
            if (!labelSearched)
            {
                labelSearched = true;
                foreach (VirtuademyLabel candidate in cue.GetComponentsInChildren<VirtuademyLabel>(true))
                {
                    if (candidate.GetComponentInParent<InteractionCuePlaceholder>(true) == cue)
                    {
                        label = candidate;
                        // Readable from a few metres, whatever the object's scale (the settings' Label Scale)
                        InteractionCueFeedback.KeepWorldSize(label.transform,
                            cue.Settings != null ? cue.Settings.LabelScale : 1f);
                        break;
                    }
                }
            }
            if (label == null)
            {
                if (show && !reportedMissingLabel)
                {
                    reportedMissingLabel = true;
                    Debug.LogWarning("[InteractionCue] " + cue.name + ": Hover Style is Rim And Label, but the cue has no " +
                        "label. Add its feedback objects (Add feedback objects, in its inspector).", cue);
                }
                return;
            }
            if (show)
            {
                label.SetText(PanelEnvironment.Text(cue.LabelKey, cue.LabelText));
                if (!labelShown)
                {
                    label.gameObject.SetActive(true);
                    label.Show();
                }
            }
            else
            {
                label.Hide();
            }
            labelShown = show;
        }

        private Material MaterialFor(EInteractionCueState state)
        {
            InteractionCueSettings settings = cue.Settings;
            if (settings == null)
            {
                if (!reportedMissingSettings && state != EInteractionCueState.Available)
                {
                    reportedMissingSettings = true;
                    Debug.LogError("[InteractionCue] " + cue.name + " has no Settings: it shows nothing. Set the SDK's " +
                        "InteractionCueSettings asset on it.", cue);
                }
                return null;
            }
            Material material;
            bool tintable = true;
            switch (state)
            {
                case EInteractionCueState.NextTarget:
                    material = cue.NextTargetStyle switch
                    {
                        ECueNextTargetStyle.Pulse => settings.NextTargetPulse,
                        ECueNextTargetStyle.Fixed => settings.NextTargetFixed,
                        _ => null,
                    };
                    break;
                case EInteractionCueState.Hover: material = settings.Hover; break;
                case EInteractionCueState.Selected: material = settings.Selected; break;
                case EInteractionCueState.Right: material = settings.Right; tintable = false; break;
                case EInteractionCueState.Wrong: material = settings.Wrong; tintable = false; break;
                default: material = null; break;
            }
            if (material == null || !tintable || !cue.OverrideColor)
            {
                return material;
            }
            if (!tintedMaterials.TryGetValue((material, cue.Color), out Material tinted) || tinted == null)
            {
                tinted = new Material(material) { name = material.name + " (cue colour)" };
                tinted.SetColor(ColorId, cue.Color);
                tintedMaterials[(material, cue.Color)] = tinted;
            }
            return tinted;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // The renderers that get the rim, collected once: the first time the rim or the arrow shows.
        private List<Renderer> Targets()
        {
            if (targets != null)
            {
                return targets;
            }
            targets = new List<Renderer>();
            if (cue.Renderers.Count > 0)
            {
                foreach (Renderer r in cue.Renderers)
                {
                    if (r != null)
                    {
                        targets.Add(r);
                    }
                }
            }
            else
            {
                foreach (Renderer r in cue.GetComponentsInChildren<Renderer>(true))
                {
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && Owns(r))
                    {
                        targets.Add(r);
                    }
                }
            }
            return targets;
        }

        private void BuildShells()
        {
            shellsBuilt = true;
            List<Renderer> targets = Targets();
            Dictionary<LODGroup, List<(int lod, Renderer shell)>> lodAdditions = null;
            List<string> staticBatched = null;
            foreach (Renderer target in targets)
            {
                if (target.isPartOfStaticBatch)
                {
                    (staticBatched ??= new List<string>()).Add(target.name);
                    continue;
                }
                Renderer shell = CreateShell(target, out int subMeshes);
                if (shell == null)
                {
                    continue;
                }
                shells.Add(shell);
                shellSubMeshes.Add(subMeshes);
                LODGroup group = target.GetComponentInParent<LODGroup>(true);
                if (group != null)
                {
                    LOD[] lods = group.GetLODs();
                    for (int l = 0; l < lods.Length; l++)
                    {
                        if (System.Array.IndexOf(lods[l].renderers, target) >= 0)
                        {
                            lodAdditions ??= new Dictionary<LODGroup, List<(int, Renderer)>>();
                            if (!lodAdditions.TryGetValue(group, out var list))
                            {
                                lodAdditions[group] = list = new List<(int, Renderer)>();
                            }
                            list.Add((l, shell));
                            break;
                        }
                    }
                }
            }
            if (lodAdditions != null)
            {
                foreach (var pair in lodAdditions)
                {
                    LOD[] lods = pair.Key.GetLODs();
                    foreach ((int lod, Renderer shell) in pair.Value)
                    {
                        Renderer[] current = lods[lod].renderers;
                        Renderer[] extended = new Renderer[current.Length + 1];
                        current.CopyTo(extended, 0);
                        extended[current.Length] = shell;
                        lods[lod].renderers = extended;
                    }
                    pair.Key.SetLODs(lods);
                }
            }
            if (staticBatched != null)
            {
                Debug.LogWarning("[InteractionCue] " + cue.name + ": no rim on " + string.Join(", ", staticBatched) +
                    ", part of a static batch. Untick Batching Static on these objects to give them a rim.", cue);
            }
            if (shells.Count == 0 && staticBatched == null)
            {
                Debug.LogWarning("[InteractionCue] " + cue.name + " has no mesh renderer to draw its rim on.", cue);
            }
        }

        // A renderer belongs to this cue unless it is a shell, one of the cue's feedback objects or its arrow, or sits
        // under another cue nested in this one.
        private bool Owns(Renderer r)
        {
            if (r.GetComponent<InteractionCueShell>() != null)
            {
                return false;
            }
            if ((cue.FeedbackRoot != null && r.transform.IsChildOf(cue.FeedbackRoot))
                || (cue.Arrow != null && r.transform.IsChildOf(cue.Arrow)))
            {
                return false;
            }
            InteractionCuePlaceholder nearest = r.GetComponentInParent<InteractionCuePlaceholder>(true);
            return nearest == cue;
        }

        private static Renderer CreateShell(Renderer target, out int subMeshes)
        {
            subMeshes = 0;
            Mesh mesh;
            if (target is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            else
            {
                mesh = target.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            }
            if (mesh == null)
            {
                return null;
            }
            subMeshes = mesh.subMeshCount;
            GameObject go = new(ShellName) { layer = target.gameObject.layer };
            go.transform.SetParent(target.transform, false);
            go.AddComponent<InteractionCueShell>();
            Renderer shell;
            if (target is SkinnedMeshRenderer source)
            {
                SkinnedMeshRenderer s = go.AddComponent<SkinnedMeshRenderer>();
                s.sharedMesh = mesh;
                s.bones = source.bones;
                s.rootBone = source.rootBone;
                s.localBounds = source.localBounds;
                s.quality = source.quality;
                s.updateWhenOffscreen = source.updateWhenOffscreen;
                shell = s;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                shell = go.AddComponent<MeshRenderer>();
            }
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            shell.allowOcclusionWhenDynamic = target.allowOcclusionWhenDynamic;
            shell.renderingLayerMask = target.renderingLayerMask;
            shell.enabled = false;
            return shell;
        }
    }
}
