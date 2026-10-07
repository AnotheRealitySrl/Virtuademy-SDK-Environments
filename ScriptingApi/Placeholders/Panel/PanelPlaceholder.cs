using Virtuademy.Environments.ScriptingApi.Placeholders;

using System;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class PanelPlaceholder : SceneComponentPlaceholderBase
    {
        [Serializable]
        public struct UISize
        {
            public float width;
            public float height;
        }

        [SerializeField]
        private Transform uiParent;
        [SerializeField]
        private UISize uiSize;

        public Transform UiParent { get => uiParent; }
        public UISize UiSize { get => uiSize; }

        private void OnDrawGizmos()
        {
            if (uiParent != null)
            {
                Vector3 size = new Vector3(UiSize.width, uiSize.height, 0.01f);
                // Salviamo la posizione e la rotazione del transform corrente
                Vector3 originalPosition = uiParent.position;
                Quaternion originalRotation = uiParent.rotation;

                // Impostiamo la matrice di trasformazione in modo che il cubo sia disegnato relativo a questo oggetto
                Gizmos.matrix = Matrix4x4.TRS(originalPosition, originalRotation, Vector3.one);

                // Disegniamo il cubo
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.zero, size);

                // Ripristiniamo la matrice di trasformazione originale
                Gizmos.matrix = Matrix4x4.identity;

            }
        }
    }
}
