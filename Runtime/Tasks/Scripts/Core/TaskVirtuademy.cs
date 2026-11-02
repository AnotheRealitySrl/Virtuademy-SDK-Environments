using Unity.VisualScripting;
using SPACS.Tasks;
using UnityEngine;
using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    [RenamedFrom("Virtuademy.SDK.Environments.Tasks.TaskReflectis")]
    public class TaskVirtuademy : Task
    {
        protected bool forceCompleted = false;
        [SerializeField]
        protected int taskID;

        protected ITasksRPCManager rpcManagerInterface;
        private TaskSystemVirtuademy taskSystem;

        public int TaskID { get => taskID; }

        protected void Start()
        {
            taskSystem = GetComponentInParent<TaskSystemVirtuademy>();
            if (taskSystem.isNetworked)
            {
                taskSystem.WhenRpcManagerReady(OnRpcManagerReady);
            }
        }

        private void OnRpcManagerReady(ITasksRPCManager manager)
        {
            rpcManagerInterface = manager;
            rpcManagerInterface.SetOnTaskCompleted(ForceTaskComplete);
            forceCompleted = false;
        }

        protected override void OnStatusChanged(TaskStatus oldStatus)
        {
            if ((Node.Status == TaskStatus.Completed && !forceCompleted) && taskSystem.isNetworked)
            {

                taskSystem.rpcManagerInterface.UpdateTasksID(taskID);
                taskSystem.rpcManagerInterface.SendRPCTaskStatusChange(taskID);
            }

            forceCompleted = false;

            base.OnStatusChanged(oldStatus);
        }

        public void ForceTaskComplete(int id)
        {
            if (taskID != id)
                return;

            forceCompleted = true;
            CompleteTask();
        }

        //Auto-assign random id when task is created from graph
        protected void Reset()
        {
            taskID = Mathf.Abs(gameObject.GetInstanceID());
        }

        public override void AddDetector()
        {
            base.AddDetector();
        }
    }
}
