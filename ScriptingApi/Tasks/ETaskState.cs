namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>The status of a task, as the task system keeps it.</summary>
    public enum ETaskState
    {
        /// <summary>Not reachable yet.</summary>
        Locked = 0,
        /// <summary>To do: the task can be done now.</summary>
        Todo = 1,
        /// <summary>Started (a task with steps of its own).</summary>
        InProgress = 2,
        /// <summary>Done.</summary>
        Completed = 3,
        /// <summary>Failed.</summary>
        Failed = 4,
    }
}
