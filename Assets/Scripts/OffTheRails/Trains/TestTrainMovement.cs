using UnityEngine;
using OffTheRails.Trains;
using OffTheRails.Tracks;
using System.Collections.Generic;

/// <summary>
/// Test script that assigns trains to paths at startup.
/// Can set up two trains on a collision course to test switch diversion.
/// </summary>
public class TestTrainMovement : MonoBehaviour
{
    [Header("Train 1 - Left to Right")]
    [SerializeField] private Train train1;
    [SerializeField] private bool enableTrain1 = true;
    
    [Header("Train 2 - Right to Left (Collision Course)")]
    [SerializeField] private Train train2;
    [SerializeField] private bool enableTrain2 = true;
    
    // Store paths separately so they don't interfere with each other
    private TrackPath train1Path;
    private TrackPath train2Path;
    
    void Start()
    {
        Debug.Log("=== TEST TRAIN MOVEMENT START ===");
        
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
        }
        
        // Get the main path's endpoints
        TrackPiece startTrack = paths[0].StartTrack;
        TrackPiece endTrack = paths[0].EndTrack;
        
        // Setup Train 1 - going LEFT to RIGHT
        if (enableTrain1 && train1 != null)
        {
            // Build a fresh path for train 1
            train1Path = new TrackPath();
            train1Path.BuildFromEndpoints(startTrack, endTrack);
            
            if (train1Path.Waypoints.Count > 0)
            {
                // Make sure it goes left to right
                float startX = train1Path.Waypoints[0].x;
                float endX = train1Path.Waypoints[train1Path.Waypoints.Count - 1].x;
                
                if (startX > endX)
                {
                    Debug.Log("Train1: Reversing path to go left-to-right");
                    train1Path.Reverse();
                }
                
                train1.SetPath(train1Path, 0f);
                Debug.Log($"Train1: Going LEFT → RIGHT, starting at {train1.transform.position}");
            }
        }
        
        // Setup Train 2 - going RIGHT to LEFT (collision course!)
        if (enableTrain2 && train2 != null)
        {
            // Build a completely separate path for train 2
            train2Path = new TrackPath();
            train2Path.BuildFromEndpoints(startTrack, endTrack);
            
            if (train2Path.Waypoints.Count > 0)
            {
                // Make sure it goes right to left
                float startX = train2Path.Waypoints[0].x;
                float endX = train2Path.Waypoints[train2Path.Waypoints.Count - 1].x;
                
                if (startX < endX)
                {
                    Debug.Log("Train2: Reversing path to go right-to-left");
                    train2Path.Reverse();
                }
                
                train2.SetPath(train2Path, 0f);
                Debug.Log($"Train2: Going RIGHT → LEFT, starting at {train2.transform.position}");
            }
        }
        
        Debug.Log("=== TEST TRAIN MOVEMENT COMPLETE ===");
        Debug.Log(">>> Toggle the switch to divert Train1 and avoid collision! <<<");
    }
}
