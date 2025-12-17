using UnityEngine;

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

        [Header("Input Settings")]
        [Tooltip("Mouse button for placing tracks (0 = left, 1 = right, 2 = middle)")]
        [SerializeField] private int placeButton = 0;

        [Tooltip("Key to rotate track clockwise")]
        [SerializeField] private KeyCode rotateClockwiseKey = KeyCode.E;

        [Tooltip("Key to rotate track counter-clockwise")]
        [SerializeField] private KeyCode rotateCounterClockwiseKey = KeyCode.Q;

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
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = trackZPosition;

            // Handle rotation input
            if (Input.GetKeyDown(rotateClockwiseKey))
            {
                currentRotation += rotationStep;
                if (previewObject != null)
                {
                    previewObject.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                }
            }

            if (Input.GetKeyDown(rotateCounterClockwiseKey))
            {
                currentRotation -= rotationStep;
                if (previewObject != null)
                {
                    previewObject.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                }
            }

            // Handle number keys for prefab selection
            for (int i = 0; i < Mathf.Min(10, trackPrefabs.Length); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectPrefab(i);
                }
            }

            // Start dragging
            if (Input.GetMouseButtonDown(placeButton) && !isDragging)
            {
                StartDragging(mousePos);
            }

            // Continue dragging
            if (isDragging)
            {
                UpdateDragging(mousePos);
            }

            // End dragging
            if (Input.GetMouseButtonUp(placeButton) && isDragging)
            {
                EndDragging(mousePos);
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
            if (trackPrefabs[selectedPrefabIndex] == null)
                return;

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
            if (trackPrefabs[selectedPrefabIndex] == null)
                return;

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
        private void PlaceTrack()
        {
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
                // Try to snap to nearby connections
                TrackManager.Instance.TrySnapTrack(track);
            }

            Debug.Log($"Placed track: {trackObject.name} at {trackObject.transform.position}");
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
            TrackPlacer existing = FindObjectOfType<TrackPlacer>();
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
