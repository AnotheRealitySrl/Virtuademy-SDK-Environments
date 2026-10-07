using Virtuademy.Environments.ScriptingApi.Placeholders;
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(InteractablePlaceholder))]
    public abstract class InteractionBehaviourPlaceholder : SceneComponentPlaceholderBase
    {
        private InteractablePlaceholder interactionPlaceholder;
        protected InteractablePlaceholder InteractionPlaceholder
        {
            get
            {
                if (interactionPlaceholder == null)
                {
                    interactionPlaceholder = GetComponentInChildren<InteractablePlaceholder>(true);
                }
                return interactionPlaceholder;
            }
        }
    }
}
