using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.SDK.Environments.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Cue
{
    /// <summary>
    /// Sets a cue's base state from the status of the tasks it is a target of: next target while any of them is to do
    /// or in progress, completed when all are completed or failed, available otherwise. Listens to each task's
    /// <see cref="TaskNode.onStatusChanged"/>; nothing runs between two changes. In a procedure in Assessed mode the cue is
    /// never the next target. It also shows the tasks' results, from any player: a wrong result or attempt as Wrong, a
    /// completion as Right (not the tasks a step setter skips).
    /// </summary>
    internal sealed class InteractionCueTaskBinding
    {
        private readonly InteractionCuePlaceholder cue;
        private readonly List<TaskNode> nodes = new();
        private readonly List<Task> tasks = new();
        private readonly List<UnityEngine.Events.UnityAction<TaskStatus>> listeners = new();
        private bool lastResultWrong;

        // The cues bound to each task, for the procedure's feedback (hint targets, shared feedback objects, sounds).
        private static readonly Dictionary<Task, List<InteractionCuePlaceholder>> cuesByTask = new();

        /// <summary>The cues bound to <paramref name="task"/> (empty when none).</summary>
        public static IReadOnlyList<InteractionCuePlaceholder> CuesOf(Task task) =>
            cuesByTask.TryGetValue(task, out List<InteractionCuePlaceholder> list) ? list : System.Array.Empty<InteractionCuePlaceholder>();

        private InteractionCueTaskBinding(InteractionCuePlaceholder cue)
        {
            this.cue = cue;
        }

        /// <summary>The task nodes bound, in the order of the cue's tasks.</summary>
        public IReadOnlyList<TaskNode> Nodes => nodes;

        /// <summary>Binds the cue to its tasks (or its task elements' tasks); null when it has none.</summary>
        public static InteractionCueTaskBinding Create(InteractionCuePlaceholder cue)
        {
            List<GameObject> taskObjects = new();
            foreach (GameObject task in cue.Tasks)
            {
                if (task != null)
                {
                    taskObjects.Add(task);
                }
            }
            if (taskObjects.Count == 0)
            {
                foreach (IInteractionCueTaskSource source in cue.GetComponents<IInteractionCueTaskSource>())
                {
                    if (source.CueTask != null)
                    {
                        taskObjects.Add(source.CueTask);
                    }
                }
            }
            if (taskObjects.Count == 0)
            {
                return null;
            }
            InteractionCueTaskBinding binding = new(cue);
            foreach (GameObject taskObject in taskObjects)
            {
                if (!taskObject.TryGetComponent(out Task task) || task.Node == null)
                {
                    Debug.LogWarning("[InteractionCue] " + cue.name + ": " + taskObject.name + " in Tasks has no Task; it " +
                        "is ignored.", cue);
                    continue;
                }
                // By reference: nodes compare equal by their Guid, which copied nodes can share.
                if (!binding.nodes.Exists(n => ReferenceEquals(n, task.Node)))
                {
                    binding.nodes.Add(task.Node);
                    binding.tasks.Add(task);
                    TaskNode node = task.Node;
                    UnityEngine.Events.UnityAction<TaskStatus> listener = previous => binding.OnStatusChanged(node, previous);
                    binding.listeners.Add(listener);
                    node.onStatusChanged.AddListener(listener);
                    if (!cuesByTask.TryGetValue(task, out List<InteractionCuePlaceholder> list))
                    {
                        cuesByTask[task] = list = new List<InteractionCuePlaceholder>();
                    }
                    list.Add(cue);
                }
            }
            if (binding.nodes.Count == 0)
            {
                return null;
            }
            TasksApi.Instance.TaskResultReported += binding.OnResult;
            binding.Apply();
            return binding;
        }

        public void Dispose()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].onStatusChanged.RemoveListener(listeners[i]);
            }
            listeners.Clear();
            foreach (Task task in tasks)
            {
                if (cuesByTask.TryGetValue(task, out List<InteractionCuePlaceholder> list))
                {
                    list.Remove(cue);
                    if (list.Count == 0)
                    {
                        cuesByTask.Remove(task);
                    }
                }
            }
            TasksApi.Instance.TaskResultReported -= OnResult;
            nodes.Clear();
            tasks.Clear();
        }

        private void OnStatusChanged(TaskNode node, TaskStatus previous)
        {
            Apply();
            // A task done now (not one a step setter skips) shows Right, unless its completing result was wrong.
            if (node.Status == TaskStatus.Completed && !TaskStepSetter.IsSkipping && !lastResultWrong && cue.isActiveAndEnabled)
            {
                cue.ShowResultLocally(true);
            }
            lastResultWrong = false;
        }

        // Every result reported on a bound task, from this user or (in a shared procedure) another one.
        private void OnResult(GameObject task, TaskResult result)
        {
            if (!tasks.Exists(t => t != null && t.gameObject == task))
            {
                return;
            }
            lastResultWrong = result.Outcome == ETaskOutcome.Wrong;
            // A wrong action shows on the object that caused it, when that object has a cue of its own (a decoy, a
            // wrong choice); on every cue of the task otherwise. 2026-11-02: a decoy's wrong showed on the real target too.
            if (lastResultWrong && (!CausedByAnotherCue(result.Source) || IsSourceOf(result.Source)))
            {
                cue.ShowResultLocally(false);
            }
        }

        private bool IsSourceOf(GameObject source) =>
            source != null && source.GetComponentInParent<InteractionCuePlaceholder>(true) == cue;

        // The source is an object with a cue of its own, which shows the result itself.
        private static bool CausedByAnotherCue(GameObject source) =>
            source != null && source.GetComponentInParent<InteractionCuePlaceholder>(true) != null;

        /// <summary>The base state the tasks give, applied to the cue.</summary>
        public void Apply()
        {
            cue.SetBaseState(BaseState());
        }

        // In a procedure in Assessed mode nothing is highlighted as the next target.
        private bool Assessed()
        {
            foreach (Task task in tasks)
            {
                TaskSystemVirtuademy system = task != null ? task.GetComponentInParent<TaskSystemVirtuademy>(true) : null;
                if (system != null && system.Procedure != null && system.Procedure.Procedure.Mode == EProcedureMode.Assessed)
                {
                    return true;
                }
            }
            return false;
        }

        private EInteractionCueState BaseState()
        {
            bool allDone = true;
            foreach (TaskNode node in nodes)
            {
                switch (node.Status)
                {
                    case TaskStatus.Todo:
                    case TaskStatus.InProgress:
                        return Assessed() ? EInteractionCueState.Available : EInteractionCueState.NextTarget;
                    case TaskStatus.Completed:
                    case TaskStatus.Failed:
                        break;
                    default:
                        allDone = false;
                        break;
                }
            }
            return allDone ? EInteractionCueState.Completed : EInteractionCueState.Available;
        }
    }
}
