namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>How a procedure guides the user.</summary>
    public enum EProcedureMode
    {
        /// <summary>The next target is highlighted and right/wrong texts show at once.</summary>
        Guided = 0,
        /// <summary>No next-target highlight; right/wrong texts wait for the summary (marks and sounds still show).</summary>
        Assessed = 1,
    }

    /// <summary>When a hint costs points. A hint asked again on the same task never costs again (user decision,
    /// 2026-11-02: the hint is a pop-up the user may reopen at will; there is no "every request" charge).</summary>
    public enum EHintCharge
    {
        /// <summary>The first hint on a task costs the hint penalty; more on the same task are free.</summary>
        OncePerTask = 0,
        /// <summary>Hints are free.</summary>
        Never = 2,
    }

    /// <summary>What a fail condition (too many errors, time up, a failed task that ends the procedure) does.</summary>
    public enum EFailBehaviour
    {
        /// <summary>The procedure ends at once, failed.</summary>
        EndNow = 0,
        /// <summary>The procedure goes on and ends failed.</summary>
        ContinueAsFailed = 1,
    }

    /// <summary>What a failed task does to the procedure.</summary>
    public enum ETaskFailedBehaviour
    {
        /// <summary>The task scores 0 and the flow goes on to the next task.</summary>
        Continue = 0,
        /// <summary>A fail condition (see <see cref="EFailBehaviour"/>).</summary>
        EndProcedure = 1,
    }

    /// <summary>How the texts of right and wrong actions show.</summary>
    public enum EFeedbackPresentation
    {
        Toast = 0,
        Panel = 1,
        None = 2,
    }

    /// <summary>Which events the summary's timeline lists.</summary>
    public enum ETimelineMode
    {
        Off = 0,
        LostPointsOnly = 1,
        All = 2,
    }

    /// <summary>How Retry starts the procedure again. A shared procedure always retries in place.</summary>
    public enum ERetryMode
    {
        /// <summary>The session is loaded again.</summary>
        ReloadSession = 0,
        /// <summary>The task system is reverted in place, and a new run starts.</summary>
        RevertInPlace = 1,
    }

    /// <summary>Whether a task counts in the score.</summary>
    public enum ETaskScored
    {
        /// <summary>Counts when it is a leaf task (no sub-tasks) shown in the task panel.</summary>
        Auto = 0,
        Yes = 1,
        No = 2,
    }

    /// <summary>How a procedure ended.</summary>
    public enum EProcedureOutcome
    {
        Passed = 0,
        Failed = 1,
        /// <summary>Ended before every task was done (Abort, Exit, Retry, the scene unloading).</summary>
        Abandoned = 2,
    }

    /// <summary>Why a procedure failed.</summary>
    public enum EProcedureFailReason
    {
        /// <summary>No fail condition: a failed procedure with this reason scored below the passing score.</summary>
        None = 0,
        MaxErrors = 1,
        TimeLimit = 2,
        TaskFailed = 3,
        /// <summary>Ended by a script or node (Abort).</summary>
        Manual = 4,
    }
}
