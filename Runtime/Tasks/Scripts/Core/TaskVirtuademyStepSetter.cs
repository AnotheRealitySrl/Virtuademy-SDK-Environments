using Unity.VisualScripting;
using SPACS.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Virtuademy.SDK.Environments.Tasks
{
    [RenamedFrom("Virtuademy.SDK.Environments.Tasks.TaskReflectisStepSetter")]
    public class TaskVirtuademyStepSetter : TaskStepSetter
    {
        protected ITasksRPCManager rpcManagerInterface;
        private TaskSystemVirtuademy systemVirtuademy;

        private void Start()
        {
            systemVirtuademy = GetComponent<TaskSystemVirtuademy>();
            if (systemVirtuademy.isNetworked)
            {
                StartCoroutine(WaitForRPCManager());
            }

        }

        private IEnumerator WaitForRPCManager()
        {
            while (rpcManagerInterface == null)
            {
                rpcManagerInterface = systemVirtuademy.rpcManagerInterface;
                yield return null;
            }
            rpcManagerInterface = systemVirtuademy.rpcManagerInterface;
            rpcManagerInterface.AddJoinRoomEvent(Init);
        }

        private void OnDestroy()
        {
            // AddJoinRoomEvent subscribes to OnInitRoom: unsubscribe through the event, so a
            // destroyed setter is not called back on the next room join.
            if (rpcManagerInterface != null)
                rpcManagerInterface.OnInitRoom -= Init;
        }

        private void Init(int id)
        {
            if(id != -1){
                systemVirtuademy.Prepare();
            }
            //calculate last node
            var tasks = FindObjectsOfType<TaskVirtuademy>();
            TaskNode targetNode = null;
            foreach (var task in tasks)
            {
                if (task.TaskID == id)
                {
                    targetNode = task.Node;
                    break;
                }
            }

            if (targetNode == null)
            {
                if (id != -1)
                    Debug.LogWarning($"[TaskVirtuademyStepSetter] No task with id {id} in the scene: the task state of the room is not restored.");
                return;
            }

            TaskNode newNode = targetNode;
            if (targetNode.Dependencies.Count != 0 && targetNode.Next != null)
            {
                newNode = targetNode.Next;
            }

            // Ordered list of tasks. I assign the state "Complete" until I find the node I want.
            IReadOnlyCollection<TaskNode> allNodes = systemVirtuademy.Tasks;
            foreach (TaskNode node in allNodes)
            {
                if (CompleteTaskRecursive(node, newNode))
                {
                    // Target reached. Nothing else to do in this loop.
                    if (targetNode.Status == TaskNode.TaskStatus.Todo)
                        targetNode.Status = TaskNode.TaskStatus.Completed;
                    break;
                }
            }
        }
    }
}
