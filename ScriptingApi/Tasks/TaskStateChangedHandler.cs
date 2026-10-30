using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Tasks
{
    /// <summary><paramref name="task"/> (the object with the Task) went from <paramref name="previous"/> to
    /// <paramref name="current"/>.</summary>
    public delegate void TaskStateChangedHandler(GameObject task, ETaskState previous, ETaskState current);
}
