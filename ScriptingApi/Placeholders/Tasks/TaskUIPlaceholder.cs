using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class TaskUIPlaceholder : SpawnableHandlerPlaceholder
    {
        [SerializeField] public GameObject taskSystem; //Reference to task system for callbacks (Forse non serve, ma ci collego gli eventi di callback)
        [SerializeField] public string descriptionKey;
        [SerializeField] public bool showIntroductionAtAwake = false;

        [HideInInspector] public UnityEvent<bool> onDisplayIntro;
        public void DisplayIntroduction(bool value)
        {
            onDisplayIntro?.Invoke(value);
        }
    }
}
