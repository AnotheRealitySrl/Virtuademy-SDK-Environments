using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>How <see cref="ProcedurePlaceholder"/> reaches the procedure run the SDK runtime gives it. Internal: a
    /// script uses the placeholder's members.</summary>
    internal interface IProcedureSink
    {
        bool IsRunning { get; }
        bool IsFinished { get; }
        int Score { get; }
        int MaxScore { get; }
        int PassingScore { get; }
        float Percent { get; }
        int Errors { get; }
        int HintsUsed { get; }
        float ElapsedSeconds { get; }
        float RemainingSeconds { get; }
        EProcedureOutcome Outcome { get; }
        string RunKey { get; }
        ProcedureSummary Summary { get; }

        void RequestHint();
        void Finish();
        void Abort();
        void Retry();
        void Exit();
        void ShowSummary();
    }

    /// <summary>How <see cref="TaskStepPlaceholder"/> reports to the task system. Internal.</summary>
    internal interface ITaskStepSink
    {
        void Complete(TaskResult result);
        void Attempt(TaskResult result);
        void Fail(string reason);
        void Unlock();
        void ShowHint();
    }
}
