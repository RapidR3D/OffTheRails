using OffTheRails.Tracks;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(TrackManager))]
    public class TrackManagerEditor : UnityEditor.Editor
    {
        private TrackManager manager;

        private void OnEnable()
        {
            manager = (TrackManager)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            /*if (GUILayout.Button("Snap All Tracks"))
            {
                SnapAllTracksWithUndo();
            }

            if (GUILayout.Button("Regenerate Paths"))
            {
                manager.RegenerateAllPaths();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }*/
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Snap All Tracks works in both Edit and Play mode", MessageType.Info);
        }

        private void SnapAllTracksWithUndo()
        {
            if (manager == null) return;

            manager.RefreshTracks();
            var tracks = manager.GetAllTracks();
            
            // Debug.Log($"=== SNAP ALL TRACKS START === ({System.Linq.Enumerable.Count(tracks)} tracks)");
            
            if (tracks == null)
            {
                Debug.LogWarning("No tracks found.");
                return;
            }

            int snapCount = 0;
            int attemptCount = 0;

            foreach (var track in tracks)
            {
                if (track == null) continue;

                bool snapped = false;

                foreach (var connectionPoint in track.ConnectionPoints)
                {
                    if (connectionPoint == null) continue;

                    // Disconnect if already connected
                    if (connectionPoint.IsConnected)
                    {
                        // Debug.Log($" Disconnecting {connectionPoint.name} from {connectionPoint.ConnectedTo?.name}");
                        Undo.RecordObject(connectionPoint, "Disconnect Track");
                        connectionPoint.Disconnect();
                    }

                    // Find nearest connection
                    // Debug.Log($" Searching for nearest connection to {connectionPoint.name} at {connectionPoint.WorldPosition}");
                    ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor(5.0f);

                    if (nearest != null)
                    {
                        attemptCount++;
                        // Debug.Log($" ✓ Found nearest: {nearest.name} at {nearest.WorldPosition} (distance: {Vector2.Distance(connectionPoint.WorldPosition, nearest.WorldPosition):F3})");
                        
                        if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 position, out Quaternion rotation))
                        {
                            // Debug.Log($" ✓ Alignment calculated: pos={position}, rot={rotation.eulerAngles}");
                            
                            // Record undo
                            Undo.RecordObject(track.transform, "Snap Track");
                            Undo.RecordObject(connectionPoint, "Connect Track");
                            Undo.RecordObject(nearest, "Connect Track");

                            // Apply transform
                            track.transform.position = position;
                            track.transform.rotation = rotation;

                            // Try to connect
                            if (connectionPoint.CanConnectTo(nearest, skipDirectionCheck: true))
                            {
                                connectionPoint.ConnectTo(nearest);
                                snapped = true;
                                
                                // Mark dirty for edit mode
                                EditorUtility.SetDirty(track);
                                EditorUtility.SetDirty(connectionPoint);
                                EditorUtility.SetDirty(nearest);
                                
                                // Debug.Log($" ✓✓✓ SNAPPED {track.name} to {nearest.ParentTrack.name}");
                            }
                            else
                            {
                                Debug.LogWarning($" ✗ CanConnectTo returned FALSE for {connectionPoint.name} -> {nearest.name}");
                            }

                            break; // Only snap one connection per track
                        }
                        else
                        {
                            Debug.LogWarning($" ✗ CalculateAlignmentTo FAILED for {connectionPoint.name} -> {nearest.name}");
                        }
                    }
                    else
                    {
                        // Debug.Log($" ✗ No nearest connection found for {connectionPoint.name}");
                    }
                }

                if (snapped)
                    snapCount++;
            }

            // Debug.Log($"=== SNAP ALL TRACKS COMPLETE ===");
            // Debug.Log($"Snap attempts: {attemptCount}, Successful: {snapCount}");

            if (snapCount > 0)
            {
                // Debug.Log($"✓ Regenerating paths.. .");
                manager.RegenerateAllPaths();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogWarning("⚠ No tracks could be snapped. Make sure tracks are within 5 units of each other.");
            }
        }
        
        
    }
}