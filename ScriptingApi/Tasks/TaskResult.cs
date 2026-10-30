using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>
    /// The result of an action on a task: how it went, a score from 0 to 1, what produced it and why. Pass it to
    /// <see cref="ITasksApi.CompleteTask(GameObject, TaskResult)"/> or <see cref="ITasksApi.ReportAttempt"/>, or receive
    /// it in <see cref="ITasksApi.TaskResultReported"/>.
    /// </summary>
    /// <remarks>A class, not a struct: interpreted scripts pass it through events (no value types in delegates).</remarks>
    public sealed class TaskResult
    {
        /// <summary>A result with the outcome's own score: Right 1, Wrong 0, Partial 0.5.</summary>
        public TaskResult(ETaskOutcome outcome)
            : this(outcome, DefaultScore(outcome), null, "", "")
        {
        }

        /// <summary>A result with every detail. <paramref name="score"/> is clamped to 0..1.</summary>
        /// <param name="source">What produced it (the object acted on, an element); may be null.</param>
        /// <param name="reason">Why, in a few words, for the summary's timeline; may be empty.</param>
        /// <param name="data">Anything the environment wants to keep with it (the answer chosen, say); may be empty.</param>
        public TaskResult(ETaskOutcome outcome, float score, GameObject source, string reason, string data)
        {
            Outcome = outcome;
            Score = Mathf.Clamp01(score);
            Source = source;
            Reason = reason ?? "";
            Data = data ?? "";
            IsLocalActor = true;
            ActorName = "";
        }

        /// <summary>A right result from <paramref name="source"/>.</summary>
        public static TaskResult Right(GameObject source) => new(ETaskOutcome.Right, 1f, source, "", "");

        /// <summary>A wrong result from <paramref name="source"/>, with a reason.</summary>
        public static TaskResult Wrong(GameObject source, string reason) => new(ETaskOutcome.Wrong, 0f, source, reason, "");

        public ETaskOutcome Outcome { get; }

        /// <summary>From 0 to 1.</summary>
        public float Score { get; }

        public GameObject Source { get; }

        public string Reason { get; }

        public string Data { get; }

        /// <summary>Whether this client's user produced it. Set by the task system: false for a result received from
        /// another user of a shared procedure.</summary>
        public bool IsLocalActor { get; internal set; }

        /// <summary>The name of the user who produced it, for a result received from another user; empty for this one's.
        /// Set by the task system.</summary>
        public string ActorName { get; internal set; }

        private static float DefaultScore(ETaskOutcome outcome) => outcome switch
        {
            ETaskOutcome.Right => 1f,
            ETaskOutcome.Partial => 0.5f,
            _ => 0f,
        };

        public override string ToString() => Outcome + " (" + Score.ToString("0.##") + ")"
            + (Reason.Length > 0 ? ": " + Reason : "");
    }
}
