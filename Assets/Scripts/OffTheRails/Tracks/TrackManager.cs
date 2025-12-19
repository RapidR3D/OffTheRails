using System;
using System.Collections.Generic;
using System.Linq;
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
                Debug.LogWarning("Attempted to register null track");
                return;
            }

            if (!registeredTracks.Contains(track))
            {
                registeredTracks.Add(track);
                Debug.Log($"Registered track: {track.name}");
                
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

                Debug.Log($"Unregistered track: {track.name}");
                RegenerateAllPaths();
                
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
                Debug.Log($"Refreshed {registeredTracks.Count} tracks in edit mode");
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

            Debug.Log($"Refreshed tracks: {registeredTracks.Count} total");
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
        /// Regenerate all track paths
        /// </summary>
        public void RegenerateAllPaths()
        {
            paths.Clear();

            HashSet<TrackPiece> processedTracks = new HashSet<TrackPiece>();

            foreach (var track in GetAllTracks())
            {
                if (track == null || processedTracks.Contains(track))
                    continue;

                // Find all connected tracks
                HashSet<TrackPiece> connectedGroup = FindConnectedTracks(track);

                // Mark all as processed
                foreach (var t in connectedGroup)
                {
                    processedTracks.Add(t);
                }

                // Build path from this group
                TrackPath path = new TrackPath();
                path.BuildFromTrack(track);
                paths.Add(path);
            }

            Debug.Log($"Generated {paths.Count} path(s) with {processedTracks.Count} tracks total");
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
            #endif
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
    }
}