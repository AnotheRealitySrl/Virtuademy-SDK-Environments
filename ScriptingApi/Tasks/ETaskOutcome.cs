namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>How a reported action on a task went.</summary>
    public enum ETaskOutcome
    {
        /// <summary>Right: full score (1) unless the result says otherwise.</summary>
        Right = 0,
        /// <summary>Wrong: score 0. As an attempt it counts as an error.</summary>
        Wrong = 1,
        /// <summary>Partly right: score 0.5 unless the result says otherwise.</summary>
        Partial = 2,
    }
}
