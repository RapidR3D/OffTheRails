using UnityEngine;
using UnityEngine.InputSystem;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Handles player interaction for placing track pieces.
    /// Supports drag and drop, snapping, and visual feedback.
    /// FIXED VERSION: Improved track selection and deletion
    /// </summary>
    public class TrackPlacer : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [Tooltip("Track piece prefabs that can be placed")]
        [SerializeField] private GameObject[] trackPrefabs;

        [Tooltip("Currently selected track prefab index")]
        [SerializeField] private int selectedPrefabIndex = 0;

        [Header("Placement Settings")]
        [Tooltip("Layer to place tracks on")]
        [SerializeField] private string trackLayer = "Default";

        [Tooltip("Sorting layer for tracks")]
        [SerializeField] private string trackSortingLayer = "Tracks";

        [Tooltip("Z position for placed tracks")]
        [SerializeField] private float trackZPosition = 0f;

        [Tooltip("Should tracks snap to grid when not snapping to connections?")]
        [SerializeField] private bool snapToGrid = false;

        [Tooltip("Grid size for snapping")]
        [SerializeField] private float gridSize = 0.5f;

        [Header("Visual Feedback")]
        [Tooltip("Color for valid placement preview")]
        [SerializeField] private Color validPlacementColor = new Color(0, 1, 0, 0.5f);

        [Tooltip("Color for invalid placement preview")]
        [SerializeField] private Color invalidPlacementColor = new Color(1, 0, 0, 0.5f);

        [Tooltip("Color for snap indicator")]
        [SerializeField] private Color snapIndicatorColor = Color.yellow;

        [Tooltip("Size of snap indicator")]
        [SerializeField] private float snapIndicatorSize = 0.3f;

        [Header("Selection Settings")]
        [Tooltip("Color for selected track")]
        [SerializeField] private Color selectionColor = Color.cyan;

        [Tooltip("Radius for selecting tracks with mouse (in world units)")]
        [SerializeField] private float selectionRadius = 1.0f;

        [Tooltip("Layer mask for selecting tracks - leave at 'Everything' if unsure")]
        [SerializeField] private LayerMask trackLayerMask = -1; // Everything by default

        [Header("Input Settings")]
        [Tooltip("Rotation step in degrees")]
        [SerializeField] private float rotationStep = 90f;

        // Runtime state
        private GameObject previewObject;
        private TrackPiece previewTrack;
        private bool isDragging = false;
        private Vector3 dragStartPosition;
        private float currentRotation = 0f;
        private ConnectionPoint snapTarget = null;
        private Camera mainCamera;
        private TrackPiece selectedTrack;
        private SpriteRenderer selectedTrackRenderer;
        private Color originalTrackColor;

        private void Start()
        {
            mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                Debug.LogError("TrackPlacer: No main camera found!");
            }

            if (trackPrefabs == null || trackPrefabs.Length == 0)
            {
                Debug.LogWarning("TrackPlacer: No track prefabs assigned!");
            }

            // Debug.Log($"TrackPlacer initialized. Layer mask value: {trackLayerMask.value}");
        }

        private void Update()
        {
            HandleInput();
            UpdatePreview();
        }

        /// <summary>
        /// Handle player input
        /// </summary>
        private void HandleInput()
        {
            if (mainCamera == null || trackPrefabs == null || trackPrefabs.Length == 0)
                return;

            // Get mouse position in world space
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = trackZPosition;

            // Handle rotation input (Q and E keys)
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                currentRotation += rotationStep;
                if (previewObject != null)
                {
                    previewObject.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                }
            }

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                currentRotation -= rotationStep;
                if (previewObject != null)
                {
                    previewObject.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                }
            }

            // Handle number keys for prefab selection (1-9)
            for (int i = 0; i < Mathf.Min(9, trackPrefabs.Length); i++)
            {
                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    SelectPrefab(i);
                }
            }

            // Handle deletion (Delete or Backspace)
            if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                DeleteSelectedTrack();
            }

            // Check if mouse is over a switch (switches handle their own clicks)
            bool isOverSwitch = IsMouseOverSwitch(mousePos);

            // Handle selection (Right Click) - but not on switches
            if (Mouse.current.rightButton.wasPressedThisFrame && !isOverSwitch)
            {
                TrySelectTrackAtPosition(mousePos);
            }

            // Handle placement (Left Click) - but not on switches
            if (Mouse.current.leftButton.wasPressedThisFrame && !isDragging && !isOverSwitch)
            {
                // Debug.Log($"LEFT CLICK: isDragging={isDragging}, isOverSwitch={isOverSwitch}");
                
                // Check if we clicked on an existing track first
                TrackPiece clickedTrack = FindTrackAtPosition(mousePos);
                
                if (clickedTrack != null)
                {
                    // Debug.Log($"Clicked on existing track: {clickedTrack.name}, selecting it");
                    SelectTrack(clickedTrack);
                    return; // Don't start dragging if we selected a track
                }

                // If we didn't click a track, start dragging new one
                // Debug.Log($"No track clicked, starting drag for new track placement");
                DeselectTrack();
                StartDragging(mousePos);
            }

            // Continue dragging
            if (isDragging)
            {
                UpdateDragging(mousePos);
            }

            // End dragging (place the track)
            if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
            {
                EndDragging(mousePos);
            }
        }

        /// <summary>
        /// Check if the mouse is over a track switch
        /// </summary>
        private bool IsMouseOverSwitch(Vector3 worldPos)
        {
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPos);
            
            // Debug.Log($"IsMouseOverSwitch check at {worldPos}: hitCollider = {(hitCollider != null ? hitCollider.gameObject.name : "null")}");
            
            if (hitCollider != null)
            {
                // Check if the hit collider or any of its parents have a TrackSwitch component
                Transform checkTransform = hitCollider.transform;
                while (checkTransform != null)
                {
                    TrackSwitch trackSwitch = checkTransform.GetComponent<TrackSwitch>();
                    if (trackSwitch != null)
                    {
                        //Debug.Log($"✓ Mouse IS over switch: {checkTransform.gameObject.name}");
                        return true;
                    }
                    // Debug.Log($"  Checking parent: {checkTransform.name} (no TrackSwitch found)");
                    checkTransform = checkTransform.parent;
                }
                // Debug.Log($"✗ Hit collider '{hitCollider.gameObject.name}' has no TrackSwitch in hierarchy");
            }
            else
            {
                // Debug.Log($"✗ No collider hit at position");
            }
            return false;
        }

        /// <summary>
        /// Try to select a track at the given position
        /// </summary>
        private void TrySelectTrackAtPosition(Vector3 worldPos)
        {
            TrackPiece track = FindTrackAtPosition(worldPos);
            
            if (track != null)
            {
                SelectTrack(track);
            }
            else
            {
                DeselectTrack();
            }
        }

        /// <summary>
        /// Find a track piece at the given world position using distance-based selection
        /// This works without colliders by checking against waypoints and track positions
        /// </summary>
        private TrackPiece FindTrackAtPosition(Vector3 worldPos)
        {
            if (TrackManager.Instance == null)
            {
                Debug.LogWarning("TrackManager instance not found");
                return null;
            }

            TrackPiece closestTrack = null;
            float closestDistance = selectionRadius;

            foreach (var track in TrackManager.Instance.GetAllTracks())
            {
                if (track == null) continue;

                // Method 1: Check distance to track's center/pivot
                float centerDistance = Vector2.Distance(worldPos, track.transform.position);
                if (centerDistance < closestDistance)
                {
                    closestDistance = centerDistance;
                    closestTrack = track;
                    continue;
                }

                // Method 2: Check distance to any waypoint along the track
                Vector2[] waypoints = track.WorldWaypoints;
                if (waypoints != null && waypoints.Length > 0)
                {
                    foreach (var waypoint in waypoints)
                    {
                        float waypointDistance = Vector2.Distance(worldPos, waypoint);
                        if (waypointDistance < closestDistance)
                        {
                            closestDistance = waypointDistance;
                            closestTrack = track;
                        }
                    }
                    
                    // Method 3: Check distance to line segments between waypoints
                    for (int i = 0; i < waypoints.Length - 1; i++)
                    {
                        float segmentDistance = DistanceToLineSegment(worldPos, waypoints[i], waypoints[i + 1]);
                        if (segmentDistance < closestDistance)
                        {
                            closestDistance = segmentDistance;
                            closestTrack = track;
                        }
                    }
                }
            }

            if (closestTrack != null)
            {
                // Debug.Log($"Found track via distance check: {closestTrack.name} (distance: {closestDistance:F2})");
            }
            else
            {
                // Debug.Log($"No track found within {selectionRadius} units of click position");
            }

            return closestTrack;
        }

        /// <summary>
        /// Calculate the distance from a point to a line segment
        /// </summary>
        private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float lineLength = line.magnitude;
            
            if (lineLength < 0.001f)
                return Vector2.Distance(point, lineStart);
            
            line.Normalize();
            
            Vector2 toPoint = point - lineStart;
            float projection = Vector2.Dot(toPoint, line);
            
            // Clamp to line segment
            projection = Mathf.Clamp(projection, 0, lineLength);
            
            Vector2 closestPoint = lineStart + line * projection;
            return Vector2.Distance(point, closestPoint);
        }

        /// <summary>
        /// Select a track piece and highlight it
        /// </summary>
        private void SelectTrack(TrackPiece track)
        {
            if (selectedTrack == track) return;

            DeselectTrack();

            selectedTrack = track;
            if (selectedTrack != null)
            {
                // Highlight the selected track
                selectedTrackRenderer = selectedTrack.GetComponentInChildren<SpriteRenderer>();
                if (selectedTrackRenderer != null)
                {
                    originalTrackColor = selectedTrackRenderer.color;
                    selectedTrackRenderer.color = selectionColor;
                }
                // Debug.Log($"✓ Selected track: {selectedTrack.name}");
            }
        }

        /// <summary>
        /// Deselect the currently selected track
        /// </summary>
        private void DeselectTrack()
        {
            if (selectedTrack != null && selectedTrackRenderer != null)
            {
                // Restore original color
                selectedTrackRenderer.color = originalTrackColor;
                // Debug.Log($"Deselected track: {selectedTrack.name}");
            }
            
            selectedTrack = null;
            selectedTrackRenderer = null;
        }

        /// <summary>
        /// Delete the currently selected track
        /// </summary>
        private void DeleteSelectedTrack()
        {
            if (selectedTrack != null)
            {
                string trackName = selectedTrack.name;
                
                // Unregister from TrackManager first
                if (TrackManager.Instance != null)
                {
                    TrackManager.Instance.UnregisterTrack(selectedTrack);
                }
                
                // Destroy the game object
                Destroy(selectedTrack.gameObject);
                
                selectedTrack = null;
                selectedTrackRenderer = null;
                
                // Debug.Log($"✓ Deleted track: {trackName}");
                
                // Regenerate paths after deletion
                if (TrackManager.Instance != null)
                {
                    TrackManager.Instance.RegenerateAllPaths();
                }
            }
            else
            {
                // Debug.Log("No track selected to delete");
            }
        }

        /// <summary>
        /// Select a track prefab by index
        /// </summary>
        public void SelectPrefab(int index)
        {
            if (index >= 0 && index < trackPrefabs.Length)
            {
                selectedPrefabIndex = index;
                
                // Recreate preview with new prefab
                if (isDragging)
                {
                    DestroyPreview();
                    CreatePreview();
                }

                // Debug.Log($"Selected track prefab: {trackPrefabs[selectedPrefabIndex].name}");
            }
        }

        /// <summary>
        /// Start dragging a track piece
        /// </summary>
        private void StartDragging(Vector3 position)
        {
            if (trackPrefabs == null || trackPrefabs.Length == 0)
            {
                Debug.LogWarning("TrackPlacer: No track prefabs assigned!");
                return;
            }

            if (selectedPrefabIndex < 0 || selectedPrefabIndex >= trackPrefabs.Length)
            {
                Debug.LogError($"TrackPlacer: Selected prefab index {selectedPrefabIndex} is out of bounds. Array length is {trackPrefabs.Length}.");
                return;
            }

            if (trackPrefabs[selectedPrefabIndex] == null)
            {
                Debug.LogWarning($"TrackPlacer: Track prefab at index {selectedPrefabIndex} is null!");
                return;
            }
            
            isDragging = true;
            dragStartPosition = position;
            currentRotation = 0f;

            CreatePreview();
        }

        /// <summary>
        /// Update dragging state
        /// </summary>
        private void UpdateDragging(Vector3 position)
        {
            if (previewObject == null)
                return;

            // Find snap target
            snapTarget = null;

            if (previewTrack != null && TrackManager.Instance != null)
            {
                if (!Application.isPlaying)
                {
                    TrackManager.Instance.RefreshTracks();
                }
                
                // Check each connection point for potential snaps
                foreach (var connectionPoint in previewTrack.ConnectionPoints)
                {
                    ConnectionPoint nearest = connectionPoint.FindNearestValidConnection();
                    
                    if (nearest != null)
                    {
                        snapTarget = nearest;
                        
                        // Align to snap target (this will override manual rotation)
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                        {
                            previewObject.transform.position = snapPos;
                            previewObject.transform.rotation = snapRot;
                            // Update currentRotation to match snap rotation
                            currentRotation = snapRot.eulerAngles.z;
                            return;
                        }
                    }
                }
            }

            // No snap target, just follow mouse and maintain manual rotation
            Vector3 targetPos = position;

            // Snap to grid if enabled
            if (snapToGrid)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.y = Mathf.Round(targetPos.y / gridSize) * gridSize;
            }

            previewObject.transform.position = targetPos;
            // Ensure rotation is applied when not snapping
            previewObject.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
        }

        /// <summary>
        /// End dragging and place the track
        /// </summary>
        private void EndDragging(Vector3 position)
        {
            isDragging = false;

            if (previewObject == null || previewTrack == null)
                return;

            // Check if placement is valid
            if (IsValidPlacement())
            {
                PlaceTrack();
            }
            else
            {
                // Debug.Log("Invalid placement location");
            }

            DestroyPreview();
            snapTarget = null;
        }

        /// <summary>
        /// Check if the current preview position is valid for placement
        /// </summary>
        private bool IsValidPlacement()
        {
            // For now, always return true
            // You can add custom validation logic here
            return true;
        }

        /// <summary>
        /// Create the preview object
        /// </summary>
        private void CreatePreview()
        {
            if (selectedPrefabIndex < 0 || selectedPrefabIndex >= trackPrefabs.Length)
                return;

            if (trackPrefabs[selectedPrefabIndex] == null)
                return;

            previewObject = Instantiate(trackPrefabs[selectedPrefabIndex]);
            previewObject.name = "Track Preview";
            
            previewTrack = previewObject.GetComponent<TrackPiece>();

            // Make preview semi-transparent
            foreach (var renderer in previewObject.GetComponentsInChildren<SpriteRenderer>())
            {
                Color color = renderer.color;
                color.a = 0.5f;
                renderer.color = color;
            }

            // Disable colliders on preview
            foreach (var collider in previewObject.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }
        }

        /// <summary>
        /// Update the preview object's visual state
        /// </summary>
        private void UpdatePreview()
        {
            if (previewObject == null || !isDragging)
                return;

            // Update preview color based on placement validity
            bool isValid = IsValidPlacement();
            Color previewColor = isValid ? validPlacementColor : invalidPlacementColor;

            foreach (var renderer in previewObject.GetComponentsInChildren<SpriteRenderer>())
            {
                Color color = renderer.color;
                color.r = previewColor.r;
                color.g = previewColor.g;
                color.b = previewColor.b;
                renderer.color = color;
            }
        }

        /// <summary>
        /// Destroy the preview object
        /// </summary>
        private void DestroyPreview()
        {
            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
                previewTrack = null;
            }
        }

        /// <summary>
        /// Place the track at the preview location
        /// </summary>
        private void PlaceTrack()
        {
            if (selectedPrefabIndex < 0 || selectedPrefabIndex >= trackPrefabs.Length)
                return;

            if (previewObject == null || trackPrefabs[selectedPrefabIndex] == null)
                return;

            // Create the actual track piece
            GameObject trackObject = Instantiate(trackPrefabs[selectedPrefabIndex], 
                previewObject.transform.position, 
                previewObject.transform.rotation);
            
            trackObject.name = trackPrefabs[selectedPrefabIndex].name;

            // Set layer and sorting layer
            SetLayerRecursively(trackObject, LayerMask.NameToLayer(trackLayer));
            
            foreach (var renderer in trackObject.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.sortingLayerName = trackSortingLayer;
            }

            // Get the track piece component
            TrackPiece track = trackObject.GetComponent<TrackPiece>();

            if (track != null && TrackManager.Instance != null)
            {
                // Refresh tracks in edit mode to ensure all tracks are registered
                if (!Application.isPlaying)
                {
                    TrackManager.Instance.RefreshTracks();
                }
                
                bool snapped = false;
                
                // Use snapTarget if we have one from the preview
                if (snapTarget != null)
                {
                    // Debug.Log($"Attempting to snap to {snapTarget.ParentTrack.name}");
                    
                    // Find which of our connection points is closest to snapTarget
                    ConnectionPoint closestPoint = null;
                    float closestDistance = float.MaxValue;
                    
                    foreach (var cp in track.ConnectionPoints)
                    {
                        float dist = Vector2.Distance(cp.WorldPosition, snapTarget.WorldPosition);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestPoint = cp;
                        }
                    }
                    
                    if (closestPoint != null && closestDistance < 5.0f)
                    {
                        if (closestPoint.CalculateAlignmentTo(snapTarget, out Vector3 snapPos, out Quaternion snapRot))
                        {
                            track.transform.position = snapPos;
                            track.transform.rotation = snapRot;
                            
                            float finalDist = Vector2.Distance(closestPoint.WorldPosition, snapTarget.WorldPosition);
                            
                            if (finalDist < 0.5f)
                            {
                                closestPoint.ConnectTo(snapTarget);
                                snapped = true;
                                // Debug.Log($"✓ Connected {track.name} to {snapTarget.ParentTrack.name}");
                            }
                        }
                    }
                }
                
                // If no snapTarget or snapping failed, try finding nearby connections
                if (!snapped)
                {
                    foreach (var connectionPoint in track.ConnectionPoints)
                    {
                        if (connectionPoint.IsConnected)
                            continue;

                        ConnectionPoint nearest = null;
                        float nearestDist = float.MaxValue;
                        
                        foreach (var otherTrack in TrackManager.Instance.GetAllTracks())
                        {
                            if (otherTrack == track) continue;
                            
                            foreach (var otherPoint in otherTrack.ConnectionPoints)
                            {
                                if (otherPoint.IsConnected) continue;
                                
                                float dist = Vector2.Distance(connectionPoint.WorldPosition, otherPoint.WorldPosition);
                                if (dist < 5.0f && dist < nearestDist)
                                {
                                    nearest = otherPoint;
                                    nearestDist = dist;
                                }
                            }
                        }
                        
                        if (nearest != null)
                        {
                            if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 pos, out Quaternion rot))
                            {
                                track.transform.position = pos;
                                track.transform.rotation = rot;
                                
                                float finalDist = Vector2.Distance(connectionPoint.WorldPosition, nearest.WorldPosition);
                                
                                if (finalDist < 0.5f)
                                {
                                    connectionPoint.ConnectTo(nearest);
                                    snapped = true;
                                    // Debug.Log($"✓ Connected to {nearest.ParentTrack.name}");
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (!snapped)
                {
                    // Debug.Log($"Placed {track.name} without snapping");
                }
                else
                {
                    // Regenerate paths after snapping
                    TrackManager.Instance.RegenerateAllPaths();
                }
            }

            // Debug.Log($"Placed track: {trackObject.name} at {trackObject.transform.position}");
        }

        /// <summary>
        /// Set layer recursively for all child objects
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDrawGizmos()
        {
            if (snapTarget != null && isDragging)
            {
                // Draw snap indicator
                Gizmos.color = snapIndicatorColor;
                Gizmos.DrawWireSphere(snapTarget.WorldPosition, snapIndicatorSize);
                
                // Draw line to snap target
                if (previewObject != null)
                {
                    Gizmos.DrawLine(previewObject.transform.position, snapTarget.WorldPosition);
                }
            }

            // Draw selection radius indicator when hovering
            if (!isDragging && mainCamera != null && Mouse.current != null)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 mousePos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
                mousePos.z = trackZPosition;
                
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(mousePos, selectionRadius);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/Off The Rails/Track Placer", false, 1)]
        private static void CreateTrackPlacer()
        {
            // Check if one already exists
            TrackPlacer existing = FindFirstObjectByType<TrackPlacer>();
            if (existing != null)
            {
                UnityEditor.Selection.activeGameObject = existing.gameObject;
                // Debug.Log("TrackPlacer already exists in scene");
                return;
            }

            GameObject placerObject = new GameObject("TrackPlacer");
            placerObject.AddComponent<TrackPlacer>();
            UnityEditor.Selection.activeGameObject = placerObject;
            // Debug.Log("Created TrackPlacer in scene");
        }
#endif
    }
}