using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    /// <summary>
    /// Inspector of <see cref="InteractionCuePlaceholder"/>: the fields, what the cue will not be able to show (the
    /// validator), and <b>Add feedback objects</b>, which puts the settings' feedback objects (and the arrow when Show
    /// Arrow is on) under the cue, above the object.
    /// </summary>
    [CustomEditor(typeof(InteractionCuePlaceholder))]
    [CanEditMultipleObjects]
    public class InteractionCuePlaceholderEditor : NetworkPlaceholderEditor
    {
        /// <summary>Visible skinned cues on Quest before profiling says otherwise (spec C.9).</summary>
        private const int QuestSkinnedBudget = 2;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (targets.Length != 1)
            {
                return;
            }
            InteractionCuePlaceholder cue = (InteractionCuePlaceholder)target;
            EditorGUILayout.Space();
            foreach ((MessageType type, string text) in Validate(cue))
            {
                EditorGUILayout.HelpBox(text, type);
            }
            using (new EditorGUI.DisabledScope(cue.Settings == null || cue.Settings.FeedbackPrefab == null || cue.FeedbackRoot != null))
            {
                if (GUILayout.Button(new GUIContent("Add feedback objects", "Puts the label, the check and cross mark, the " +
                    "flash and a 2D audio source under the cue, above the object; and the arrow when Show Arrow is on.")))
                {
                    AddFeedbackObjects(cue);
                }
            }
        }

        /// <summary>What the cue will not be able to show, and the choices that cost on a headset.</summary>
        public static List<(MessageType, string)> Validate(InteractionCuePlaceholder cue)
        {
            List<(MessageType, string)> messages = new();
            if (cue.Settings == null)
            {
                messages.Add((MessageType.Error, "No Settings: the cue shows nothing. Use the SDK's InteractionCueSettings asset " +
                    "(Prefabs/InteractionCue)."));
            }
            List<Renderer> renderers = TargetRenderers(cue);
            if (renderers.Count == 0)
            {
                messages.Add((MessageType.Warning, "No mesh renderer under this object: the cue has nothing to draw its rim on."));
            }
            List<string> batched = renderers.Where(IsBatchingStatic).Select(r => r.name).ToList();
            if (batched.Count > 0)
            {
                messages.Add((MessageType.Warning, "Batching Static, so no rim at run time: " + string.Join(", ", batched) +
                    ". Untick Batching Static on these objects."));
            }
            int skinned = renderers.Count(r => r is SkinnedMeshRenderer);
            if (skinned > 0)
            {
                messages.Add((MessageType.Info, skinned + " skinned renderer(s): their rim re-skins while it shows. On Quest keep " +
                    "at most " + QuestSkinnedBudget + " skinned cues visible at once. Blend shapes are not followed."));
            }
            if (cue.HoverStyle == ECueHoverStyle.RimAndLabel && cue.FeedbackRoot == null)
            {
                messages.Add((MessageType.Warning, "Hover Style is Rim And Label, but the cue has no feedback objects: no label " +
                    "will show. Add feedback objects below."));
            }
            if ((cue.Feedback & (ECueFeedback.Mark | ECueFeedback.Flash)) != 0 && cue.FeedbackRoot == null)
            {
                messages.Add((MessageType.Info, "Feedback includes Mark or Flash, which need the cue's feedback objects. " +
                    "Without them a right or wrong action shows the rim and plays the sound only."));
            }
            if (cue.IsNetworked && cue.Tasks.Any(t => t != null))
            {
                messages.Add((MessageType.Info, "Is Networked costs a network view per cue in multiplayer. A cue that only " +
                    "follows networked tasks shows their right and wrong actions to everyone without it: untick Is Networked " +
                    "unless scripts call Show Right or Show Wrong on it."));
            }
            return messages;
        }

        private static bool IsBatchingStatic(Renderer r) =>
            GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic);

        // The renderers the cue will draw its rim on, as the runtime collects them.
        private static List<Renderer> TargetRenderers(InteractionCuePlaceholder cue)
        {
            if (cue.Renderers.Count > 0)
            {
                return cue.Renderers.Where(r => r != null).ToList();
            }
            return cue.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                .Where(r => r.GetComponentInParent<InteractionCuePlaceholder>(true) == cue)
                .Where(r => cue.FeedbackRoot == null || !r.transform.IsChildOf(cue.FeedbackRoot))
                .Where(r => cue.Arrow == null || !r.transform.IsChildOf(cue.Arrow))
                .ToList();
        }

        private static void AddFeedbackObjects(InteractionCuePlaceholder cue)
        {
            List<Renderer> renderers = TargetRenderers(cue);
            Bounds bounds = new(cue.transform.position, Vector3.zero);
            if (renderers.Count > 0)
            {
                bounds = renderers[0].bounds;
                foreach (Renderer r in renderers.Skip(1))
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            Vector3 top = new(bounds.center.x, bounds.max.y, bounds.center.z);
            Undo.SetCurrentGroupName("Add feedback objects");
            int group = Undo.GetCurrentGroup();

            GameObject feedback = (GameObject)PrefabUtility.InstantiatePrefab(cue.Settings.FeedbackPrefab, cue.transform);
            Undo.RegisterCreatedObjectUndo(feedback, "Add feedback objects");
            feedback.transform.position = top + Vector3.up * 0.1f;
            SerializedObject so = new(cue);
            so.FindProperty("feedbackRoot").objectReferenceValue = feedback.transform;
            if (cue.ShowArrow && cue.Arrow == null && cue.Settings.ArrowPrefab != null)
            {
                GameObject arrow = (GameObject)PrefabUtility.InstantiatePrefab(cue.Settings.ArrowPrefab, cue.transform);
                Undo.RegisterCreatedObjectUndo(arrow, "Add feedback objects");
                arrow.transform.position = cue.ArrowAnchor != null ? cue.ArrowAnchor.position : top + Vector3.up * cue.Settings.ArrowHeight;
                arrow.SetActive(false);
                so.FindProperty("arrow").objectReferenceValue = arrow.transform;
            }
            so.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
        }
    }
}
