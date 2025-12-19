using UnityEngine;
using UnityEngine.InputSystem;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Handles player interaction for placing track pieces.
    /// Supports drag and drop, snapping, and visual feedback.
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

        [Tooltip("Layer mask for selecting tracks")]
        [SerializeField] private LayerMask trackLayerMask;

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

            // Ensure layer mask is set if empty
            if (trackLayerMask.value == 0)
            {
                int layer = LayerMask.NameToLayer(trackLayer);
                if (layer != -1)
                {
                    trackLayerMask = 1 << layer;
                    Debug.Log($"TrackPlacer: Automatically set trackLayerMask to '{trackLayer}' layer.");
                }
                else
                {
                    Debug.LogWarning($"TrackPlacer: Layer '{trackLayer}' not found! Please set trackLayerMask manually.");
                }
            }
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

            // Handle rotation input
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

            // Handle number keys for prefab selection
            // Note: This is a simplified check for number keys 1-9
            for (int i = 0; i < Mathf.Min(9, trackPrefabs.Length); i++)
            {
                // Key.Digit1 is 49, Key.Digit9 is 57
                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    SelectPrefab(i);
                }
            }

            // Handle deletion
            if (Keyboard.current.deleteKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                DeleteSelectedTrack();
            }

            // Handle selection (Right Click)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0.1f, trackLayerMask);
                
                if (hit.collider != null)
                {
                    TrackPiece clickedTrack = hit.collider.GetComponentInParent<TrackPiece>();
                    if (clickedTrack != null)
                    {
                        SelectTrack(clickedTrack);
                    }
                }
                else
                {
                    DeselectTrack();
                }
            }

            // Handle placement (Left Click)
            if (Mouse.current.leftButton.wasPressedThisFrame && !isDragging)
            {
                // Check if we clicked on an existing track
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0.1f, trackLayerMask);
                
                if (hit.collider != null)
                {
                    TrackPiece clickedTrack = hit.collider.GetComponentInParent<TrackPiece>();
                    if (clickedTrack != null)
                    {
                        SelectTrack(clickedTrack);
                        return; // Don't start dragging if we selected a track
                    }
                }

                // If we didn't click a track, start dragging new one
                DeselectTrack();
                StartDragging(mousePos);
            }

            // Continue dragging
            if (isDragging)
            {
                UpdateDragging(mousePos);
            }

            // End dragging
            if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
            {
                EndDragging(mousePos);
            }
        }

        private void SelectTrack(TrackPiece track)
        {
            if (selectedTrack == track) return;

            DeselectTrack();

            selectedTrack = track;
            if (selectedTrack != null)
            {
                // Highlight the selected track
                var renderer = selectedTrack.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null)
                {
                    originalTrackColor = renderer.color;
                    renderer.color = selectionColor;
                }
                Debug.Log($"Selected track: {selectedTrack.name}");
            }
        }

        private void DeselectTrack()
        {
            if (selectedTrack != null)
            {
                // Restore original color
                var renderer = selectedTrack.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = originalTrackColor;
                }
                selectedTrack = null;
            }
        }

        private void DeleteSelectedTrack()
        {
            if (selectedTrack != null)
            {
                Debug.Log($"Deleting track: {selectedTrack.name}");
                Destroy(selectedTrack.gameObject);
                selectedTrack = null;
            }
        }

        /// <summary>
        /// Select a track prefab by index
        /// </summary>
        /// <param name="index">Index of the prefab to select</param>
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

                Debug.Log($"Selected track prefab: {trackPrefabs[selectedPrefabIndex].name}");
            }
        }

        /// <summary>
        /// Start dragging a track piece
        /// </summary>
        /// <param name="position">Starting position</param>
        private void StartDragging(Vector3 position)
        {
            // Validate array bounds FIRST
            if (trackPrefabs == null || trackPrefabs. Length == 0)
            {
                Debug.LogWarning("TrackPlacer: No track prefabs assigned!");
                return;
            }

            if (selectedPrefabIndex < 0 || selectedPrefabIndex >= trackPrefabs.Length)
            {
                Debug.LogError($"TrackPlacer: Selected prefab index {selectedPrefabIndex} is out of bounds.  Array length is {trackPrefabs.Length}.");
                return;
            }

            if (trackPrefabs[selectedPrefabIndex] == null)
            {
                Debug.LogWarning($"TrackPlacer: Track prefab at index {selectedPrefabIndex} is null!");
                return;
            }
            
            /*try
            {
                if (selectedPrefabIndex < 0 || selectedPrefabIndex >= trackPrefabs.Length)
                {
                    Debug.LogError($"TrackPlacer: Selected prefab index {selectedPrefabIndex} is out of bounds. Array length is {trackPrefabs.Length}.");
                    return;
                }

                if (trackPrefabs[selectedPrefabIndex] == null)
                    return;

                isDragging = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in StartDragging: {e.Message}");
                return;
            }*/
            isDragging = true;
            dragStartPosition = position;
            currentRotation = 0f;

            CreatePreview();
        }

        /// <summary>
        /// Update dragging state
        /// </summary>
        /// <param name="position">Current mouse position</param>
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
                        
                        // Align to snap target
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                        {
                            previewObject.transform.position = snapPos;
                            previewObject.transform.rotation = snapRot;
                            return;
                        }
                    }
                }
            }

            // No snap target, just follow mouse
            Vector3 targetPos = position;

            // Snap to grid if enabled
            if (snapToGrid)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.y = Mathf.Round(targetPos.y / gridSize) * gridSize;
            }

            previewObject.transform.position = targetPos;
        }

        /// <summary>
        /// End dragging and place the track
        /// </summary>
        /// <param name="position">Final position</param>
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
                Debug.Log("Invalid placement location");
            }

            DestroyPreview();
            snapTarget = null;
        }

        /// <summary>
        /// Create preview object
        /// </summary>
        private void CreatePreview()
        {
            // Add validation
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
                

            previewObject = Instantiate(trackPrefabs[selectedPrefabIndex]);
            previewObject.name = "TrackPreview";
            
            previewTrack = previewObject.GetComponent<TrackPiece>();
            
            if (previewTrack == null)
            {
                Debug.LogError("Selected prefab does not have a TrackPiece component!");
                Destroy(previewObject);
                previewObject = null;
                return;
            }

            // Make preview semi-transparent
            SetPreviewTransparency(0.5f);

            // Disable colliders in preview
            foreach (var collider in previewObject.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }
        }
        
        /// <summary>
        /// Destroy the preview object
        /// </summary>
        private void DestroyPreview()
        {
            if (previewObject != null)
            {
                if (Application.isPlaying)
                    Destroy(previewObject);
                else
                    DestroyImmediate(previewObject);
            
                previewObject = null;
                previewTrack = null;
            }
        }

        /// <summary>
        /// Update preview visual feedback
        /// </summary>
        private void UpdatePreview()
        {
            if (previewObject == null || !isDragging)
                return;

            // Set color based on placement validity
            Color color = IsValidPlacement() ? validPlacementColor : invalidPlacementColor;
            SetPreviewColor(color);
        }

        /// <summary>
        /// Set preview transparency
        /// </summary>
        /// <param name="alpha">Alpha value</param>
        private void SetPreviewTransparency(float alpha)
        {
            if (previewObject == null)
                return;

            foreach (var renderer in previewObject.GetComponentsInChildren<SpriteRenderer>())
            {
                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }

        /// <summary>
        /// Set preview color
        /// </summary>
        /// <param name="color">Color to set</param>
        private void SetPreviewColor(Color color)
        {
            if (previewObject == null)
                return;

            foreach (var renderer in previewObject.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.color = color;
            }
        }

        /// <summary>
        /// Check if current placement is valid
        /// </summary>
        /// <returns>True if placement is valid</returns>
        private bool IsValidPlacement()
        {
            // For now, any placement is valid
            // Could add checks for overlapping tracks, etc.
            return true;
        }

        /// <summary>
        /// Place the track at the preview location
        /// </summary>
        /// <summary>
        /// Place the track at the preview location
        /// </summary>
       /// <summary>
/// Place the track at the preview location
/// </summary>
/*private void PlaceTrack()
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
        // Refresh tracks in edit mode
        if (!Application.isPlaying)
        {
            TrackManager.Instance.RefreshTracks();
        }
        
        // Try to snap to the SPECIFIC snapTarget we found during drag
        bool snapped = false;
        
        if (snapTarget != null)
        {
            // Find which of OUR connection points is closest to snapTarget
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
            
            if (closestPoint != null)
            {
                Debug.Log($"Attempting snap: {closestPoint.name} to {snapTarget.ParentTrack.name}, distance={closestDistance:F3}");
                
                // Calculate and apply alignment
                if (closestPoint.CalculateAlignmentTo(snapTarget, out Vector3 snapPos, out Quaternion snapRot))
                {
                    track.transform.position = snapPos;
                    track.transform.rotation = snapRot;
                    
                    // Re-check distance after alignment
                    float finalDistance = Vector2.Distance(closestPoint.WorldPosition, snapTarget.WorldPosition);
                    
                    Debug.Log($"After alignment, distance={finalDistance:F3}");
                    
                    // Only connect if VERY close after alignment (within 0.1 units)
                    if (finalDistance < 0.1f && closestPoint.CanConnectTo(snapTarget, skipDirectionCheck: true))
                    {
                        closestPoint.ConnectTo(snapTarget);
                        snapped = true;
                        Debug.Log($"✓ Placed and connected {track.name} to {snapTarget.ParentTrack.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"✗ Alignment failed: final distance {finalDistance:F3} too large or can't connect");
                    }
                }
            }
        }
        else
        {
            // No snapTarget - try to find nearby connections anyway
            foreach (var connectionPoint in track.ConnectionPoints)
            {
                if (connectionPoint.IsConnected)
                    continue;

                ConnectionPoint nearest = connectionPoint.FindNearestValidConnection();
                
                if (nearest != null)
                {
                    Debug.Log($"Found nearby connection: {nearest.ParentTrack.name}");
                    
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                    {
                        track.transform.position = snapPos;
                        track.transform.rotation = snapRot;
                        
                        float finalDistance = Vector2.Distance(connectionPoint.WorldPosition, nearest.WorldPosition);
                        
                        if (finalDistance < 0.1f && connectionPoint.CanConnectTo(nearest, skipDirectionCheck: true))
                        {
                            connectionPoint.ConnectTo(nearest);
                            snapped = true;
                            Debug.Log($"✓ Connected {track.name} to {nearest.ParentTrack.name}");
                            break; // Only snap ONE connection point
                        }
                    }
                }
            }
        }
        
        if (!snapped)
        {
            Debug.Log($"Placed {track.name} without snapping");
        }
    }

    Debug.Log($"Placed track: {trackObject.name} at {trackObject.transform.position}");
}*/

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
        // IMPORTANT:  Refresh tracks in edit mode to ensure all tracks are registered
        if (!Application.isPlaying)
        {
            TrackManager.Instance.RefreshTracks();
        }
        
        bool snapped = false;
        
        // Use snapTarget if we have one from the preview
        if (snapTarget != null)
        {
            Debug.Log($"Attempting to snap to {snapTarget.ParentTrack.name}");
            
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
            
            if (closestPoint != null && closestDistance < 5.0f) // Within 5 units
            {
                Debug.Log($"Closest point:  {closestPoint.name}, distance: {closestDistance: F3}");
                
                if (closestPoint.CalculateAlignmentTo(snapTarget, out Vector3 snapPos, out Quaternion snapRot))
                {
                    track.transform.position = snapPos;
                    track.transform.rotation = snapRot;
                    
                    // Check final distance after alignment
                    float finalDist = Vector2.Distance(closestPoint.WorldPosition, snapTarget.WorldPosition);
                    Debug.Log($"After alignment, distance: {finalDist:F4}");
                    
                    if (finalDist < 0.5f)
                    {
                        // Close enough - connect with direction check skipped
                        closestPoint.ConnectTo(snapTarget);
                        snapped = true;
                        Debug.Log($"✓ Connected {track.name} to {snapTarget.ParentTrack.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"After alignment, still too far: {finalDist:F4}");
                    }
                }
            }
        }
        
        // If no snapTarget or snapping failed, try finding nearby connections
        if (!snapped)
        {
            Debug.Log("No snapTarget, searching for nearby connections...");
            
            foreach (var connectionPoint in track.ConnectionPoints)
            {
                if (connectionPoint.IsConnected)
                    continue;

                // Find ALL nearby points (not just valid ones)
                ConnectionPoint nearest = null;
                float nearestDist = float.MaxValue;
                
                foreach (var otherTrack in TrackManager.Instance.GetAllTracks())
                {
                    if (otherTrack == track) continue;
                    
                    foreach (var otherPoint in otherTrack.ConnectionPoints)
                    {
                        if (otherPoint.IsConnected) continue;
                        
                        float dist = Vector2.Distance(connectionPoint.WorldPosition, otherPoint.WorldPosition);
                        if (dist < 5.0f && dist < nearestDist) // Within 5 units
                        {
                            nearest = otherPoint;
                            nearestDist = dist;
                        }
                    }
                }
                
                if (nearest != null)
                {
                    Debug.Log($"Found nearby connection: {nearest.ParentTrack.name}, distance: {nearestDist:F3}");
                    
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 pos, out Quaternion rot))
                    {
                        track.transform.position = pos;
                        track.transform.rotation = rot;
                        
                        float finalDist = Vector2.Distance(connectionPoint.WorldPosition, nearest.WorldPosition);
                        
                        if (finalDist < 0.5f)
                        {
                            connectionPoint.ConnectTo(nearest);
                            snapped = true;
                            Debug.Log($"✓ Connected to {nearest.ParentTrack.name}");
                            break; // Only snap one connection
                        }
                    }
                }
            }
        }
        
        if (!snapped)
        {
            Debug.Log($"Placed {track.name} without snapping");
        }
        else
        {
            // Regenerate paths after snapping
            TrackManager.Instance.RegenerateAllPaths();
        }
    }

    Debug.Log($"Placed track: {trackObject.name} at {trackObject.transform.position}");
}

        /// <summary>
        /// Set layer recursively for all child objects
        /// </summary>
        /// <param name="obj">Object to set layer for</param>
        /// <param name="layer">Layer to set</param>
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
                Debug.Log("TrackPlacer already exists in scene");
                return;
            }

            GameObject placerObject = new GameObject("TrackPlacer");
            placerObject.AddComponent<TrackPlacer>();
            UnityEditor.Selection.activeGameObject = placerObject;
            Debug.Log("Created TrackPlacer in scene");
        }
#endif
    }
}