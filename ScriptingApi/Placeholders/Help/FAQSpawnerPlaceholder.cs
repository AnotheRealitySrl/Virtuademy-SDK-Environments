using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class FAQSpawnerPlaceholder : MonoBehaviour
    {
        [SerializeField]
        private string faqCategory;
        [SerializeField]
        private Transform faqParent;
        [SerializeField]
        private Size blockSize;

        public string FaqCategory { get => faqCategory; set => faqCategory = value; }
        public Transform FaqParent { get => faqParent; set => faqParent = value; }
        public Size BlockSize { get => blockSize; set => blockSize = value; }
    }
}
