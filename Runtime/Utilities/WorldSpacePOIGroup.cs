using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Makes a set of <see cref="WorldSpacePOIToggle"/> mutually exclusive: when one POI opens, every
    /// other POI in the group that is currently open gets closed.
    ///
    /// The POIs come from the serialized <c>pois</c> list. If the list is left empty, the group
    /// collects every <see cref="WorldSpacePOIToggle"/> in its children instead, so it can simply sit
    /// on the parent of the POIs.
    ///
    /// Only POIs that are actually open are closed, so <c>onClose</c> never fires on a POI that was
    /// already closed.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldSpacePOIGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("POIs in this group. If empty, every WorldSpacePOIToggle in the " +
            "children of this object is used.")]
        private List<WorldSpacePOIToggle> pois = new();

        private void Awake()
        {
            if (pois.Count == 0)
            {
                GetComponentsInChildren(true, pois);
            }
        }

        private void OnEnable()
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null)
                {
                    poi.Opened += OnPOIOpened;
                }
            }
        }

        private void OnDisable()
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null)
                {
                    poi.Opened -= OnPOIOpened;
                }
            }
        }

        private void OnPOIOpened(WorldSpacePOIToggle opened)
        {
            foreach (WorldSpacePOIToggle poi in pois)
            {
                if (poi != null && poi != opened && poi.IsOpen)
                {
                    poi.Close();
                }
            }
        }
    }
}
