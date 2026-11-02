using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Collect element: the pickable added to this client's inventory completes the task, Right. The event is
    /// the pickable's own (raised on the client that picks it up), so the picker reports, once.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class CollectElementDriver : ElementDriver
    {
        private PickablePlaceholder pickable;
        private bool collected;

        private CollectElementPlaceholder Collect => (CollectElementPlaceholder)Element;

        protected override void OnBind()
        {
            pickable = Collect.Pickable != null ? Collect.Pickable : GetComponent<PickablePlaceholder>();
            if (pickable == null)
            {
                Debug.LogWarning("[CollectElement] " + name + " needs a Pickable placeholder: nothing can be collected.", this);
                return;
            }
            pickable.onItemAddedToInventory ??= new UnityEngine.Events.UnityEvent();
            pickable.onItemAddedToInventory.AddListener(OnCollected);
        }

        private void OnDestroy()
        {
            pickable?.onItemAddedToInventory?.RemoveListener(OnCollected);
        }

        // The application can raise the event twice for one pickup (its picked callback and its equip): one report.
        private void OnCollected()
        {
            if (collected)
            {
                return;
            }
            collected = true;
            Report(TaskResult.Right(gameObject), true);
        }
    }
}
