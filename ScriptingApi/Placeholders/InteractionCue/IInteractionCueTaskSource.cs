using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// A placeholder on a cue's object that names the task the object is a target of (the task elements). A cue with no
    /// tasks of its own follows these. Internal: the SDK runtime reads it.
    /// </summary>
    internal interface IInteractionCueTaskSource
    {
        GameObject CueTask { get; }
    }
}
