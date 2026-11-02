using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Question element: a quiz (or the quizzes drawn from the pool) answered to the end completes the task
    /// with its normalised score, 0 (Wrong) below the element's Wrong Below.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class QuestionElementDriver : ElementDriver
    {
        private readonly List<QuizPlaceholder> drawn = new();
        private int current;
        private float scoreSum;
        private Task watchedTask;

        private QuestionElementPlaceholder Question => (QuestionElementPlaceholder)Element;

        // The question elements awake, for a shared procedure's snapshot (the master's draws).
        private static readonly List<QuestionElementDriver> all = new();

        internal static IReadOnlyList<QuestionElementDriver> All => all;

        internal static QuestionElementDriver For(Task task) => task == null ? null : all.Find(q => q.TaskForSync == task);

        /// <summary>The task this question reports on (null without one).</summary>
        internal Task TaskForSync => TaskOf();

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);

        protected override void OnBind()
        {
            Draw();
            watchedTask = TaskOf();
            if (watchedTask != null)
            {
                // A new run (a revert locks the task again) draws again.
                watchedTask.Node.onStatusChanged.AddListener(OnTaskStatusChanged);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (watchedTask != null)
            {
                watchedTask.Node.onStatusChanged.RemoveListener(OnTaskStatusChanged);
            }
        }

        private void OnTaskStatusChanged(TaskStatus previous)
        {
            if (watchedTask.Node.Status == TaskStatus.Locked && previous != TaskStatus.Locked)
            {
                Draw();
            }
        }

        /// <summary>Draws the quizzes of this run (the single quiz without a pool) and shows the first.</summary>
        internal void Draw()
        {
            Unsubscribe();
            drawn.Clear();
            current = 0;
            scoreSum = 0f;
            if (Question.Pool.Count == 0)
            {
                QuizPlaceholder single = Question.Quiz != null ? Question.Quiz : GetComponent<QuizPlaceholder>();
                if (single == null)
                {
                    Debug.LogWarning("[QuestionElement] " + name + " has no quiz: set Quiz or the Pool.", this);
                    Question.Drawn = new QuizPlaceholder[0];
                    return;
                }
                drawn.Add(single);
            }
            else
            {
                List<QuizPlaceholder> candidates = new();
                foreach (QuizPlaceholder q in Question.Pool)
                {
                    if (q != null)
                    {
                        candidates.Add(q);
                    }
                }
                if (Question.Shuffle)
                {
                    for (int i = candidates.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                    }
                }
                int count = Question.DrawCount <= 0 ? candidates.Count : Mathf.Min(Question.DrawCount, candidates.Count);
                drawn.AddRange(candidates.GetRange(0, count));
                foreach (QuizPlaceholder q in Question.Pool)
                {
                    if (q != null)
                    {
                        q.gameObject.SetActive(false);
                    }
                }
            }
            Question.Drawn = drawn.ToArray();
            ShowCurrent();
        }

        /// <summary>Uses <paramref name="order"/> (indices into the pool) as this run's draw: a shared procedure's
        /// clients show the quizzes the master drew.</summary>
        internal void UseDraw(int[] order)
        {
            Unsubscribe();
            drawn.Clear();
            current = 0;
            scoreSum = 0f;
            foreach (QuizPlaceholder q in Question.Pool)
            {
                if (q != null)
                {
                    q.gameObject.SetActive(false);
                }
            }
            foreach (int i in order)
            {
                if (i >= 0 && i < Question.Pool.Count && Question.Pool[i] != null)
                {
                    drawn.Add(Question.Pool[i]);
                }
            }
            Question.Drawn = drawn.ToArray();
            ShowCurrent();
        }

        /// <summary>This run's draw, as indices into the pool (empty without a pool).</summary>
        internal int[] DrawOrder()
        {
            List<int> order = new();
            foreach (QuizPlaceholder q in drawn)
            {
                int i = IndexInPool(q);
                if (i >= 0)
                {
                    order.Add(i);
                }
            }
            return order.ToArray();
        }

        private int IndexInPool(QuizPlaceholder quiz)
        {
            for (int i = 0; i < Question.Pool.Count; i++)
            {
                if (Question.Pool[i] == quiz)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ShowCurrent()
        {
            if (current >= drawn.Count)
            {
                return;
            }
            QuizPlaceholder quiz = drawn[current];
            if (Question.Pool.Count > 0)
            {
                quiz.gameObject.SetActive(true);
            }
            quiz.onFinish ??= new UnityEngine.Events.UnityEvent();
            quiz.onFinish.AddListener(OnQuizFinished);
        }

        private void Unsubscribe()
        {
            foreach (QuizPlaceholder q in drawn)
            {
                if (q != null)
                {
                    q.onFinish?.RemoveListener(OnQuizFinished);
                    q.onContinue?.RemoveListener(OnQuizContinued);
                }
            }
        }

        private void OnQuizContinued()
        {
            QuizPlaceholder done = drawn[current - 1];
            done.onContinue.RemoveListener(OnQuizContinued);
            if (Question.Pool.Count > 0)
            {
                done.gameObject.SetActive(false);
            }
            ShowCurrent();
        }

        private void OnQuizFinished()
        {
            QuizPlaceholder quiz = drawn[current];
            quiz.onFinish.RemoveListener(OnQuizFinished);
            float range = quiz.QuizInstanceAllGoodScore - quiz.QuizInstanceAllBadScore;
            scoreSum += range <= 0f ? 1f : Mathf.Clamp01((quiz.QuizInstanceScore - quiz.QuizInstanceAllBadScore) / range);
            // The quiz's own recap (each answer right or wrong): Confirm alone shows nothing more. Continue leads to the
            // next quiz of the pool; the last quiz's recap ends the question, with no button (2026-11-02, editor tests:
            // the quizzes ended without a recap, then Continue stayed on the last one and led nowhere).
            bool last = current + 1 >= drawn.Count;
            if (!last)
            {
                quiz.onContinue ??= new UnityEngine.Events.UnityEvent();
                quiz.onContinue.AddListener(OnQuizContinued);
            }
            quiz.VSNode_GoToResults(false, false, !last);
            current++;
            if (current < drawn.Count)
            {
                return;
            }
            float score = scoreSum / drawn.Count;
            // Below the creator's threshold the question is answered wrong (2026-11-02, Elements: one wrong answer of three
            // scored 1/3, a Partial, and the procedure gave the positive feedback)
            if (score < Question.WrongBelow)
            {
                score = 0f;
            }
            ETaskOutcome outcome = score >= 1f ? ETaskOutcome.Right : score <= 0f ? ETaskOutcome.Wrong : ETaskOutcome.Partial;
            Report(new TaskResult(outcome, score, gameObject, "", ""), true);
        }
    }
}
