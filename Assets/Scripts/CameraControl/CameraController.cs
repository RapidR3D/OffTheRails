using UnityEngine;
using UnityEngine.InputSystem;

namespace OffTheRails.CameraControl
{
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [Tooltip("Minimum orthographic size (zoom in)")]
        [SerializeField] private float minZoom = 2f;

        [Tooltip("Maximum orthographic size (zoom out)")]
        [SerializeField] private float maxZoom = 20f;

        [Tooltip("Zoom speed multiplier")]
        [SerializeField] private float zoomSpeed = 1f;

        [Tooltip("Smoothness of the zoom (higher is smoother)")]
        [SerializeField] private float smoothTime = 0.1f;

        [Header("Pan Settings")]
        [Tooltip("Enable camera panning")]
        [SerializeField] private bool enablePanning = true;

        private Camera cam;
        private float targetZoom;
        private float currentVelocity;
        private bool isPanning;
        private Vector3 lastMousePosition;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("CameraController: No Camera component found!");
                enabled = false;
                return;
            }

            targetZoom = cam.orthographicSize;
        }

        private void Update()
        {
            HandleZoom();
            HandlePan();
        }

        private void HandlePan()
        {
            if (!enablePanning) return;

            // Mouse Panning
            if (Mouse.current != null)
            {
                if (Mouse.current.middleButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
                {
                    isPanning = true;
                }

                if (Mouse.current.middleButton.wasReleasedThisFrame || Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    isPanning = false;
                }

                if (isPanning)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    PanCamera(mouseDelta);
                }
            }

            // Touch Panning
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var touch = Touchscreen.current.touches[0];
                
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 touchDelta = touch.delta.ReadValue();
                    PanCamera(touchDelta);
                }
            }
        }

        private void PanCamera(Vector2 delta)
        {
            // Calculate how many world units one pixel represents
            // Orthographic size is half the vertical size of the view
            float unitsPerPixel = (2f * cam.orthographicSize) / Screen.height;

            // Move opposite to drag direction
            Vector3 move = new Vector3(-delta.x * unitsPerPixel, -delta.y * unitsPerPixel, 0);
            
            transform.Translate(move, Space.World);
        }

        private void HandleZoom()
        {
            // Get scroll input
            float scrollInput = Mouse.current.scroll.y.ReadValue();

            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                // Adjust target zoom based on scroll direction
                // Negative scroll (pulling back) increases size (zooms out)
                // Positive scroll (pushing forward) decreases size (zooms in)
                targetZoom -= scrollInput * zoomSpeed * Time.deltaTime;
                
                // Clamp target zoom
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            // Smoothly interpolate to target zoom
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref currentVelocity, smoothTime);
        }
    }
}
