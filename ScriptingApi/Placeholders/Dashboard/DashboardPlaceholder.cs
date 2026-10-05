using Virtuademy.Environments.ScriptingApi.Placeholders;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class DashboardPlaceholder : SceneComponentPlaceholderBase, IAddressablePlaceholder
    {
        public enum DashboardFilter
        {
            Environment,
            Tags,
            //Retrocompatibility: this was used to filter by old tags
            NoFilter,
            None
        }

        [SerializeField] private string addressableKey;

        [Header("DashboardData")]
        [SerializeField, Tooltip("Choose the type on information you want to show on the dashboard")]
        private DashboardFilter filter;
        [SerializeField, Tooltip("Write the name of the environment or tag present in the backoffice that you want to show on the dashboard ")]
        private string dashboardNameFilter;
        [SerializeField]
        private Transform panTransform;

        [SerializeField, /*Range(0.5f, 10),*/ Tooltip("The width of the screen.")]
        [OnChangedCall(nameof(OnScaleChanged))]
        private float dashboardScale = 6f;

        public string DashboardNameFilter => dashboardNameFilter;
        public DashboardFilter Filter => filter;
        public Transform PanTransform => panTransform;

        public float DashboardScale => dashboardScale;

        public string AddressableKey => addressableKey;

        public void OnScaleChanged()
        {
            transform.localScale = new Vector3(dashboardScale, dashboardScale, transform.localScale.z);
        }

    }
}
