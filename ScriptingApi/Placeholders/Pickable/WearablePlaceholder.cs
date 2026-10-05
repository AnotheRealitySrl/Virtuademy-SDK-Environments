using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(PickablePlaceholder))]
    public class WearablePlaceholder : SceneComponentPlaceholderNetwork
    {
        [Tooltip("If set to false then the user cannot unequip this wearable")]
        public bool canRemove = true;
    }
}
