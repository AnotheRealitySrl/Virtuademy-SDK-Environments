namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the player enters the zone (a trigger collider on this object) and the task is completed, Right.
    /// Only this client's player is detected here (each client reports for its own player), so a teammate reaching the
    /// zone completes it for the room from their client.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class ReachZoneElementPlaceholder : ElementPlaceholderBase
    {
    }
}
