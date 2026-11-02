using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Task element: the user watches or reads something to the end and the task is completed, Right: a video reaching
    /// its end (Video Player), a POI closed, or a Virtuademy Panel hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public class WatchElementPlaceholder : ElementPlaceholderBase
    {
        [Header("Watch")]
        [SerializeField, Tooltip("The object watched: with a Video Player, a POI placeholder or a Virtuademy Panel. Empty: " +
            "this object.")]
        private GameObject watched;

        public GameObject Watched { get => watched; set => watched = value; }
    }
}
