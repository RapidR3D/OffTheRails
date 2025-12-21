using UnityEngine;
using OffTheRails.Trains;
using OffTheRails.Tracks;

/// <summary>
/// Test script that assigns a train to a path at startup.
/// Attach to a GameObject and assign a Train reference.
/// </summary>
public class TestTrainMovement : MonoBehaviour
{
    [SerializeField] private Train train;
    [SerializeField] private bool startFromLeft = true;
    
    void Start()
    {
        Debug.Log("=== TEST TRAIN MOVEMENT START ===");
        
        if (train == null)
        {
            Debug.LogError("Train reference is NULL!");
            return;
        }
        
        if (TrackManager.Instance == null)
        {
            Debug.LogError("TrackManager.Instance is NULL!");
            return;
        }
        
        // Regenerate paths based on current switch states
        TrackManager.Instance.RegenerateAllPaths();
        
        var paths = TrackManager.Instance.GetPaths();
        Debug.Log($"Found {paths.Count} paths");
        
        if (paths.Count == 0)
        {
            Debug.LogError("No paths found!");
            return;
        }
        
        // Log all paths
        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            Debug.Log($"Path {i}: {path.TrackPieces.Count} tracks, {path.Waypoints.Count} waypoints, length={path.TotalLength:F1}");
            Debug.Log($"  Start: {path.StartTrack?.name ?? "null"} at {(path.Waypoints.Count > 0 ? path.Waypoints[0].ToString() : "no waypoints")}");
            Debug.Log($"  End: {path.EndTrack?.name ?? "null"}");
        }
        
        // Select path and determine if we need to reverse it
        TrackPath selectedPath = null;
        
        if (paths.Count > 0)
        {
            selectedPath = paths[0];
            
            if (selectedPath.Waypoints.Count > 0)
            {
                // Check which end of the path is more to the left (smaller X)
                float startX = selectedPath.Waypoints[0].x;
                float endX = selectedPath.Waypoints[selectedPath.Waypoints.Count - 1].x;
                
                bool pathGoesLeftToRight = startX < endX;
                
                if (startFromLeft && !pathGoesLeftToRight)
                {
                    // We want left-to-right but path goes right-to-left, reverse it
                    Debug.Log($"Reversing path to go left-to-right (was X={startX:F2} → X={endX:F2})");
                    selectedPath.Reverse();
                }
                else if (!startFromLeft && pathGoesLeftToRight)
                {
                    // We want right-to-left but path goes left-to-right, reverse it
                    Debug.Log($"Reversing path to go right-to-left (was X={startX:F2} → X={endX:F2})");
                    selectedPath.Reverse();
                }
                else
                {
                    Debug.Log($"Path direction is correct: X={startX:F2} → X={endX:F2}");
                }
            }
        }
        
        if (selectedPath == null || selectedPath.Waypoints.Count == 0)
        {
            Debug.LogError("No valid path selected!");
            return;
        }
        
        Debug.Log($"Selected path: {selectedPath.StartTrack?.name} → {selectedPath.EndTrack?.name}");
        Debug.Log($"Path tracks: {string.Join(" → ", System.Linq.Enumerable.Select(selectedPath.TrackPieces, t => t.name))}");
        
        // Assign train to path at the start
        train.SetPath(selectedPath, 0f);
        
        Debug.Log($"Train assigned to path at position {train.transform.position}");
        Debug.Log("=== TEST TRAIN MOVEMENT COMPLETE ===");
    }
}
