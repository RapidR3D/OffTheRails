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

        /// <summary>
        /// Event fired when paths are rebuilt (switches toggled)
        /// </summary>
        public event System.Action OnPathsRebuilt;

        public static TrackManager GetInstance()
        {
            if (Instance != null) return Instance;
            
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
                // Debug.LogWarning($"Multiple TrackManagers! Destroying duplicate on {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            RefreshTracks();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterTrack(TrackPiece track)
        {
            if (track == null || registeredTracks.Contains(track)) return;
            registeredTracks.Add(track);
            
            #if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public void UnregisterTrack(TrackPiece track)
        {
            if (track == null) return;
            if (registeredTracks.Remove(track))
            {
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected) cp.Disconnect();
                }
                #if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }

        public IEnumerable<TrackPiece> GetAllTracks()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return UnityEngine.Object.FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
            }
            #endif
            return registeredTracks;
        }

        public void RefreshTracks()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                registeredTracks.Clear();
                registeredTracks.AddRange(UnityEngine.Object.FindObjectsByType<TrackPiece>(FindObjectsSortMode.None));
                UnityEditor.EditorUtility.SetDirty(this);
                return;
            }
            #endif

            registeredTracks.RemoveAll(t => t == null);
            var allTracks = FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
            foreach (var track in allTracks)
            {
                if (!registeredTracks.Contains(track))
                    registeredTracks.Add(track);
            }
        }

        /// <summary>
        /// Generate paths between all endpoint pairs.
        /// Paths respect current switch states.
        /// </summary>
        public void RegenerateAllPaths()
        {
            // Debug.Log("[TrackManager] ═══ REGENERATING ALL PATHS ═══");
            paths.Clear();

            // Find endpoints (tracks with unconnected connection points)
            List<TrackPiece> endpoints = FindEndpoints();
            // Debug.Log($"[TrackManager] Found {endpoints.Count} endpoints");

            if (endpoints.Count < 2)
            {
                // Debug.LogWarning("[TrackManager] Need at least 2 endpoints");
                return;
            }

            // Create ONE path between each pair of endpoints
            // The path will follow current switch states
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    TrackPath path = new TrackPath();
                    path.BuildFromEndpoints(endpoints[i], endpoints[j]);
                    
                    if (path.TrackPieces.Count > 0 && path.Waypoints.Count > 0)
                    {
                        paths.Add(path);
                        // Debug.Log($"✓ Created path: {endpoints[i].name} → {endpoints[j].name} ({path.TrackPieces.Count} tracks, {path.Waypoints.Count} waypoints)");
                    }
                }
            }

            // Debug.Log($"[TrackManager] ═══ COMPLETE: {paths.Count} paths ═══");

            #if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
            #endif

            OnPathsRebuilt?.Invoke();
        }

        /// <summary>
        /// Rebuild all existing paths to reflect current switch states.
        /// Called when a switch is toggled.
        /// </summary>
        public void RebuildAllPaths()
        {
            // Debug.Log("[TrackManager] ═══ REBUILDING PATHS FOR SWITCH CHANGE ═══");

            foreach (var path in paths)
            {
                path.RebuildPath();
            }

            // Debug.Log($"[TrackManager] ═══ REBUILD COMPLETE ═══");

            OnPathsRebuilt?.Invoke();
        }

        /// <summary>
        /// Find all endpoint track pieces (tracks with at least one unconnected connection point)
        /// </summary>
        private List<TrackPiece> FindEndpoints()
        {
            List<TrackPiece> endpoints = new List<TrackPiece>();
            
            foreach (var track in registeredTracks)
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
                    endpoints.Add(track);
                }
            }
            
            return endpoints;
        }

        public IReadOnlyList<TrackPath> GetPaths()
        {
            return paths.AsReadOnly();
        }

        /// <summary>
        /// Get the path that starts nearest to the given position.
        /// Useful for assigning trains to paths.
        /// </summary>
        public TrackPath GetPathNearestTo(Vector2 position)
        {
            TrackPath nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var path in paths)
            {
                if (path.Waypoints.Count == 0) continue;
                
                // Check distance to path start
                float dist = Vector2.Distance(position, path.Waypoints[0]);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = path;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get a path that starts from a specific track piece
        /// </summary>
        public TrackPath GetPathStartingFrom(TrackPiece startTrack)
        {
            foreach (var path in paths)
            {
                if (path.StartTrack == startTrack)
                    return path;
            }
            return null;
        }

        public TrackPath FindPathContaining(TrackPiece track)
        {
            return paths.FirstOrDefault(p => p.ContainsTrack(track));
        }

        [ContextMenu("Force Connect Nearby Tracks")]
        public void ForceConnectNearbyTracks()
        {
            int connectionsFound = 0;
            
            foreach (var track in GetAllTracks())
            {
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected) continue;
                        
                    foreach (var otherTrack in GetAllTracks())
                    {
                        if (otherTrack == track) continue;
                            
                        foreach (var otherCp in otherTrack.ConnectionPoints)
                        {
                            if (otherCp.IsConnected) continue;
                                
                            float distance = Vector2.Distance(cp.WorldPosition, otherCp.WorldPosition);
                            
                            if (distance < 0.5f)
                            {
                                float dot = Vector2.Dot(cp.WorldDirection, otherCp.WorldDirection);
                                
                                if (dot < -0.7f)
                                {
                                    cp.ConnectTo(otherCp);
                                    connectionsFound++;
                                    // Debug.Log($"✓ Connected {track.name} to {otherTrack.name}");
                                }
                            }
                        }
                    }
                }
            }
            
            // Debug.Log($"=== Force connection complete: {connectionsFound} ===");
            
            if (connectionsFound > 0)
            {
                RegenerateAllPaths();
            }
        }
    }
}
