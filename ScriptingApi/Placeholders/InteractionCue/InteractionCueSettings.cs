using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// How interaction cues look: the rim materials of each state, the arrow, and their colour. Referenced by every
    /// <see cref="InteractionCuePlaceholder"/>, so it ships in the environment's bundle with the materials and the
    /// shaders they use. The SDK's asset, <c>Prefabs/InteractionCue/InteractionCueSettings.asset</c>, reproduces the
    /// Showcase highlight: a cyan rim that pulses on the next target. Duplicate it to change the look of a whole
    /// environment.
    /// </summary>
    /// <remarks>
    /// The rim materials use the shader <c>Virtuademy/InteractionCue/Rim</c>; the arrow uses
    /// <c>Virtuademy/InteractionCue/Arrow</c>. A cue swaps between these shared materials when its state changes, so
    /// cues that share a state draw with the same material.
    /// </remarks>
    [CreateAssetMenu(menuName = "Virtuademy/Interaction cue settings", fileName = "InteractionCueSettings")]
    public class InteractionCueSettings : ScriptableObject
    {
        [Header("Rim materials")]
        [SerializeField, Tooltip("Next target, pulsing (the default next-target look).")]
        private Material nextTargetPulse;
        [SerializeField, Tooltip("Next target, fixed.")]
        private Material nextTargetFixed;
        [SerializeField, Tooltip("Hovered by the local user.")]
        private Material hover;
        [SerializeField, Tooltip("Selected by the local user.")]
        private Material selected;
        [SerializeField, Tooltip("Right answer or action, shown for the cue's feedback time.")]
        private Material right;
        [SerializeField, Tooltip("Wrong answer or action, shown for the cue's feedback time.")]
        private Material wrong;

        [Header("Colour")]
        [SerializeField, ColorUsage(false, true), Tooltip("The colour of the next-target, hover and selected rims and " +
            "of the arrow, as set in their materials. A cue that overrides its colour starts from this one.")]
        private Color color = new(0f, 2.65f, 4f, 1f);

        [Header("Arrow")]
        [SerializeField, Tooltip("The \"go here\" arrow shown above a next target when the cue's Show Arrow is on.")]
        private GameObject arrowPrefab;
        [SerializeField, Min(0f), Tooltip("Height of the arrow above the object's bounds, in metres, when the cue has " +
            "no arrow anchor.")]
        private float arrowHeight = 0.25f;

        [SerializeField, Min(0.1f), Tooltip("The hover label's size: 1 is the Virtuademy label's own (body text 28 mm), 2 " +
            "reads from a few metres on desktop and in VR. The label and the check mark keep their size in the world " +
            "whatever the object's scale.")]
        private float labelScale = 2f;

        [Header("Feedback")]
        [SerializeField, Tooltip("The feedback objects Add feedback objects puts under a cue: label, check and cross mark, " +
            "flash, 2D audio source.")]
        private GameObject feedbackPrefab;
        [SerializeField, Tooltip("Played for a right action shown by a cue.")]
        private AudioClip rightClip;
        [SerializeField, Tooltip("Played for a wrong action shown by a cue.")]
        private AudioClip wrongClip;
        [SerializeField, Tooltip("Played when a task step is completed (procedures).")]
        private AudioClip taskCompletedClip;
        [SerializeField, Tooltip("Played when a procedure's summary shows.")]
        private AudioClip summaryClip;

        public Material NextTargetPulse => nextTargetPulse;
        public Material NextTargetFixed => nextTargetFixed;
        public Material Hover => hover;
        public Material Selected => selected;
        public Material Right => right;
        public Material Wrong => wrong;
        public Color Color => color;
        public GameObject ArrowPrefab => arrowPrefab;
        public float ArrowHeight => arrowHeight;
        public float LabelScale => labelScale;
        public GameObject FeedbackPrefab => feedbackPrefab;
        public AudioClip RightClip => rightClip;
        public AudioClip WrongClip => wrongClip;
        public AudioClip TaskCompletedClip => taskCompletedClip;
        public AudioClip SummaryClip => summaryClip;
    }
}
