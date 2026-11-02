namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>How a procedure ended: outcome, score, time, errors, hints, one row per scored task, and the timeline.
    /// Given by <c>ProcedurePlaceholder.Finished</c>.</summary>
    public sealed class ProcedureSummary
    {
        public EProcedureOutcome Outcome { get; internal set; }
        public EProcedureFailReason FailReason { get; internal set; }
        public int Score { get; internal set; }
        public int MaxScore { get; internal set; }
        /// <summary>The score needed to pass: round(MaxScore x pass percent / 100).</summary>
        public int PassingScore { get; internal set; }
        /// <summary>Score as a percentage of MaxScore, 0..100.</summary>
        public float Percent { get; internal set; }
        public float ElapsedSeconds { get; internal set; }
        public int Errors { get; internal set; }
        public int HintsUsed { get; internal set; }
        public ProcedureTaskRow[] Tasks { get; internal set; } = new ProcedureTaskRow[0];
        public ProcedureTimelineEntry[] Timeline { get; internal set; } = new ProcedureTimelineEntry[0];

        internal ProcedureSummary()
        {
        }
    }

    /// <summary>One scored task of a <see cref="ProcedureSummary"/>.</summary>
    public sealed class ProcedureTaskRow
    {
        /// <summary>The task's text (its description).</summary>
        public string Name { get; internal set; }
        public string StepId { get; internal set; }
        public int Points { get; internal set; }
        public int MaxPoints { get; internal set; }
        public ETaskState State { get; internal set; }
        public int WrongAttempts { get; internal set; }
        public int HintsUsed { get; internal set; }

        internal ProcedureTaskRow()
        {
        }
    }

    /// <summary>One event of a procedure's timeline.</summary>
    public sealed class ProcedureTimelineEntry
    {
        /// <summary>Seconds since the procedure started.</summary>
        public float Seconds { get; internal set; }
        /// <summary>The task's text; empty for an event of the whole procedure (a distractor without a task, time up).</summary>
        public string Step { get; internal set; }
        /// <summary>Who did it: empty for this client's user.</summary>
        public string Actor { get; internal set; }
        /// <summary>A good action (true) or a mistake (false).</summary>
        public bool Good { get; internal set; }
        /// <summary>Points lost by it (0 for a good action).</summary>
        public float LostPoints { get; internal set; }
        public string Reason { get; internal set; }

        internal ProcedureTimelineEntry()
        {
        }
    }

    /// <summary>A procedure ended.</summary>
    public delegate void ProcedureFinishedHandler(ProcedureSummary summary);

    /// <summary>Something happened to a procedure (score changed, passed, failed); read its state on the placeholder.</summary>
    public delegate void ProcedureEventHandler(UnityEngine.GameObject procedure);

    /// <summary>A hint was used on <paramref name="task"/>; <paramref name="hintsUsed"/> is the procedure's total.</summary>
    public delegate void ProcedureHintHandler(UnityEngine.GameObject task, int hintsUsed);

    /// <summary>Something happened to <paramref name="task"/> (unlocked, hint shown).</summary>
    public delegate void TaskEventHandler(UnityEngine.GameObject task);

    /// <summary><paramref name="task"/> failed, with the reason given.</summary>
    public delegate void TaskFailedHandler(UnityEngine.GameObject task, string reason);
}
