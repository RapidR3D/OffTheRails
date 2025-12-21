using UnityEngine;
using OffTheRails.Trains;
using OffTheRails.Tracks;

public class TestTrainMovement : MonoBehaviour
{
    [SerializeField] private Train train;
    
    void Start()
    {
        Debug.Log("=== TEST TRAIN MOVEMENT START ===");
        
        if (train == null)
        {
            Debug.LogError("Train is NULL!");
            return;
        }
        
        if (TrackManager.Instance == null)
        {
            Debug.LogError("TrackManager.Instance is NULL!");
            return;
        }
        
        TrackManager.Instance.RegenerateAllPaths();
        var paths = TrackManager.Instance.GetPaths();
        Debug.Log($"✓ Found {paths.Count} paths");
        
        if (paths.Count == 0)
        {
            Debug.LogError("No paths found!");
            return;
        }
        
        // Show ALL paths with their start/end points
        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            if (path == null || path.Waypoints == null || path.Waypoints.Count == 0)
            {
                Debug.LogWarning($"Path {i} is invalid!");
                continue;
            }
            
            Debug.Log($"━━━ PATH {i} ━━━");
            Debug.Log($"Tracks: {path.TrackPieces.Count}");
            Debug.Log($"Waypoints: {path.Waypoints.Count}");
            Debug.Log($"First track: {path.TrackPieces[0].name}");
            Debug.Log($"Last track: {path.TrackPieces[path.TrackPieces.Count - 1].name}");
            Debug.Log($"First waypoint: {path.Waypoints[0]}");
            Debug.Log($"Last waypoint: {path.Waypoints[path.Waypoints.Count - 1]}");
        }
        
        // Select the path that starts at the ACTUAL beginning
        // (Look for a path that starts with "StraightTrack (1)" or similar)
        TrackPath selectedPath = null;
        float minX = float.MaxValue;

        foreach (var path in paths)
        {
            if (path.Waypoints.Count == 0)
                continue;
        
            float startX = path.Waypoints[0].x;
    
            if (startX < minX)
            {
                minX = startX;
                selectedPath = path;
            }
        }

        /*if (selectedPath != null)
        {
            Debug.Log($"✓ Selected path starting at X={minX:F2} (leftmost position)");
        }
        // Option 1: Let user specify which track should be the start
        string desiredStartTrack = "StraightTrack (9)"; // ← CHANGE THIS to your actual first track name
        
        foreach (var path in paths)
        {
            if (path.Waypoints.Count > 0 && path.TrackPieces[0].name == desiredStartTrack)
            {
                selectedPath = path;
                Debug.Log($"✓ Found path starting with '{desiredStartTrack}'!");
                break;
            }
        }*/
        
        // Option 2: If no specific start found, use the longest path
        /*if (selectedPath == null)
        {
            Debug.LogWarning($"No path starting with '{desiredStartTrack}' found, using longest path");
            int maxWaypoints = 0;
            foreach (var path in paths)
            {
                if (path.Waypoints.Count > maxWaypoints)
                {
                    maxWaypoints = path.Waypoints.Count;
                    selectedPath = path;
                }
            }
        }*/
        
        if (selectedPath == null)
        {
            Debug.LogError("❌ No valid path selected!");
            return;
        }
        
        Debug.Log($"═══ SELECTED PATH ═══");
        Debug.Log($"Waypoints: {selectedPath.Waypoints.Count}");
        Debug.Log($"First track: {selectedPath.TrackPieces[0].name}");
        Debug.Log($"First waypoint: {selectedPath.Waypoints[0]}");
        
        Debug.Log($"BEFORE SetPath - Train position: {train.transform.position}");
        
        train.SetPath(selectedPath, 0f);
        
        Debug.Log($"AFTER SetPath - Train position: {train.transform.position}");
        Debug.Log($"Train IsActive: {train.IsActive}");
        Debug.Log($"Train Speed: {train.Speed}");
        
        Debug.Log("=== TEST TRAIN MOVEMENT COMPLETE ===");
    }
}