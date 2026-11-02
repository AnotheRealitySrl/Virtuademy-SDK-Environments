using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Question element: a quiz (or the quizzes drawn from the pool) answered to the end completes the task
    /// with its normalised score.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class QuestionElementDriver : ElementDriver
    {
        private readonly List<QuizPlaceholder> drawn = new();
        private int current;
        private float scoreSum;
        private Task watchedTask;
        private bool panned;

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
                ExitPan();
                Draw();
            }
            else if (IsToDo(watchedTask.Node.Status) && !IsToDo(previous))
            {
                PanToCurrent();
            }
        }

        private static bool IsToDo(TaskStatus status) => status == TaskStatus.Todo || status == TaskStatus.InProgress;

        // Desktop and mobile, Pan To Quiz (2026-11-02, editor test: the sample's quiz was out of reach, nothing took the
        // camera to it): the pan state the POIs use, at the quiz's camera point.
        private void PanToCurrent()
        {
            if (!Question.PanToQuiz || current >= drawn.Count || !IVirtuademyGameplay.IsAvailable
                || IVirtuademyGameplay.Current.Platform.IsVR)
            {
                return;
            }
            QuizPlaceholder quiz = drawn[current];
            Transform point = quiz.CameraPanTransform != null ? quiz.CameraPanTransform : quiz.transform;
            panned = true;
            StartCoroutine(IVirtuademyGameplay.Current.Player.PanCameraAround(point));
        }

        private void ExitPan()
        {
            if (!panned || !IVirtuademyGameplay.IsAvailable)
            {
                return;
            }
            panned = false;
            StartCoroutine(IVirtuademyGameplay.Current.Player.ExitCameraPan());
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
            if (current >= drawn.Count)
            {
                // The last quiz: its recap stays, movement comes back
                ExitPan();
                return;
            }
            if (Question.Pool.Count > 0)
            {
                done.gameObject.SetActive(false);
            }
            ShowCurrent();
            if (panned)
            {
                PanToCurrent();
            }
        }

        private void OnQuizFinished()
        {
            QuizPlaceholder quiz = drawn[current];
            quiz.onFinish.RemoveListener(OnQuizFinished);
            float range = quiz.QuizInstanceAllGoodScore - quiz.QuizInstanceAllBadScore;
            scoreSum += range <= 0f ? 1f : Mathf.Clamp01((quiz.QuizInstanceScore - quiz.QuizInstanceAllBadScore) / range);
            // The quiz's own recap (each answer right or wrong), with Continue only: Confirm alone shows nothing more
            // (2026-11-02, editor test: the quizzes ended without it). The next quiz of a pool shows on Continue.
            quiz.onContinue ??= new UnityEngine.Events.UnityEvent();
            quiz.onContinue.AddListener(OnQuizContinued);
            quiz.VSNode_GoToResults(false, false, true);
            current++;
            if (current < drawn.Count)
            {
                return;
            }
            float score = scoreSum / drawn.Count;
            ETaskOutcome outcome = score >= 1f ? ETaskOutcome.Right : score <= 0f ? ETaskOutcome.Wrong : ETaskOutcome.Partial;
            Report(new TaskResult(outcome, score, gameObject, "", ""), true);
        }
    }
}
