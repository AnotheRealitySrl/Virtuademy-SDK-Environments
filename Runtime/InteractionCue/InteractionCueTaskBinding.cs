using System.Collections.Generic;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.InteractionCue
{
    /// <summary>
    /// Sets a cue's base state from the status of the tasks it is a target of: next target while any of them is to do
    /// or in progress, completed when all are completed or failed, available otherwise. Listens to each task's
    /// <see cref="TaskNode.onStatusChanged"/>; nothing runs between two changes.
    /// </summary>
    internal sealed class InteractionCueTaskBinding
    {
        private readonly InteractionCuePlaceholder cue;
        private readonly List<TaskNode> nodes = new();

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
                    task.Node.onStatusChanged.AddListener(binding.OnStatusChanged);
                }
            }
            if (binding.nodes.Count == 0)
            {
                return null;
            }
            binding.Apply();
            return binding;
        }

        public void Dispose()
        {
            foreach (TaskNode node in nodes)
            {
                node.onStatusChanged.RemoveListener(OnStatusChanged);
            }
            nodes.Clear();
        }

        private void OnStatusChanged(TaskStatus previous) => Apply();

        /// <summary>The base state the tasks give, applied to the cue.</summary>
        public void Apply()
        {
            cue.SetBaseState(BaseState());
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
                        return EInteractionCueState.NextTarget;
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
