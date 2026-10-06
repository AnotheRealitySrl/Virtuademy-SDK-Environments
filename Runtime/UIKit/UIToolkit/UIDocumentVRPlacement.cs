using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace Virtuademy.SDK.Environments.Utilities
{
    /// <summary>
    /// Makes a screen-space UIDocument visible in VR. Screen-space UI is not drawn in the headset, so
    /// whenever this object is enabled while an XR device is active (<see cref="XRSettings.isDeviceActive"/>)
    /// the document is switched to world-space <see cref="PanelSettings"/>, given a fixed size, and placed
    /// upright in front of the main camera. Outside VR it does nothing, and the document keeps the panel
    /// settings authored on it.
    ///
    /// Typical use: on a full-screen overlay (e.g. a video opened with <c>SetActive(true)</c>), so the
    /// same object works on desktop, mobile and VR.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIDocumentVRPlacement : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space panel settings used in VR.")]
        private PanelSettings worldPanelSettings;

        [SerializeField, Tooltip("Size of the document in VR, in panel pixels.")]
        private Vector2 panelSize = new(1200f, 750f);

        [SerializeField, Tooltip("Distance in metres from the camera at which the document is placed.")]
        private float distance = 1.8f;

        private void OnEnable()
        {
            if (!XRSettings.isDeviceActive || worldPanelSettings == null)
            {
                return;
            }
            UIDocument document = GetComponent<UIDocument>();
            document.panelSettings = worldPanelSettings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = panelSize;
            document.pivot = Pivot.Center;
            PlaceInFrontOfCamera();
        }

        private void PlaceInFrontOfCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            // Level the direction so the document stays upright even if the user is looking up or down.
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            }
            forward.Normalize();
            transform.SetPositionAndRotation(cam.transform.position + forward * distance,
                Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = Vector3.one;
        }
    }
}
