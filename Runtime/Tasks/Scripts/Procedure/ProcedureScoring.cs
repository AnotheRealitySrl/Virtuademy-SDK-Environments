using System;
using System.Collections.Generic;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The arithmetic of a procedure, apart from any scene object: each scored task's maximum points, what it earned,
    /// the total, the passing score. Plain C#, so it is tested on its own.
    /// </summary>
    public sealed class ProcedureScoring
    {
        /// <summary>One scored task: its weight, and what happened to it.</summary>
        public sealed class TaskScore
        {
            public float Weight = 1f;
            /// <summary>Completed by a step setter (start from a later step, late-join restore): out of the score.</summary>
            public bool Skipped;
            public bool Done;
            public bool Failed;
            /// <summary>The completing result's score, 0..1.</summary>
            public float ResultScore;
            public int WrongAttempts;
            public int HintRequests;
            /// <summary>Set by <see cref="ProcedureScoring.Recompute"/>.</summary>
            public int MaxPoints;
            /// <summary>Set by <see cref="ProcedureScoring.Recompute"/>.</summary>
            public int Points;
        }

        public int MaxScoreSetting = 100;
        public float PassPercent = 60f;
        public float ErrorPenalty = 0.25f;
        public float HintPenalty = 0.25f;
        public EHintCharge HintCharge = EHintCharge.OncePerTask;

        public readonly List<TaskScore> Tasks = new();

        /// <summary>Points taken off the total by distractors.</summary>
        public float DistractorPenalty;

        /// <summary>The maximum score of the tasks not skipped (the setting when none is).</summary>
        public int MaxScore { get; private set; }
        public int Score { get; private set; }
        public int PassingScore => Mathf.RoundToInt(MaxScore * PassPercent / 100f);
        public float Percent => MaxScore <= 0 ? 0f : 100f * Score / MaxScore;

        /// <summary>Hints that cost points on a task, as <see cref="HintCharge"/> says.</summary>
        public int ChargedHints(TaskScore task) => HintCharge switch
        {
            EHintCharge.OncePerTask => Math.Min(task.HintRequests, 1),
            _ => 0,
        };

        /// <summary>
        /// Shares <see cref="MaxScoreSetting"/> out by weight among the tasks not skipped (largest remainder, so the
        /// shares add up exactly), scaled by the share of weight left after skipped tasks, then each task's points and
        /// the total.
        /// </summary>
        public void Recompute()
        {
            float totalWeight = 0f, liveWeight = 0f;
            foreach (TaskScore t in Tasks)
            {
                totalWeight += t.Weight;
                if (!t.Skipped)
                {
                    liveWeight += t.Weight;
                }
            }
            // Skipped tasks leave their share out: the maximum shrinks, so a run that started from step 3 is out of
            // what it could earn.
            MaxScore = totalWeight <= 0f ? 0 : Mathf.RoundToInt(MaxScoreSetting * liveWeight / totalWeight);
            ShareOut(MaxScore, liveWeight);
            float earned = 0f;
            foreach (TaskScore t in Tasks)
            {
                if (t.Skipped)
                {
                    t.Points = 0;
                    continue;
                }
                if (!t.Done || t.Failed)
                {
                    t.Points = 0;
                    continue;
                }
                float fraction = Mathf.Clamp01(t.ResultScore - ErrorPenalty * t.WrongAttempts - HintPenalty * ChargedHints(t));
                t.Points = Mathf.RoundToInt(t.MaxPoints * fraction);
                earned += t.Points;
            }
            Score = Mathf.Max(0, Mathf.RoundToInt(earned - DistractorPenalty));
        }

        private void ShareOut(int total, float liveWeight)
        {
            List<(TaskScore task, float remainder, int index)> remainders = new();
            int given = 0;
            for (int i = 0; i < Tasks.Count; i++)
            {
                TaskScore t = Tasks[i];
                if (t.Skipped || liveWeight <= 0f)
                {
                    t.MaxPoints = 0;
                    continue;
                }
                float exact = total * t.Weight / liveWeight;
                t.MaxPoints = Mathf.FloorToInt(exact);
                given += t.MaxPoints;
                remainders.Add((t, exact - t.MaxPoints, i));
            }
            // Largest remainder first; on a tie, the earlier task (List.Sort is not stable, so the index decides).
            remainders.Sort((a, b) => a.remainder != b.remainder ? b.remainder.CompareTo(a.remainder) : a.index.CompareTo(b.index));
            for (int i = 0; given < total && i < remainders.Count; i++, given++)
            {
                remainders[i].task.MaxPoints++;
            }
        }

        /// <summary>Passed: the score reaches the passing score and no fail condition happened.</summary>
        public bool Passes(bool failCondition) => !failCondition && Score >= PassingScore;
    }
}
