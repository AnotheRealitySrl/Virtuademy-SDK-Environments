using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.SDK.Environments.VisualScripting;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;


namespace Virtuademy.SDK.Environments.Tasks
{
    public class VisualScriptingInteractableHoverDetector : MonoBehaviour
    {
        [HideInInspector]
        public InteractablePlaceholderObsolete interactablePlaceholder;
        public VisualScriptingInteractablePlaceholder visualscriptingPlaceholder;
        [SerializeField, Tooltip("VR: a controller's ray hovers the object.")]
        private bool allowRay;
        [SerializeField, Tooltip("VR: a hand touches the object.")]
        private bool allowHandDirect;
        [SerializeField, Tooltip("Desktop: the mouse pointer hovers the object. Mobile: the finger that pressed the object is over it.")]
        private bool allowMouse;

        public UnityEvent OnHoverEnter = default;
        public UnityEvent OnHoverExit = default;


        public async void Setup()
        {
            if (visualscriptingPlaceholder == null)
            {
                return;
            }

            IVisualScriptingInteractable visualScriptingInteractable = null;
            while (!visualscriptingPlaceholder.TryGetComponent(out visualScriptingInteractable))
            {
                await Task.Yield();
            }
            if (visualScriptingInteractable != null)
            {
                if (allowHandDirect)
                {
                    visualScriptingInteractable.OnHoverGrabEnter.AddListener(OnHoverEnterEvent);
                    visualScriptingInteractable.OnHoverGrabExit.AddListener(OnHoverExitEvent);
                }

                if (allowRay)
                {
                    visualScriptingInteractable.OnHoverRayEnter.AddListener(OnHoverEnterEvent);
                    visualScriptingInteractable.OnHoverRayExit.AddListener(OnHoverExitEvent);
                }

                if (allowMouse)
                {
                    visualScriptingInteractable.OnHoverMouseEnter.AddListener(OnHoverEnterEvent);
                    visualScriptingInteractable.OnHoverMouseExit.AddListener(OnHoverExitEvent);
                }
            }
        }

        public void OnEnable()
        {
            Setup();
        }

        public void OnDisable()
        {
            IVisualScriptingInteractable interactable = visualscriptingPlaceholder.gameObject.GetComponent<IVisualScriptingInteractable>();
            if (interactable != null)
            {
                if (allowHandDirect)
                {
                    interactable.OnHoverGrabEnter.RemoveListener(OnHoverEnterEvent);
                    interactable.OnHoverGrabExit.RemoveListener(OnHoverExitEvent);
                }

                if (allowRay)
                {
                    interactable.OnHoverRayEnter.RemoveListener(OnHoverEnterEvent);
                    interactable.OnHoverRayExit.RemoveListener(OnHoverExitEvent);
                }

                if (allowMouse)
                {
                    interactable.OnHoverMouseEnter.RemoveListener(OnHoverEnterEvent);
                    interactable.OnHoverMouseExit.RemoveListener(OnHoverExitEvent);
                }
            }
        }

        private void OnHoverEnterEvent()
        {
            OnHoverEnter.Invoke();
        }

        private void OnHoverExitEvent()
        {
            OnHoverExit.Invoke();
        }
    }
}
