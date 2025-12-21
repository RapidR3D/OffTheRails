using System;
using System.Collections.Generic;
using System.Linq;
using OffTheRails.Trains;
using UnityEngine;

namespace OffTheRails.Tracks
{
    public class TrackManager : MonoBehaviour
    {
        public static TrackManager Instance { get; private set; }

        [SerializeField] private List<TrackPiece> registeredTracks = new List<TrackPiece>();
        [SerializeField] private List<TrackPath> paths = new List<TrackPath>();

        // ADD THIS: Lazy getter that works in edit mode
        public static TrackManager GetInstance()
        {
            if (Instance != null)
                return Instance;
            
            // In edit mode, find it
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return UnityEngine.Object.FindFirstObjectByType<TrackManager>();
            }
            #endif
            
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"Multiple TrackManagers detected! Destroying duplicate on {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            RefreshTracks();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Register a track piece with the manager
        /// </summary>
        public void RegisterTrack(TrackPiece track)
        {
            if (track == null)
            {
                //Debug.LogWarning("Attempted to register null track");
                return;
            }

            if (!registeredTracks.Contains(track))
            {
                registeredTracks.Add(track);
               // Debug.Log($"Registered track: {track.name}");
                
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                #endif
            }
        }

        /// <summary>
        /// Unregister a track piece from the manager
        /// </summary>
        public void UnregisterTrack(TrackPiece track)
        {
            if (track == null) return;

            if (registeredTracks.Remove(track))
            {
                // Disconnect all connection points
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected)
                    {
                        cp.Disconnect();
                    }
                }

                //Debug.Log($"Unregistered track: {track.name}");
                //RegenerateAllPaths();
                
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                #endif
            }
        }

        /// <summary>
        /// Get all registered tracks - WORKS IN EDIT MODE
        /// </summary>
        public IEnumerable<TrackPiece> GetAllTracks()
        {
            #if UNITY_EDITOR
            // In edit mode, always search fresh since registration might not work
            if (!Application.isPlaying)
            {
                return UnityEngine.Object.FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
            }
            #endif
            
            return registeredTracks;
        }

        /// <summary>
        /// Refresh the list of registered tracks - WORKS IN EDIT MODE
        /// </summary>
        public void RefreshTracks()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In edit mode, find all tracks in scene
                registeredTracks.Clear();
                var allTracks = UnityEngine.Object.FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
                registeredTracks.AddRange(allTracks);
               // Debug.Log($"Refreshed {registeredTracks.Count} tracks in edit mode");
                UnityEditor.EditorUtility.SetDirty(this);
                return;
            }
            #endif

            // Runtime: Clean up null references
            registeredTracks.RemoveAll(t => t == null);

            // Find any tracks that aren't registered
            var allTracks2 = FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
            foreach (var track in allTracks2)
            {
                if (!registeredTracks.Contains(track))
                {
                    registeredTracks.Add(track);
                }
            }

            //Debug.Log($"Refreshed tracks: {registeredTracks.Count} total");
        }

        /// <summary>
        /// Find all connected track pieces starting from the given track
        /// </summary>
        public HashSet<TrackPiece> FindConnectedTracks(TrackPiece startTrack)
        {
            HashSet<TrackPiece> connected = new HashSet<TrackPiece>();
            Queue<TrackPiece> toProcess = new Queue<TrackPiece>();

            toProcess.Enqueue(startTrack);
            connected.Add(startTrack);

            while (toProcess.Count > 0)
            {
                TrackPiece current = toProcess.Dequeue();

                foreach (var cp in current.ConnectionPoints)
                {
                    if (cp.IsConnected)
                    {
                        TrackPiece neighbor = cp.ConnectedTo.ParentTrack;
                        if (!connected.Contains(neighbor))
                        {
                            connected.Add(neighbor);
                            toProcess.Enqueue(neighbor);
                        }
                    }
                }
            }

            return connected;
        }

        /// <summary>
        /// Regenerate all track paths from registered track pieces
        /// Creates paths for all possible routes through the track network
        /// </summary>
        public void RegenerateAllPaths()
        {
            Debug.Log("[TrackManager] ═══ REGENERATING ALL PATHS ═══");
            
            // Store old paths for train updates
            Dictionary<string, TrackPath> oldPaths = new Dictionary<string, TrackPath>();
            foreach (var path in paths)
            {
                oldPaths[path.PathID] = path;
            }
            
            paths.Clear();

            // Find all endpoints (tracks with unconnected connection points)
            List<TrackPiece> endpoints = new List<TrackPiece>();
            
            foreach (var track in registeredTracks)
            {
                int connectedCount = 0;
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected)
                        connectedCount++;
                }
                
                // Endpoint = has fewer connections than total connection points
                if (connectedCount < track.ConnectionPoints.Length)
                {
                    endpoints.Add(track);
                    Debug.Log($"[RegenerateAllPaths] Endpoint found: {track.name} ({connectedCount}/{track.ConnectionPoints.Length} connections)");
                }
            }
            
            Debug.Log($"[RegenerateAllPaths] Found {endpoints.Count} endpoints");
            
            // For each pair of endpoints, find ALL possible paths between them
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    TrackPiece startPoint = endpoints[i];
                    TrackPiece endPoint = endpoints[j];
                    
                    Debug.Log($"[RegenerateAllPaths] Finding paths from {startPoint.name} to {endPoint.name}...");
                    
                    // Find all paths between these endpoints
                    List<List<TrackPiece>> allRoutes = FindAllPathsBetween(startPoint, endPoint);
                    
                    Debug.Log($"Found {allRoutes.Count} possible routes");
                    
                    // Create a TrackPath for each route
                    foreach (var route in allRoutes)
                    {
                        TrackPath newPath = new TrackPath();
                        newPath.BuildFromTrackList(route); 
                        
                        if (newPath.Waypoints.Count > 0)
                        {
                            paths.Add(newPath);
                            Debug.Log($"✓ Created path with {newPath.TrackPieces.Count} tracks, {newPath.Waypoints.Count} waypoints");
                        }
                    }
                }
            }
            
            Debug.Log($"[TrackManager] ═══ REGENERATION COMPLETE: {paths.Count} paths created ═══");

        #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        #endif

            // Notify all active trains to update their path references (Play Mode only)
            if (Application.isPlaying)
            {
                UpdateTrainsWithNewPaths(oldPaths);
            }
        }

        /// <summary>
        /// Find ALL possible paths between two track pieces using DFS
        /// This will find multiple routes if there are junctions
        /// </summary>
        private List<List<TrackPiece>> FindAllPathsBetween(TrackPiece start, TrackPiece end)
        {
            List<List<TrackPiece>> allPaths = new List<List<TrackPiece>>();
            List<TrackPiece> currentPath = new List<TrackPiece>();
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            
            FindPathsRecursive(start, end, currentPath, visited, allPaths);
            
            return allPaths;
        }

        /// <summary>
        /// Recursive DFS to find all paths
        /// </summary>
        private void FindPathsRecursive(TrackPiece current, TrackPiece target, List<TrackPiece> currentPath, HashSet<TrackPiece> visited, List<List<TrackPiece>> allPaths, int depth = 0)
        {
            string indent = new string(' ', depth * 2);
    
            // Add current to path
            currentPath.Add(current);
            visited.Add(current);
    
            Debug.Log($"{indent}[DFS depth={depth}] At '{current.name}', path length={currentPath.Count}");
    
            // Check if we reached the target
            if (current == target)
            {
                // Found a complete path!  Save a copy
                Debug.Log($"{indent}✓ FOUND COMPLETE PATH with {currentPath.Count} tracks!");
                allPaths.Add(new List<TrackPiece>(currentPath));
            }
            else
            {
                // Explore all connected tracks (respecting current switch states)
                var connections = current.GetConnectedTracks(respectSwitchState: false);
        
                Debug.Log($"{indent} '{current.name}' has {connections.Count} connections");
        
                foreach (var next in connections)
                {
                    if (!visited.Contains(next))
                    {
                        Debug.Log($"{indent} → Exploring '{next.name}'");
                        FindPathsRecursive(next, target, currentPath, visited, allPaths,depth + 1);
                    }
                    else
                    {
                        Debug.Log($"{indent} → Skipping '{next.name}' (already visited)");
                    }
                }
        
                if (connections.Count == 0)
                {
                    Debug.LogWarning($"{indent} ⚠️ DEAD END at '{current.name}'!");
                }
            }
    
            // Backtrack
            Debug.Log($"{indent}[DFS depth={depth}] Backtracking from '{current.name}'");
            currentPath.RemoveAt(currentPath.Count - 1);
            visited.Remove(current);
        }
        
        private void UpdateTrainsWithNewPaths(Dictionary<string, TrackPath> oldPaths)
        {
            // Find all active trains in the scene
            var trains = FindObjectsOfType<Train>();
    
            foreach (var train in trains)
            {
                if (!train.IsActive)
                    continue;
            
                // Get the train's current path
                var currentPath = train.GetCurrentPath(); // You'll need to add this getter
                if (currentPath == null)
                    continue;
            
                // Find the regenerated version of this path
                TrackPath newPath = null;
                foreach (var path in paths)
                {
                    // Match by first track piece (simple heuristic)
                    if (path.TrackPieces.Count > 0 && 
                        currentPath.TrackPieces.Count > 0 &&
                        path.TrackPieces[0] == currentPath.TrackPieces[0])
                    {
                        newPath = path;
                        break;
                    }
                }
        
                if (newPath != null)
                {
                    Debug.Log($"Updating train {train.name} to use regenerated path");
                    train.UpdatePath(newPath); 
                }
            }
        }
        /// <summary>
        /// Get all track paths
        /// </summary>
        public IReadOnlyList<TrackPath> GetPaths()
        {
            return paths.AsReadOnly();
        }

        /// <summary>
        /// Find the path that contains the given track
        /// </summary>
        public TrackPath FindPathContaining(TrackPiece track)
        {
            return paths.FirstOrDefault(p => p.TrackPieces.Contains(track));
        }
        
        /// <summary>
        /// EDITOR/DEBUG: Force check all tracks and connect nearby connection points
        /// </summary>
        [ContextMenu("Force Connect Nearby Tracks")]
        public void ForceConnectNearbyTracks()
        {
            int connectionsFound = 0;
            
            foreach (var track in GetAllTracks())
            {
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected)
                        continue;
                        
                    // Find nearest unconnected point
                    foreach (var otherTrack in GetAllTracks())
                    {
                        if (otherTrack == track)
                            continue;
                            
                        foreach (var otherCp in otherTrack.ConnectionPoints)
                        {
                            if (otherCp.IsConnected)
                                continue;
                                
                            float distance = Vector2.Distance(cp.WorldPosition, otherCp.WorldPosition);
                            
                            // If very close (within 0.5 units), connect them
                            if (distance < 0.5f)
                            {
                                // Check if directions are roughly opposite
                                float dot = Vector2.Dot(cp.WorldDirection, otherCp.WorldDirection);
                                
                                if (dot < -0.7f) // Roughly opposite
                                {
                                    cp.ConnectTo(otherCp);
                                    connectionsFound++;
                                    Debug.Log($"✓ Force connected {track.name} to {otherTrack.name} (distance: {distance:F3}, dot: {dot:F3})");
                                }
                                else
                                {
                                    Debug.LogWarning($"✗ Close but wrong direction: {track.name} to {otherTrack.name} (dot: {dot:F3})");
                                }
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"=== Force connection complete: {connectionsFound} connections made ===");
            
            if (connectionsFound > 0)
            {
                RegenerateAllPaths();
            }
        }
    }
}