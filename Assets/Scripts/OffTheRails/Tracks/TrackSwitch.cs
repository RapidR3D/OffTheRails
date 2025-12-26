using System.Collections.Generic;
using OffTheRails.Trains;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Handles the visual and logical state of a track switch.
    /// When toggled, it rebuilds all paths to reflect the new switch state.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TrackSwitch : MonoBehaviour
    {
        [Header("Switch State")]
        [SerializeField] private bool isDiverging = false;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color straightColor = Color.green;
        [SerializeField] private Color divergingColor = Color.yellow;
        [SerializeField] private Color hoverColor = Color.cyan;
        [SerializeField] private bool showVisualFeedback = true;
        
        [Header("Events")]
        public UnityEvent OnSwitchToggled;

        public bool IsDiverging => isDiverging;
        public TrackPiece ParentTrack { get; private set; }

        private SpriteRenderer spriteRenderer;
        private bool isHovering = false;
        private Camera mainCamera;

        private void Awake()
        {
            ParentTrack = GetComponent<TrackPiece>();
            if (ParentTrack == null)
                ParentTrack = GetComponentInParent<TrackPiece>();
            
            if (ParentTrack == null)
                Debug.LogError($"TrackSwitch on {gameObject.name} couldn't find TrackPiece!");
            
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mainCamera = Camera.main;
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (mainCamera == null) return;

            bool inputDetected = false;
            Vector2 inputScreenPos = Vector2.zero;
            bool inputPressed = false;

            // Mouse Input
            if (Mouse.current != null)
            {
                inputScreenPos = Mouse.current.position.ReadValue();
                inputPressed = Mouse.current.leftButton.wasPressedThisFrame;
                inputDetected = true;
            }

            // Touch Input (overrides mouse if present)
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var touch = Touchscreen.current.touches[0];
                inputScreenPos = touch.position.ReadValue();
                inputPressed = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
                inputDetected = true;
            }

            if (!inputDetected) return;

            Vector3 worldPos = mainCamera.ScreenToWorldPoint(inputScreenPos);
            
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPos);
            bool wasHovering = isHovering;
            
            isHovering = false;
            if (hitCollider != null)
            {
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
            
            if (isHovering != wasHovering && showVisualFeedback)
                UpdateVisuals();

            if (isHovering && inputPressed)
                ToggleSwitch();
        }

        /// <summary>
        /// Toggle the switch between straight and diverging.
        /// This rebuilds all paths and updates train positions.
        /// </summary>
        public void ToggleSwitch()
        {
            // Debug.Log($"═══════════════════════════════════════════════");
            // Debug.Log($"SWITCH TOGGLE: {gameObject.name}");
            // Debug.Log($"═══════════════════════════════════════════════");
            
            // Step 1: Store train states before rebuilding
            Train[] allTrains = FindObjectsByType<Train>(FindObjectsSortMode.None);
            Dictionary<Train, TrainState> trainStates = new Dictionary<Train, TrainState>();
            
            foreach (var train in allTrains)
            {
                if (!train.IsActive || train.GetCurrentPath() == null) continue;
                
                trainStates[train] = new TrainState
                {
                    WorldPosition = train.transform.position,
                    Direction = train.GetDirection(),
                    DistanceAlongPath = train.DistanceAlongPath,
                    OldPathLength = train.GetCurrentPath().TotalLength
                };
                
                // Debug.Log($"[Train {train.name}] Stored state: pos={trainStates[train].WorldPosition}, dir={trainStates[train].Direction}, dist={trainStates[train].DistanceAlongPath:F1}");
            }
            
            // Step 2: Toggle the switch state
            isDiverging = !isDiverging;
            // Debug.Log($"Switch state changed to: {(isDiverging ? "DIVERGING" : "STRAIGHT")}");
            
            // Step 3: Regenerate waypoints for the junction track
            if (ParentTrack != null)
            {
                ParentTrack.InvalidateWaypointCache();
                ParentTrack.GenerateWaypoints();
            }
            
            // Step 4: Regenerate all paths with new switch state
            // We use RegenerateAllPaths (not RebuildAllPaths) because the switch change
            // may make some endpoint pairs unreachable and others reachable
            if (TrackManager.Instance != null)
            {
                TrackManager.Instance.RegenerateAllPaths();
            }
            
            // Step 5: Reassign trains to rebuilt paths, maintaining their position AND direction
            // BUT only if:
            // - The train hasn't passed the junction yet AND
            // - The train is approaching from the FACING point direction (CP[0])
            // Trains approaching from trailing point (CP[1] or CP[2]) are not affected by switch
            foreach (var train in allTrains)
            {
                if (!trainStates.ContainsKey(train)) continue;
                
                var state = trainStates[train];
                
                TrackPath currentPath = train.GetCurrentPath();
                bool shouldReassign = false;
                
                if (currentPath != null && ParentTrack != null)
                {
                    // Find where the junction is in the current path
                    int junctionIndex = currentPath.TrackPieces.IndexOf(ParentTrack);
                    
                    if (junctionIndex >= 0)
                    {
                        // Find where the train currently is in the path
                        int trainTrackIndex = -1;
                        float distanceSoFar = 0f;
                        
                        for (int i = 0; i < currentPath.TrackPieces.Count; i++)
                        {
                            var track = currentPath.TrackPieces[i];
                            float trackLength = track.Length;
                            
                            if (distanceSoFar + trackLength >= state.DistanceAlongPath)
                            {
                                trainTrackIndex = i;
                                break;
                            }
                            distanceSoFar += trackLength;
                        }
                        
                        bool hasPassedJunction = trainTrackIndex >= junctionIndex;
                        
                        // Check if train is approaching from facing point (CP[0]) or trailing point (CP[1]/CP[2])
                        // Train approaches from facing point if the PREVIOUS track in path connects to CP[0]
                        bool approachingFromFacingPoint = false;
                        
                        if (junctionIndex > 0)
                        {
                            TrackPiece prevTrack = currentPath.TrackPieces[junctionIndex - 1];
                            var junctionCPs = ParentTrack.ConnectionPoints;
                            
                            // Check if prevTrack is connected to CP[0] (the facing/common point)
                            if (junctionCPs.Length >= 1 && junctionCPs[0].IsConnected && 
                                junctionCPs[0].ConnectedTo != null &&
                                junctionCPs[0].ConnectedTo.ParentTrack == prevTrack)
                            {
                                approachingFromFacingPoint = true;
                            }
                        }
                        
                        // Debug.Log($"[Train {train.name}] Junction at index {junctionIndex}, train at index {trainTrackIndex}, " +
                                 // $"hasPassedJunction={hasPassedJunction}, approachingFromFacingPoint={approachingFromFacingPoint}");
                        
                        // Only reassign if train hasn't passed AND is approaching from facing point
                        shouldReassign = !hasPassedJunction && approachingFromFacingPoint;
                    }
                    else
                    {
                        // Junction not in current path - train is on a different route, don't touch it
                        // Debug.Log($"[Train {train.name}] Junction not in path, keeping current path");
                    }
                }
                
                if (!shouldReassign)
                {
                    // Debug.Log($"[Train {train.name}] Keeping current path (passed junction or trailing point approach)");
                    continue;
                }
                
                // Train hasn't passed junction yet AND is approaching from facing point - reassign to new path
                TrackPath newPath = TrackManager.Instance.GetPathNearestTo(state.WorldPosition);
                
                if (newPath == null || newPath.Waypoints.Count == 0)
                {
                    // Debug.LogWarning($"[Train {train.name}] No valid path found!");
                    continue;
                }
                
                // Find closest point on the new path
                float newDistance = newPath.GetClosestDistanceToPoint(state.WorldPosition);
                
                // Check if the path direction matches the train's original direction
                Vector2 pathDirectionAtPoint = newPath.GetDirectionAtDistance(newDistance);
                float dot = Vector2.Dot(state.Direction, pathDirectionAtPoint);
                
                if (dot < 0)
                {
                    // Path is going the wrong way - reverse it
                    // Debug.Log($"[Train {train.name}] Path direction mismatch (dot={dot:F2}), reversing path");
                    newPath.Reverse();
                    newDistance = newPath.GetClosestDistanceToPoint(state.WorldPosition);
                }
                
                // Debug.Log($"[Train {train.name}] Reassigned to path, distance: {state.DistanceAlongPath:F1} → {newDistance:F1}");
                
                train.SetPath(newPath, newDistance);
            }
            
            // Step 6: Update visuals and fire event
            UpdateVisuals();
            OnSwitchToggled?.Invoke();
            
            // Debug.Log($"═══════════════════════════════════════════════");
            // Debug.Log($"SWITCH TOGGLE COMPLETE");
            // Debug.Log($"═══════════════════════════════════════════════");
        }
        
        private struct TrainState
        {
            public Vector2 WorldPosition;
            public Vector2 Direction;
            public float DistanceAlongPath;
            public float OldPathLength;
        }

        public void SetState(bool diverging)
        {
            if (isDiverging != diverging)
                ToggleSwitch();
        }

        private void UpdateVisuals()
        {
            if (!showVisualFeedback || spriteRenderer == null) return;

            if (isHovering)
                spriteRenderer.color = hoverColor;
            else if (isDiverging)
                spriteRenderer.color = divergingColor;
            else
                spriteRenderer.color = straightColor;
        }

        public void RefreshVisuals() => UpdateVisuals();

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = isDiverging ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            var trackPiece = ParentTrack ?? GetComponent<TrackPiece>() ?? GetComponentInParent<TrackPiece>();
            if (trackPiece == null || trackPiece.ConnectionPoints.Length < 3) return;

            // Draw straight path
            Gizmos.color = isDiverging ? new Color(0.5f, 0.5f, 0.5f, 0.3f) : Color.green;
            Gizmos.DrawLine(trackPiece.ConnectionPoints[0].WorldPosition, 
                           trackPiece.ConnectionPoints[1].WorldPosition);

            // Draw diverging path
            Gizmos.color = isDiverging ? Color.yellow : new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Gizmos.DrawLine(trackPiece.ConnectionPoints[0].WorldPosition, 
                           trackPiece.ConnectionPoints[2].WorldPosition);
        }
    }
}
