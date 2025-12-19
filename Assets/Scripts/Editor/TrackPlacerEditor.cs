using OffTheRails.Tracks;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(TrackPlacer))]
    public class TrackPlacerEditor : UnityEditor.Editor
    {
        private TrackPlacer placer;
        private int selectedPrefabIndex = 0;
        private float currentRotation = 0f;
        private GameObject previewObject;
        private bool previewPositionValid = false;

        private void OnEnable()
        {
            placer = (TrackPlacer)target;
        }

        private void OnDisable()
        {
            previewPositionValid = false;
            DestroyPreview();
        }

        private void DestroyPreview()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
                previewObject = null;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Select the TrackPlacer object and use the Scene View to place tracks.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Controls:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("- Left Click: Place Track");
            EditorGUILayout.LabelField("- E/Q: Rotate");
            EditorGUILayout.LabelField("- 1-9: Select Prefab");

            EditorGUILayout.Space();
            if (GUILayout.Button("Snap All Tracks"))
            {
                SnapAllTracksWithUndo();
            }

            if (GUILayout.Button("Regenerate Paths"))
            {
                if (TrackManager.Instance != null)
                {
                    TrackManager.Instance.RegenerateAllPaths();
                }
            }
        }

        private void OnSceneGUI()
        {
            // Only handle events if we have prefabs
            SerializedProperty prefabsProp = serializedObject.FindProperty("trackPrefabs");
            if (prefabsProp.arraySize == 0) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            float rotationStep = serializedObject.FindProperty("rotationStep").floatValue;

            // Handle inputs
            switch (e.GetTypeForControl(controlID))
            {
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.E)
                    {
                        currentRotation -= rotationStep;
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Q)
                    {
                        currentRotation += rotationStep;
                        e.Use();
                    }
                    // Number keys for prefab selection
                    else if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
                    {
                        int index = e.keyCode - KeyCode.Alpha1;
                        if (index < prefabsProp.arraySize)
                        {
                            selectedPrefabIndex = index;
                            e.Use();
                        }
                    }

                    break;

                case EventType.MouseMove:
                    // Force repaint to update preview position
                    HandleUtility.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt) // Left click and not orbiting
                    {
                        // Place track
                        GUIUtility.hotControl = controlID;
                        PlaceTrack();
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;

                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlID);
                    break;
            }

            // Update Preview Logic (Position/Rotation)
            // We do this on Repaint to ensure smooth movement, or MouseMove
            // But we should avoid doing it on Layout if possible to avoid side effects
            if (e.type == EventType.Repaint || e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                UpdatePreviewTransform(GetMouseWorldPosition());
            }

            // Draw Visuals (Handles)
            // MUST be done only on Repaint
            if (e.type == EventType.Repaint)
            {
                DrawPreviewVisuals();
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            float zPos = serializedObject.FindProperty("trackZPosition").floatValue;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, zPos));
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            return new Vector3(0, 0, zPos);
        }

        private void UpdatePreviewTransform(Vector3 position)
        {
            SerializedProperty prefabsProp = serializedObject.FindProperty("trackPrefabs");
            if (prefabsProp.arraySize == 0) return;

            if (selectedPrefabIndex >= prefabsProp.arraySize) selectedPrefabIndex = 0;

            GameObject prefab =
                (GameObject)prefabsProp.GetArrayElementAtIndex(selectedPrefabIndex).objectReferenceValue;
            if (prefab == null)
            {
                DestroyPreview();
                return;
            }

            // Create preview if needed or if prefab changed
            if (previewObject == null)
            {
                CreatePreview(prefab);
            }
            else
            {
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(previewObject);
                if (sourcePrefab != prefab)
                {
                    DestroyPreview();
                    CreatePreview(prefab);
                }
            }

            if (previewObject == null) return;

            // Default position (grid snap)
            float gridSize = serializedObject.FindProperty("gridSize").floatValue;
            bool snapToGrid = serializedObject.FindProperty("snapToGrid").boolValue;

            Vector3 targetPos = position;
            Quaternion targetRot = Quaternion.Euler(0, 0, currentRotation);

            if (snapToGrid)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.y = Mathf.Round(targetPos.y / gridSize) * gridSize;
            }

            // Connection snap logic
            // We calculate the snap position here but don't draw handles
            TrackPiece previewTrack = previewObject.GetComponent<TrackPiece> ();
            //TrackManager manager = FindObjectOfType<TrackManager>();
            TrackManager manager = FindFirstObjectByType<TrackManager>();

            if (previewTrack != null && manager != null)
            {
                // Temporarily move to target position to check connections
                // This is safe as long as we don't rely on physics updates in the same frame
                previewObject.transform.position = targetPos;
                previewObject.transform.rotation = targetRot;

                foreach (var connectionPoint in previewTrack.ConnectionPoints)
                {
                    ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor();

                    if (nearest != null)
                    {
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                        {
                            targetPos = snapPos;
                            targetRot = snapRot;
                            break;
                        }
                    }
                }
            }

            previewObject.transform.position = targetPos;
            previewObject.transform.rotation = targetRot;
            
            previewPositionValid = true;
        }

        private void DrawPreviewVisuals()
        {
            if (previewObject == null || !previewPositionValid) return;

            TrackPiece previewTrack = previewObject.GetComponent<TrackPiece>();
            TrackManager manager = FindFirstObjectByType<TrackManager>();

            if (previewTrack != null && manager != null)
            {
                foreach (var connectionPoint in previewTrack.ConnectionPoints)
                {
                    // Guard: Only search if preview is actually positioned (not at origin)
                    // This prevents searching when preview is freshly spawned
                    if (previewTrack.transform.position.sqrMagnitude < 0.1f)
                    {
                        // Preview is at or near origin, skip connection checks
                        continue;
                    }

                    ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor();

                    if (nearest != null)
                    {
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                        {
                            Handles.color = Color.yellow;
                            Handles.DrawWireDisc(nearest.WorldPosition, Vector3.forward, 0.3f);
                            Handles.DrawLine(previewObject.transform.position, nearest.WorldPosition);
                            break;
                        }
                    }
                }
            }
        }

        private void CreatePreview(GameObject prefab)
        {
            previewObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            previewObject.name = "TrackPreview";
            previewObject.hideFlags = HideFlags.HideAndDontSave; // Don't save in scene, don't show in hierarchy

            // Disable TrackPiece component to prevent registration
            var trackPiece = previewObject.GetComponent<OffTheRails.Tracks.TrackPiece>();
            if (trackPiece != null) trackPiece.enabled = false;

            // Disable colliders
            foreach (var collider in previewObject.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }

            // Make transparent
            foreach (var renderer in previewObject.GetComponentsInChildren<SpriteRenderer>())
            {
                Color color = renderer.color;
                color.a = 0.5f;
                renderer.color = color;
            }
        }

        private void SnapAllTracksWithUndo()
        {
            //TrackManager manager = FindObjectOfType<TrackManager>();
            TrackManager manager = FindFirstObjectByType<TrackManager>();

            if (manager == null) return;

            manager.RefreshTracks();
            var tracks = manager.GetAllTracks();
            int snapCount = 0;

            foreach (var track in tracks)
            {
                bool snapped = false;
                foreach (var connectionPoint in track.ConnectionPoints)
                {
                    if (connectionPoint.IsConnected)
                        continue;

                    ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor();

                    if (nearest != null)
                    {
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 pos, out Quaternion rot))
                        {
                            Undo.RecordObject(track.transform, "Snap Track");

                            track.transform.position = pos;
                            track.transform.rotation = rot;

                            connectionPoint.ConnectTo(nearest);

                            snapped = true;
                            snapCount++;
                            break;
                        }
                    }
                }
            }

            if (snapCount > 0)
            {
                Debug.Log($"Snapped {snapCount} tracks.");
                manager.RegenerateAllPaths();
            }
        }

        private void PlaceTrack()
        {
            if (previewObject == null) return;
        
            SerializedProperty prefabsProp = serializedObject.FindProperty("trackPrefabs");
            GameObject prefab =
                (GameObject)prefabsProp.GetArrayElementAtIndex(selectedPrefabIndex).objectReferenceValue;
        
            if (prefab != null)
            {
                GameObject newTrack = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                newTrack.transform.position = previewObject.transform.position;
                newTrack.transform.rotation = previewObject.transform.rotation;
        
                // Set layer and sorting layer
                string trackLayer = serializedObject.FindProperty("trackLayer").stringValue;
                string trackSortingLayer = serializedObject.FindProperty("trackSortingLayer").stringValue;
        
                SetLayerRecursively(newTrack, LayerMask.NameToLayer(trackLayer));
                foreach (var renderer in newTrack.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderer.sortingLayerName = trackSortingLayer;
                }
        
                Undo.RegisterCreatedObjectUndo(newTrack, "Place Track");
        
                // Try to snap the newly placed track
                TrackManager manager = FindFirstObjectByType<TrackManager>();
                if (manager != null)
                {
                    manager.RefreshTracks();
        
                    TrackPiece piece = newTrack.GetComponent<TrackPiece>();
                    if (piece != null)
                    {
                        // Attempt to snap the newly placed track to nearby connections
                        foreach (var connectionPoint in piece.ConnectionPoints)
                        {
                            ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor();
                            if (nearest != null)
                            {
                                if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 snapPos, out Quaternion snapRot))
                                {
                                    Undo.RecordObject(newTrack.transform, "Snap Placed Track");
                                    newTrack.transform.position = snapPos;
                                    newTrack.transform.rotation = snapRot;
                                    
                                    if (connectionPoint.CanConnectTo(nearest))
                                    {
                                        connectionPoint.ConnectTo(nearest);
                                        EditorUtility.SetDirty(piece);
                                        EditorUtility.SetDirty(nearest);
                                    }
                                    break;
                                }
                            }
                        }
                        
                        manager.RegenerateAllPaths();
                    }
                }
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}