using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: a quiz answered to the end completes the task with its score,
    /// <c>(score - all wrong) / (all right - all wrong)</c>: Right at 1, Wrong at 0, Partial between. With a pool, the
    /// element draws <see cref="DrawCount"/> quizzes and shows them one after the other; the result is their mean.
    /// </summary>
    /// <remarks>The pool follows the model of the kit's QuizRandomizer (quiz list, number of quizzes), in C#; the legacy
    /// randomizer prefabs are unchanged. The quizzes of the pool not drawn, and those not current, are kept inactive.</remarks>
    [DisallowMultipleComponent]
    public class QuestionElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Question")]
        [SerializeField, Tooltip("The quiz. Empty: the quiz on this object. Ignored when the pool has quizzes.")]
        private QuizPlaceholder quiz;

        [SerializeField, Tooltip("Quizzes to draw from. Empty: the single quiz above.")]
        private List<QuizPlaceholder> pool = new();

        [SerializeField, Min(0), Tooltip("How many quizzes of the pool are drawn. 0: all of them.")]
        private int drawCount = 0;

        [SerializeField, Tooltip("Draw at random (otherwise in the pool's order).")]
        private bool shuffle = true;

        [SerializeField, Tooltip("Desktop and mobile: when the task becomes the one to do, the camera pans to the quiz (its " +
            "Camera Pan Transform), as it does to a POI, and gives movement back after the last quiz's Continue. Not in VR.")]
        private bool panToQuiz = true;

        public QuizPlaceholder Quiz { get => quiz; set => quiz = value; }
        public IReadOnlyList<QuizPlaceholder> Pool => pool;
        public int DrawCount { get => drawCount; set => drawCount = Mathf.Max(0, value); }
        public bool Shuffle { get => shuffle; set => shuffle = value; }
        public bool PanToQuiz { get => panToQuiz; set => panToQuiz = value; }

        /// <summary>The quizzes drawn for the current run, in the order they are shown (filled by the runtime).</summary>
        public QuizPlaceholder[] Drawn { get; internal set; } = new QuizPlaceholder[0];
    }
}
