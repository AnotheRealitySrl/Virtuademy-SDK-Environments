using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Item On Target element: the detector's right item completes the task, a wrong one is a wrong attempt.
    /// The detector fires on every client of the room. Any Player: in a shared procedure the master reports (once), in
    /// a procedure of this client's own every item counts. Local Player Only: only this client's player's items and tools
    /// (the application says whose each event was, <see cref="InventoryItemTriggerDetectorPlaceholder.LastActionByLocalPlayer"/>),
    /// reported by this client.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ItemOnTargetElementDriver : ElementDriver
    {
        private InventoryItemTriggerDetectorPlaceholder detector;

        private ItemOnTargetElementPlaceholder Item => (ItemOnTargetElementPlaceholder)Element;

        protected override void OnBind()
        {
            detector = Item.Detector != null ? Item.Detector : GetComponent<InventoryItemTriggerDetectorPlaceholder>();
            if (detector == null)
            {
                Debug.LogWarning("[ItemOnTargetElement] " + name + " needs an Inventory Item Trigger Detector placeholder.", this);
                return;
            }
            detector.OnItemTriggerEnter ??= new UnityEngine.Events.UnityEvent();
            detector.OnWrongItemTriggerEnter ??= new UnityEngine.Events.UnityEvent();
            detector.OnItemTriggerEnter.AddListener(OnRightItem);
            detector.OnWrongItemTriggerEnter.AddListener(OnWrongItem);
            // The element's cue and the procedure show the feedback: the tool's own check or cross would be a second one
            detector.ElementShowsFeedback = true;
            detector.refusesItems = RefusesItems;
        }

        private void OnDestroy()
        {
            if (detector != null)
            {
                detector.ElementShowsFeedback = false;
                detector.refusesItems = null;
                detector.OnItemTriggerEnter?.RemoveListener(OnRightItem);
                detector.OnWrongItemTriggerEnter?.RemoveListener(OnWrongItem);
            }
        }

        // The target takes an item only while its task is to do or in progress: a completed (or failed) target would spend
        // a use for nothing, and a locked one would spend it before its turn. Without a task it always takes one.
        private bool RefusesItems()
        {
            var task = TaskOf();
            if (task == null)
            {
                return false;
            }
            SPACS.Tasks.TaskNode.TaskStatus status = task.Node.Status;
            return status != SPACS.Tasks.TaskNode.TaskStatus.Todo && status != SPACS.Tasks.TaskNode.TaskStatus.InProgress;
        }

        private void OnRightItem()
        {
            if (Reports())
            {
                Report(TaskResult.Right(gameObject), true);
            }
        }

        private void OnWrongItem()
        {
            if (Reports())
            {
                Report(TaskResult.Wrong(gameObject, Item.WrongReason), false);
            }
        }

        // The detector fires on every client of the room. Local Player Only: the client whose player acted reports. Any
        // Player: in a shared procedure the master reports, once; in this client's own procedure, every event counts.
        private bool Reports()
        {
            if (Element.CompletedBy == EElementCompleter.LocalPlayerOnly)
            {
                return detector.LastActionByLocalPlayer;
            }
            TaskSystemVirtuademy system = TaskOf() != null ? TaskOf().GetComponentInParent<TaskSystemVirtuademy>(true) : null;
            bool shared = system != null && system.isNetworked;
            return !shared || !IVirtuademyFramework.IsAvailable || IVirtuademyFramework.Current.Session.IsMasterClient;
        }
    }
}
