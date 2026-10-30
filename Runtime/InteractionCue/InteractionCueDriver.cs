using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.InteractionCue
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
    public class InteractionCueDriver : MonoBehaviour
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

        internal void Bind(InteractionCuePlaceholder placeholder)
        {
            cue = placeholder;
            cue.VisualChanged += Draw;
            Draw();
        }

        private void OnDestroy()
        {
            if (cue != null)
            {
                cue.VisualChanged -= Draw;
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

        private void BuildShells()
        {
            shellsBuilt = true;
            List<Renderer> targets = new();
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

        // A renderer belongs to this cue unless it is a shell or sits under another cue nested in this one.
        private bool Owns(Renderer r)
        {
            if (r.name == ShellName && r.GetComponent<InteractionCueShell>() != null)
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
