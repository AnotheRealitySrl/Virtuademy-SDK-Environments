using Unity.VisualScripting;
using SPACS.Tasks;
using SPACS.Tasks.UI;

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.Tasks
{
    [RenamedFrom("Virtuademy.SDK.Environments.Tasks.TaskSystemReflectis")]
    public class TaskSystemVirtuademy : TaskSystem
    {
        public ITasksRPCManager rpcManagerInterface { get; private set; }
        //public static TaskSystemVirtuademy Instance { get; private set; }

        public TaskUIManager taskUIManager;
        private int initializedTaskDescriptions = 0;

        [SerializeField] private bool _isNetworked;
        public bool isNetworked
        {
            private set
            {
                _isNetworked = value;
            }
            get
            {
                return _isNetworked && IVirtuademyFramework.Current.Session.IsMultiplayer;
            }
        }


        // VisualScripting usage
        public UnityEvent OnTaskSystemReady => taskSystemReady;

        //UI will link tot his callback
        public UnityEvent OnRebuildUI;


        //questo deve prendersi istanza dell'rpcManager che viene generato tramite il placeholder, in realt� si piglia l'interfaccia. 
        //dovr� poi aspettare che mi arrivi questo componennt prima di fare il Prepare nel caso in cui io sia networkato

        protected override void Awake()
        {
            // If there is an instance, and it's not me, delete myself.

            //if (Instance != null && Instance != this)
            //{
            //    Destroy(this);
            //}
            //else
            //{
            //    Instance = this;
            //}

            initializedTaskDescriptions = 0;

            ForwardStateChanges();

            /*if (isNetworked)
            {
                Debug.Log(gameObject.GetComponent<ITasksRPCManager>());
            }*/

            //TODO wait for all taskNode to update their descriptions....

            if (isNetworked)
            {
                // A manager already on the object (one baked into a prefab) is ready now; otherwise the application's
                // bridge hands one over with SetRpcManager.
                if (rpcManagerInterface == null && TryGetComponent(out ITasksRPCManager baked))
                {
                    SetRpcManager(baked);
                }
                WhenRpcManagerReady(manager => manager.SetOnRevert(base.Revert));
            }

            if (startImmediately)
            {
                base.Awake();
            }
        }

        // Every task of this system tells ITasksApi.TaskStateChanged when its status changes, with the task's object.
        private void ForwardStateChanges()
        {
            TasksApi.Instance.SystemAwake();
            Dictionary<TaskNode, GameObject> objects = new();
            foreach (Task task in GetComponentsInChildren<Task>(true))
            {
                if (task.Node != null)
                {
                    objects[task.Node] = task.gameObject;
                }
            }
            foreach (TaskNode node in Tasks)
            {
                if (objects.TryGetValue(node, out GameObject taskObject))
                {
                    Task task = taskObject.GetComponent<Task>();
                    node.onStatusChanged.AddListener(previous => OnTaskStatusChanged(task, previous));
                }
            }
        }

        // One listener per task, so the order is fixed: the step's events, the procedure, then ITasksApi.
        private void OnTaskStatusChanged(Task task, TaskNode.TaskStatus previous)
        {
            TaskNode.TaskStatus status = task.Node.Status;
            TaskResult result = status == TaskNode.TaskStatus.Completed ? TaskReports.TakePending(task) : null;
            string failReason = status == TaskNode.TaskStatus.Failed ? TaskReports.TakeFailReason(task) : null;
            if (task.TryGetComponent(out TaskStepPlaceholder step))
            {
                if (status == TaskNode.TaskStatus.Todo)
                {
                    step.RaiseUnlocked();
                }
                else if (status == TaskNode.TaskStatus.Completed)
                {
                    step.RaiseCompleted(result ?? new TaskResult(ETaskOutcome.Right, 1f, task.gameObject, "", ""));
                }
                else if (status == TaskNode.TaskStatus.Failed)
                {
                    step.RaiseFailed(failReason ?? "");
                }
            }
            Procedure?.OnTaskStatusChanged(task, previous, result, failReason);
            TasksApi.Instance.RaiseStateChanged(task.gameObject, (ETaskState)previous, (ETaskState)status);
        }

        /// <summary>The procedure run of this task system, when it has a Procedure placeholder.</summary>
        internal ProcedureRun Procedure { get; set; }

        // With a procedure whose failed tasks continue, a failed task lets the flow go on as a completed one does.
        protected override bool AdvancesFlow(TaskNode task) => task.Status == TaskNode.TaskStatus.Completed
            || (task.Status == TaskNode.TaskStatus.Failed && Procedure != null && Procedure.FailedTasksAdvance);

        protected virtual void OnDestroy()
        {
            TasksApi.Instance.SystemDestroyed();
        }

        private event Action<ITasksRPCManager> RpcManagerReady;

        /// <summary>
        /// Hands the task system its RPC manager (the application's task network bridge does, in multiplayer) and runs
        /// what was waiting for it. Replaces polling for the component every frame.
        /// </summary>
        public void SetRpcManager(ITasksRPCManager manager)
        {
            if (manager == null || ReferenceEquals(rpcManagerInterface, manager))
            {
                return;
            }
            rpcManagerInterface = manager;
            Action<ITasksRPCManager> waiting = RpcManagerReady;
            RpcManagerReady = null;
            waiting?.Invoke(manager);
        }

        /// <summary>Runs <paramref name="onReady"/> with the RPC manager: now if it is there, otherwise once it is set.</summary>
        public void WhenRpcManagerReady(Action<ITasksRPCManager> onReady)
        {
            if (rpcManagerInterface != null)
            {
                onReady(rpcManagerInterface);
            }
            else
            {
                RpcManagerReady += onReady;
            }
        }

        //function called when the descriptions on the tasks have been initialized in the localizeTaskBridge
        public void InitializedTaskDescription()
        {
            initializedTaskDescriptions += 1;

            //this simply rebuilds the UI
            //taskSystemReady.Invoke();

            if (initializedTaskDescriptions == Tasks.Count)
            {
                //Debug.LogError("AllTasks initialized!");
                if (taskUIManager != null)
                {
                    taskUIManager.RebuildUIImmediately();
                }
                OnRebuildUI?.Invoke();
            }
        }

        public override void Revert()
        {
            Procedure?.OnSystemReverted();
            if (isNetworked)
            {
                rpcManagerInterface.SendRPCTaskRevert();
                rpcManagerInterface.UpdateTasksID(-1);
            }
            base.Revert();
        }

        public void RebuildTaskUI()
        {
            if (taskUIManager != null)
            {
                taskUIManager.RebuildUIImmediately();
            }
            OnRebuildUI?.Invoke();
        }
    }
}
