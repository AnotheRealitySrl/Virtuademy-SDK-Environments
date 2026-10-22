namespace Virtuademy.Environments.ScriptingApi.UI
{
    /// <summary>
    /// The role of an SDK panel: its accent bar and icon colour. <c>Default</c> for a description or a
    /// welcome, <c>Error</c> for a wrong answer, <c>Success</c> for a right one.
    /// </summary>
    public enum EPanelVariant
    {
        Default = 0,
        Hint = 1,
        Warning = 2,
        Error = 3,
        Success = 4,
    }
}
