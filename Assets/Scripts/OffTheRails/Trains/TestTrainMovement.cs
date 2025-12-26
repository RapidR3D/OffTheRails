using UnityEngine;
using OffTheRails.Trains;
using OffTheRails.Tracks;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Test script that assigns trains to paths at startup.
/// Can set up two trains on a collision course to test switch diversion.
/// </summary>
public class TestTrainMovement : MonoBehaviour
{
    [Header("Train 1 - Left to Right")]
    [SerializeField, Required] private Train train1;
    [SerializeField] private bool enableTrain1 = true;
    
    // [Header("Train 2 - Right to Left (Collision Course)")]
    // [SerializeField, Required] private Train train2;
    // [SerializeField] private bool enableTrain2 = true;
    
    // Store paths separately so they don't interfere with each other
    [ShowInInspector, ReadOnly, FoldoutGroup("Debug Info")]
    private TrackPath train1Path;
    
    [ShowInInspector, ReadOnly, FoldoutGroup("Debug Info")]
    private TrackPath train2Path;
    
    [ShowInInspector, ReadOnly, FoldoutGroup("Debug Info")]
    private Vector2 train1StartPos;
    
    [ShowInInspector, ReadOnly, FoldoutGroup("Debug Info")]
    private Vector2 train2StartPos;
    
    [Button("Setup Trains"), GUIColor(0, 1, 0)]
    [EnableIf("@UnityEngine.Application.isPlaying")]
    
    //public RouteDefinition MainLR;
    private void SetupTrains()
    {
        Start();
    }
    
    [Button("Reset Train Positions")]
    [EnableIf("@UnityEngine.Application.isPlaying")]
    private void ResetTrainPositions()
    {
        if (train1 != null && train1Path != null)
            train1.SetPath(train1Path, 0f);
        // if (train2 != null && train2Path != null)
        // //train2.SetPath(train2Path, 0f);
    }
    
    void Start()
    {
        // Debug.Log("=== TEST TRAIN MOVEMENT START ===");
        
        if (TrackManager.Instance == null)
        {
            Debug.LogError("TrackManager.Instance is NULL!");
            return;
        }
        
        // Regenerate paths based on current switch states
        TrackManager.Instance.RegenerateAllPaths();
        
        //RouteManager.Instance.AssignTrainToRoute(train1, MainLR, true);
        
        var paths = TrackManager.Instance.GetPaths();
        // Debug.Log($"Found {paths.Count} paths");
        
        if (paths.Count == 0)
        {
            Debug.LogError("No paths found!");
            return;
        }
        
        // Log all paths
        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            // Debug.Log($"Path {i}: {path.TrackPieces.Count} tracks, {path.Waypoints.Count} waypoints, length={path.TotalLength:F1}");
        }
        
        // Find all endpoints and log them
        // Debug.Log("=== ALL ENDPOINTS ===");
        var allTracks = TrackManager.Instance.GetAllTracks();
        
        foreach (var track in allTracks)
        {
            bool hasUnconnected = false;
            foreach (var cp in track.ConnectionPoints)
            {
                if (!cp.IsConnected)
                {
                    hasUnconnected = true;
                    break;
                }
            }
            if (hasUnconnected)
            {
                // Debug.Log($"Endpoint: {track.name} at position {track.transform.position}");
            }
        }
        
        // Known WAYPOINT coordinates (measured with cursor tool):
        // Main line left endpoint: (-102.38, -70.11) - Engine 1 start, Engine 2 end
        // Main line right endpoint: (141.11, -50.65) - Engine 1 end (straight), Engine 2 start
        // Diverging branch endpoint: (141.33, -27.44) - Engine 1 end (diverging)
        
        Vector2 mainLineLeft = new Vector2(-102.38f, -70.11f);
        Vector2 mainLineRight = new Vector2(141.11f, -50.65f);
        
        // Find the path that has waypoints closest to BOTH main line endpoints
        // This ensures we get the straight-through path, not the diverging one
        TrackPath mainLinePath = null;
        float bestScore = float.MaxValue;
        
        foreach (var path in paths)
        {
            if (path.Waypoints.Count < 2) continue;
            
            Vector2 firstWP = path.Waypoints[0];
            Vector2 lastWP = path.Waypoints[path.Waypoints.Count - 1];
            
            // Check both orientations (path could be built in either direction)
            float score1 = Vector2.Distance(firstWP, mainLineLeft) + Vector2.Distance(lastWP, mainLineRight);
            float score2 = Vector2.Distance(firstWP, mainLineRight) + Vector2.Distance(lastWP, mainLineLeft);
            float score = Mathf.Min(score1, score2);
            
            // Debug.Log($"Path check: first={firstWP}, last={lastWP}, score={score:F1}");
            
            if (score < bestScore)
            {
                bestScore = score;
                mainLinePath = path;
            }
        }
        
        if (mainLinePath == null)
        {
            Debug.LogError("Could not find main line path!");
            return;
        }
        
        // Debug.Log($"Selected main line path with score {bestScore:F1}");
        
        // Setup Train 1 - going LEFT to RIGHT on main line
        if (enableTrain1 && train1 != null)
        {
            // Clone the path for train 1
            train1Path = new TrackPath();
            train1Path.BuildFromEndpoints(mainLinePath.StartTrack, mainLinePath.EndTrack);
            
            if (train1Path.Waypoints.Count > 0)
            {
                // Make sure it starts from the LEFT (-102) and goes RIGHT (141)
                float firstX = train1Path.Waypoints[0].x;
                float lastX = train1Path.Waypoints[train1Path.Waypoints.Count - 1].x;
                
                if (firstX > lastX)
                {
                    // Debug.Log("Train1: Reversing path to go left-to-right");
                    train1Path.Reverse();
                }
                
                train1.SetPath(train1Path, 0f);
                train1StartPos = train1Path.Waypoints[0];
                // Debug.Log($"Train1: Going LEFT → RIGHT, starting at {train1.transform.position}, first waypoint={train1Path.Waypoints[0]}");
            }
        }
        
        // Setup Train 2 - going RIGHT to LEFT on main line (collision course!)
        /*if (enableTrain2 && train2 != null)
        {
            // Clone the path for train 2
            train2Path = new TrackPath();
            train2Path.BuildFromEndpoints(mainLinePath.StartTrack, mainLinePath.EndTrack);
            
            if (train2Path.Waypoints.Count > 0)
            {
                // Make sure it starts from the RIGHT (141) and goes LEFT (-102)
                float firstX = train2Path.Waypoints[0].x;
                float lastX = train2Path.Waypoints[train2Path.Waypoints.Count - 1].x;
                
                // Debug.Log($"Train2 path BEFORE: waypoint[0]={train2Path.Waypoints[0]}, waypoint[last]={train2Path.Waypoints[train2Path.Waypoints.Count-1]}");
                
                if (firstX < lastX)
                {
                    // Debug.Log("Train2: Reversing path to go right-to-left");
                    train2Path.Reverse();
                    // Debug.Log($"Train2 path AFTER: waypoint[0]={train2Path.Waypoints[0]}, waypoint[last]={train2Path.Waypoints[train2Path.Waypoints.Count-1]}");
                }
                
                train2.SetPath(train2Path, 0f);
                train2StartPos = train2Path.Waypoints[0];
                
                // Debug.Log($"Train2: Going RIGHT → LEFT, spawned at {train2.transform.position}, first waypoint={train2Path.Waypoints[0]}");
            }
        }*/
        
        // Debug.Log("=== TEST TRAIN MOVEMENT COMPLETE ===");
        // Debug.Log(">>> Toggle the switch to divert Train1 and avoid collision! <<<");
    }
}
