using UnityEngine;
using UnityEditor;
using OffTheRails.Tracks;

[CustomEditor(typeof(TrackManager))]
public class TrackManagerEditor : Editor
{
    private TrackManager manager;

    private void OnEnable()
    {
        manager = (TrackManager)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Snap All Tracks"))
        {
            SnapAllTracksWithUndo();
        }

        if (GUILayout.Button("Regenerate Paths"))
        {
            manager.RegenerateAllPaths();
            SceneView.RepaintAll();
        }
    }

    private void SnapAllTracksWithUndo()
    {
        manager.RefreshTracks();
        var tracks = manager.GetAllTracks();
        int snapCount = 0;

        foreach (var track in tracks)
        {
            bool snapped = false;
            
            foreach (var connectionPoint in track.ConnectionPoints)
            {
                if (connectionPoint.IsConnected)
                {
                    connectionPoint.Disconnect();
                }

                // Use LOOSER search
                ConnectionPoint nearest = connectionPoint.FindNearestConnectionForSnapping(3.0f);

                if (nearest != null)
                {
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 position, out Quaternion rotation))
                    {
                        Undo.RecordObject(track.transform, "Snap Track");
                        
                        track.transform.position = position;
                        track.transform.rotation = rotation;

                        if (connectionPoint.CanConnectTo(nearest))
                        {
                            connectionPoint.ConnectTo(nearest);
                            snapped = true;
                            snapCount++;
                        }
                        
                        break; 
                    }
                }
            }
        }

        if (snapCount > 0)
        {
            Debug.Log($"✓ Snapped {snapCount} tracks");
            manager.RegenerateAllPaths();
            SceneView.RepaintAll();
        }
        else
        {
            Debug.LogWarning("No tracks could be snapped");
        }
    }
}

