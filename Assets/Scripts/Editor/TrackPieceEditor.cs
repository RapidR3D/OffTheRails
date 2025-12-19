using UnityEngine;
using UnityEditor;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    [CustomEditor(typeof(TrackPiece))]
    [CanEditMultipleObjects]
    public class TrackPieceEditor : Editor
    {
        private Tracks.TrackPiece trackPiece;
        private bool isDragging = false;

        private void OnEnable()
        {
            trackPiece = (Tracks.TrackPiece)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Snap to Connections"))
            {
                SnapTrackWithUndo();
                GUIUtility.ExitGUI();
            }
        }

        private void OnSceneGUI()
        {
            Event e = Event.current;

            // Check if we are dragging the object
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                isDragging = true;
            }

            // When mouse is released, try to snap
            if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
            {
                isDragging = false;
                SnapTrackWithUndo();
            }
        }

        private void SnapTrackWithUndo()
        {
            if (trackPiece == null) return;

            // Ensure TrackManager knows about all tracks
            if (TrackManager.Instance != null)
            {
                TrackManager.Instance.RefreshTracks();
            }

            bool snapped = false;

            foreach (var connectionPoint in trackPiece.ConnectionPoints)
            {
                // If already connected, disconnect first to allow re-snapping
                if (connectionPoint.IsConnected)
                {
                    connectionPoint.Disconnect();
                }



                // Use the looser search for editor snapping
                ConnectionPoint nearest = connectionPoint.FindNearestConnectionForSnapping();

                if (nearest != null)
                {
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 position, out Quaternion rotation))
                    {
                        // Record Undo
                        Undo.RecordObject(trackPiece.transform, "Snap Track");

                        // Apply alignment
                        trackPiece.transform.position = position;
                        trackPiece.transform.rotation = rotation;

                        // check if they can connect after alignment
                        if (connectionPoint.CanConnectTo(nearest))
                        {
                            connectionPoint.ConnectTo(nearest);
                            snapped = true;
                            Debug.Log($"Snapped {trackPiece.name} to {nearest.ParentTrack.name}");
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"After alignment, connection points could not connect: {connectionPoint.name} to {nearest.name}");
                        }

                        break;
                    }
                }
            }

            if (snapped)
            {
                // Regenerate paths if needed
                if (TrackManager.Instance != null)
                {
                    TrackManager.Instance.RegenerateAllPaths();
                }

                // Repaint scene to show updated position immediately
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogWarning("Could not find any nearby connection points to snap to (within 3.0 units).");
            }
        }
    }
}