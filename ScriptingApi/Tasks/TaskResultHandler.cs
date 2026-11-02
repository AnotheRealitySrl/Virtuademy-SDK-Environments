using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary>A result reported on <paramref name="task"/> (the object with the Task).</summary>
    public delegate void TaskResultHandler(GameObject task, TaskResult result);
}
