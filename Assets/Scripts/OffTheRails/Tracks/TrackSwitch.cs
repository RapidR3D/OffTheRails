using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OffTheRails.Tracks  
{
    /// <summary>
    /// Handles the visual and logical state of a track switch. 
    /// Can be toggled by clicking on it to change between straight and diverging paths.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TrackSwitch : MonoBehaviour
    {
        [Header("Switch State")]
        [SerializeField] private bool isDiverging = false;
        
        [Header("Visual Feedback")]
        [Tooltip("Color when in straight mode")]
        [SerializeField] private Color straightColor = Color.green;
        
        [Tooltip("Color when in diverging mode")]
        [SerializeField] private Color divergingColor = Color.yellow;
        
        [Tooltip("Color when hovering over switch")]
        [SerializeField] private Color hoverColor = Color.cyan;
        
        [Tooltip("Should the switch show visual feedback?")]
        [SerializeField] private bool showVisualFeedback = true;
        
        [Header("Events")]
        public UnityEvent OnSwitchToggled;

        public bool IsDiverging => isDiverging;
        public TrackPiece ParentTrack { get; private set; }

        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private bool isHovering = false;
        private Camera mainCamera;

        private void Awake()
        {
            ParentTrack = GetComponent<TrackPiece>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mainCamera = Camera.main;
            
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            
            // Ensure we have a collider
            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                Debug.LogWarning($"TrackSwitch on {gameObject.name} requires a Collider2D component!");
            }
        }

        private void Start()
        {
            // Set initial visual state
            UpdateVisuals();
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Handle mouse input for clicking the switch
        /// </summary>
        private void HandleInput()
        {
            if (mainCamera == null || Mouse.current == null)
                return;

            // Get mouse position in world space
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            
            // Check if mouse is over this switch (checking both parent and children)
            Collider2D hitCollider = Physics2D.OverlapPoint(mouseWorldPos);
            bool wasHovering = isHovering;
            
            // Check if the collider belongs to this switch's GameObject hierarchy
            isHovering = false;
            if (hitCollider != null)
            {
                // Check if the hit collider is on this GameObject or any of its children
                Transform checkTransform = hitCollider.transform;
                while (checkTransform != null)
                {
                    if (checkTransform.gameObject == gameObject)
                    {
                        isHovering = true;
                        break;
                    }
                    checkTransform = checkTransform.parent;
                }
            }
            
            // Update hover visuals
            if (isHovering != wasHovering && showVisualFeedback)
            {
                UpdateVisuals();
            }

            // Handle click to toggle
            if (isHovering && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"Switch {gameObject.name} clicked!");
                ToggleSwitch();
            }
        }

        /// <summary>
        /// Toggle the switch between straight and diverging
        /// </summary>
        public void ToggleSwitch()
        {
            isDiverging = !isDiverging;
            UpdateVisuals();
            OnSwitchToggled?.Invoke();
            
            Debug.Log($"═══ SWITCH TOGGLE START ═══");
            Debug.Log($"Switch '{gameObject.name}' toggled to: {(isDiverging ? "DIVERGING (Yellow)" : "STRAIGHT (Green)")}");
            
            // Notify TrackManager to regenerate paths
            if (TrackManager.Instance != null)
            {
                // Force the track piece to regenerate its waypoints
                // Use GetComponentInParent since the switch is a child of the track piece
                var trackPiece = GetComponentInParent<TrackPiece>();
                if (trackPiece != null)
                {
                    Debug.Log($"Found TrackPiece on '{trackPiece.gameObject.name}' - regenerating waypoints...");
                    
                    // Invalidate cache first, then regenerate
                    trackPiece.InvalidateWaypointCache();
                    trackPiece.GenerateWaypoints();
                    
                    Debug.Log($"Waypoints regenerated. Calling TrackManager.RegenerateAllPaths()...");
                    TrackManager.Instance.RegenerateAllPaths();
                    Debug.Log($"═══ SWITCH TOGGLE COMPLETE ═══");
                }
                else
                {
                    //Debug.LogError($"ERROR: No TrackPiece component found on '{gameObject.name}' or its parents!");
                }
            }
            else
            {
               // Debug.LogError("ERROR: TrackManager.Instance is null!");
            }
        }

        /// <summary>
        /// Set the switch state programmatically
        /// </summary>
        /// <param name="diverging">True for diverging, false for straight</param>
        public void SetState(bool diverging)
        {
            if (isDiverging != diverging)
            {
                ToggleSwitch();
            }
        }

        /// <summary>
        /// Update visual representation of the switch
        /// </summary>
        private void UpdateVisuals()
        {
            if (!showVisualFeedback || spriteRenderer == null)
                return;

            // Determine color based on state and hover
            Color targetColor;
            
            if (isHovering)
            {
                targetColor = hoverColor;
            }
            else if (isDiverging)
            {
                targetColor = divergingColor;
            }
            else
            {
                targetColor = straightColor;
            }

            spriteRenderer.color = targetColor;
        }

        /// <summary>
        /// Force visual update (useful when external code changes state)
        /// </summary>
        public void RefreshVisuals()
        {
            UpdateVisuals();
        }

        private void OnDrawGizmos()
        {
            // Draw a small indicator showing switch state
            if (!Application.isPlaying)
                return;

            Gizmos.color = isDiverging ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            // Show which connection points are active
            var trackPiece = GetComponent<TrackPiece>();
            if (trackPiece == null || trackPiece.ConnectionPoints.Length < 3)
                return;

            // Draw lines to show active connections
            Gizmos.color = Color.green;
            if (trackPiece.ConnectionPoints.Length >= 2)
            {
                // Common point to straight path (always active)
                Gizmos.DrawLine(trackPiece.ConnectionPoints[0].WorldPosition, 
                               trackPiece.ConnectionPoints[1].WorldPosition);
            }

            if (trackPiece.ConnectionPoints.Length >= 3)
            {
                // Diverging path (active when isDiverging is true)
                Gizmos.color = isDiverging ? Color.yellow : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Gizmos.DrawLine(trackPiece.ConnectionPoints[0].WorldPosition, 
                               trackPiece.ConnectionPoints[2].WorldPosition);
            }
        }
    }
}