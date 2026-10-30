using Unity.VisualScripting;
using SPACS.Tasks;
using SPACS.Tasks.UI;

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
