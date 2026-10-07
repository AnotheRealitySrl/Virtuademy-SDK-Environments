using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [RequireComponent(typeof(MascottePlaceholder))]
    public class MascottePanelPlaceholder : PanelPlaceholder
    {
        [SerializeField]
        private string[] faqCategory;

        public string[] FaqCategory { get => faqCategory; }

    }
}