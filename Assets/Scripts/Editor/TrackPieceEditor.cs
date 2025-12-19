using OffTheRails.Tracks;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(TrackPiece))]
    [CanEditMultipleObjects]
    public class TrackPieceEditor : UnityEditor.Editor
    {
        private OffTheRails.Tracks.TrackPiece trackPiece;
        private bool isDragging = false;

        private void OnEnable()
        {
            trackPiece = (OffTheRails.Tracks.TrackPiece)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Snap to Connections"))
            {
                SnapTrackWithUndo();
            }
        }

        private void OnSceneGUI()
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                isDragging = true;
            }

            if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
            {
                isDragging = false;
                SnapTrackWithUndo();
            }
        }

        private void SnapTrackWithUndo()
        {
            if (trackPiece == null) return;

            TrackManager manager = GetTrackManager();
            if (manager != null)
            {
                manager.RefreshTracks();
            }

            bool snapped = false;

            foreach (var connectionPoint in trackPiece.ConnectionPoints)
            {
                if (connectionPoint.IsConnected)
                {
                    Undo.RecordObject(connectionPoint, "Disconnect Track");
                    connectionPoint.Disconnect();
                }

                ConnectionPoint nearest = connectionPoint.FindNearestConnectionForEditor();

                if (nearest != null)
                {
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 position, out Quaternion rotation))
                    {
                        Undo.RecordObject(trackPiece.transform, "Snap Track");
                        Undo.RecordObject(connectionPoint, "Connect Track");
                        Undo.RecordObject(nearest, "Connect Track");

                        trackPiece.transform.position = position;
                        trackPiece.transform.rotation = rotation;

                        if (connectionPoint.CanConnectTo(nearest, skipDirectionCheck: true))
                        {
                            connectionPoint.ConnectTo(nearest);
                            snapped = true;
                    
                            EditorUtility.SetDirty(trackPiece);
                            EditorUtility.SetDirty(connectionPoint);
                            EditorUtility.SetDirty(nearest);
                    
                            Debug.Log($"Snapped {trackPiece.name} to {nearest.ParentTrack.name}");
                        }

                        break;
                    }
                }
            }

            if (snapped)
            {
                if (manager != null)
                {
                    manager.RegenerateAllPaths();
                    EditorUtility.SetDirty(manager);
                }
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogWarning("Could not find any nearby connection points to snap to.");
            }
        }

        /// <summary>
        /// Get TrackManager that works in both edit and play mode
        /// </summary>
        private TrackManager GetTrackManager()
        {
            return TrackManager.GetInstance();
        }
    }
}