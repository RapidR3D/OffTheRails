using System.Collections.Generic;
using UnityEngine;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Singleton manager that handles all track pieces and their connections.
    /// Manages snapping logic and path generation.
    /// </summary>
    [ExecuteAlways]
    public class TrackManager : MonoBehaviour
    {
        private static TrackManager instance;
        private static bool isQuitting = false;

        /// <summary>
        /// Singleton instance of the TrackManager
        /// </summary>
        public static TrackManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    return null;
                }

                if (instance == null)
                {
                    instance = FindFirstObjectByType<TrackManager>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("TrackManager");
                        instance = go.AddComponent<TrackManager>();
                        Debug.Log("TrackManager instance created automatically");
                    }
                }
                return instance;
            }
        }

        [Header("Snapping Settings")]
        [Tooltip("Default snap radius for track connections")]
        [SerializeField] private float defaultSnapRadius = 0.5f;

        [Tooltip("Automatically generate paths when tracks connect")]
        [SerializeField] private bool autoGeneratePaths = true;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLogs = true;

        [Tooltip("Show path gizmos in scene view")]
        [SerializeField] private bool showPathGizmos = true;

        [Tooltip("Color for path gizmos")]
        [SerializeField] private Color pathGizmoColor = Color.magenta;

        /// <summary>
        /// All registered track pieces
        /// </summary>
        private List<TrackPiece> allTracks = new List<TrackPiece>();

        /// <summary>
        /// All generated paths
        /// </summary>
        private List<TrackPath> allPaths = new List<TrackPath>();

        /// <summary>
        /// Default snap radius
        /// </summary>
        public float DefaultSnapRadius => defaultSnapRadius;

        /// <summary>
        /// Check if an instance of TrackManager exists
        /// </summary>
        public static bool HasInstance
        {
            get
            {
                if (isQuitting) return false;
                return instance != null;
            }
        }

        private void Awake()
        {
            // Enforce singleton pattern
            if (instance != null && instance != this)
            {
                // In Edit Mode, we might have duplicates temporarily, but we should clean up
                if (Application.isPlaying)
                {
                    Debug.LogWarning("Multiple TrackManager instances detected. Destroying duplicate.");
                    Destroy(gameObject);
                    return;
                }
            }

            instance = this;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                RefreshTracks();
                RebuildConnections();
                RegenerateAllPaths();
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        /// <summary>
        /// Register a track piece with the manager
        /// </summary>
        /// <param name="track">The track piece to register</param>
        public void RegisterTrack(TrackPiece track)
        {
            if (track == null)
                return;

            if (!allTracks.Contains(track))
            {
                allTracks.Add(track);
                
                if (enableDebugLogs)
                    Debug.Log($"Registered track: {track.gameObject.name}");
            }
        }

        /// <summary>
        /// Unregister a track piece from the manager
        /// </summary>
        /// <param name="track">The track piece to unregister</param>
        public void UnregisterTrack(TrackPiece track)
        {
            if (track == null)
                return;

            if (allTracks.Contains(track))
            {
                // Disconnect all connection points
                foreach (var connectionPoint in track.ConnectionPoints)
                {
                    connectionPoint.Disconnect();
                }

                allTracks.Remove(track);
                
                // Regenerate paths
                if (autoGeneratePaths)
                {
                    RegenerateAllPaths();
                }

                if (enableDebugLogs)
                    Debug.Log($"Unregistered track: {track.gameObject.name}");
            }
        }

        /// <summary>
        /// Get all registered track pieces
        /// </summary>
        /// <returns>List of all track pieces</returns>
        public List<TrackPiece> GetAllTracks()
        {
            return new List<TrackPiece>(allTracks);
        }

        /// <summary>
        /// Refresh the list of tracks from the scene (useful for Editor tools)
        /// </summary>
        public void RefreshTracks()
        {
            allTracks.Clear();
            TrackPiece[] tracks = FindObjectsByType<TrackPiece>(FindObjectsSortMode.None);
            foreach (var track in tracks)
            {
                RegisterTrack(track);
            }
        }

        /// <summary>
        /// Rebuild connections between tracks based on proximity
        /// </summary>
        public void RebuildConnections()
        {
            foreach (var track in allTracks)
            {
                foreach (var point in track.ConnectionPoints)
                {
                    if (point.IsConnected)
                        continue;

                    // Find matching connection
                    foreach (var otherTrack in allTracks)
                    {
                        if (otherTrack == track) continue;

                        foreach (var otherPoint in otherTrack.ConnectionPoints)
                        {
                            if (otherPoint.IsConnected) continue;

                            // Check if they are effectively in the same position
                            if (Vector2.Distance(point.WorldPosition, otherPoint.WorldPosition) < 0.01f)
                            {
                                // Check alignment
                                float dot = Vector2.Dot(point.WorldDirection, otherPoint.WorldDirection);
                                if (dot <= -0.9f) // Roughly opposite
                                {
                                    point.ConnectTo(otherPoint);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all generated paths
        /// </summary>
        /// <returns>List of all paths</returns>
        public List<TrackPath> GetAllPaths()
        {
            return new List<TrackPath>(allPaths);
        }

        /// <summary>
        /// Attempt to snap all tracks in the scene
        /// </summary>
        public void SnapAllTracks()
        {
            RefreshTracks();
            int snapCount = 0;
            foreach (var track in allTracks)
            {
                if (TrySnapTrack(track))
                {
                    snapCount++;
                }
            }
            
            if (snapCount > 0)
            {
                Debug.Log($"Snapped {snapCount} tracks.");
                RegenerateAllPaths();
            }
        }

        /// <summary>
        /// Attempt to snap a track piece to nearby connection points
        /// </summary>
        /// <param name="track">The track piece to snap</param>
        /// <returns>True if snapping occurred</returns>
        public bool TrySnapTrack(TrackPiece track)
        {
            if (track == null)
                return false;

            bool snapped = false;

            // Try to snap each connection point
            foreach (var connectionPoint in track.ConnectionPoints)
            {
                if (connectionPoint.IsConnected)
                    continue;

                // Find nearest valid connection
                ConnectionPoint nearest = connectionPoint.FindNearestValidConnection();

                if (nearest != null)
                {
                    // Calculate alignment
                    if (connectionPoint.CalculateAlignmentTo(nearest, out Vector3 position, out Quaternion rotation))
                    {
                        // Apply alignment
                        track.transform.position = position;
                        track.transform.rotation = rotation;

                        // Connect the points
                        connectionPoint.ConnectTo(nearest);
                        snapped = true;

                        if (enableDebugLogs)
                            Debug.Log($"Snapped {track.gameObject.name} to {nearest.ParentTrack.gameObject.name}");

                        break; // Only snap one connection at a time
                    }
                }
            }

            // Regenerate paths if we snapped
            if (snapped && autoGeneratePaths)
            {
                RegenerateAllPaths();
            }

            return snapped;
        }

        /// <summary>
        /// Find the nearest valid connection point to a given position
        /// </summary>
        /// <param name="position">The position to check from</param>
        /// <param name="excludeTrack">Track to exclude from search (optional)</param>
        /// <returns>The nearest valid connection point, or null if none found</returns>
        public ConnectionPoint FindNearestConnectionPoint(Vector2 position, TrackPiece excludeTrack = null)
        {
            ConnectionPoint nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var track in allTracks)
            {
                if (track == excludeTrack)
                    continue;

                foreach (var point in track.ConnectionPoints)
                {
                    if (point.IsConnected)
                        continue;

                    float distance = Vector2.Distance(position, point.WorldPosition);
                    
                    if (distance < nearestDistance && distance <= defaultSnapRadius)
                    {
                        nearest = point;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Connect two connection points
        /// </summary>
        /// <param name="point1">First connection point</param>
        /// <param name="point2">Second connection point</param>
        /// <returns>True if connection was successful</returns>
        public bool ConnectPoints(ConnectionPoint point1, ConnectionPoint point2)
        {
            if (point1 == null || point2 == null)
                return false;

            if (!point1.CanConnectTo(point2))
                return false;

            point1.ConnectTo(point2);

            // Regenerate paths
            if (autoGeneratePaths)
            {
                RegenerateAllPaths();
            }

            return true;
        }

        /// <summary>
        /// Disconnect two connection points
        /// </summary>
        /// <param name="point">The connection point to disconnect</param>
        public void DisconnectPoint(ConnectionPoint point)
        {
            if (point == null)
                return;

            point.Disconnect();

            // Regenerate paths
            if (autoGeneratePaths)
            {
                RegenerateAllPaths();
            }
        }

        /// <summary>
        /// Generate all paths from connected tracks
        /// </summary>
        public void RegenerateAllPaths()
        {
            allPaths.Clear();

            HashSet<TrackPiece> processed = new HashSet<TrackPiece>();

            // Build paths from connected track networks
            foreach (var track in allTracks)
            {
                if (processed.Contains(track))
                    continue;

                // Only create paths for tracks that have connections
                if (!track.HasConnections())
                {
                    processed.Add(track);
                    continue;
                }

                // Create a new path starting from this track
                TrackPath path = new TrackPath();
                path.BuildFromTrack(track);

                // Mark all tracks in this path as processed
                foreach (var trackInPath in path.TrackPieces)
                {
                    processed.Add(trackInPath);
                }

                allPaths.Add(path);

                if (enableDebugLogs)
                    Debug.Log($"Generated path with {path.TrackPieces.Count} tracks and {path.Waypoints.Count} waypoints");
            }
        }

        /// <summary>
        /// Find the path that contains a specific track piece
        /// </summary>
        /// <param name="track">The track piece to search for</param>
        /// <returns>The path containing the track, or null if not found</returns>
        public TrackPath FindPathContainingTrack(TrackPiece track)
        {
            foreach (var path in allPaths)
            {
                if (path.ContainsTrack(track))
                    return path;
            }
            return null;
        }

        /// <summary>
        /// Get all connection points that are available (not connected)
        /// </summary>
        /// <returns>List of available connection points</returns>
        public List<ConnectionPoint> GetAvailableConnectionPoints()
        {
            List<ConnectionPoint> available = new List<ConnectionPoint>();

            foreach (var track in allTracks)
            {
                foreach (var point in track.ConnectionPoints)
                {
                    if (!point.IsConnected)
                    {
                        available.Add(point);
                    }
                }
            }

            return available;
        }

        private void OnDrawGizmos()
        {
            if (!showPathGizmos || allPaths == null)
                return;

            // Draw all paths
            Gizmos.color = pathGizmoColor;

            foreach (var path in allPaths)
            {
                if (path.Waypoints.Count < 2)
                    continue;

                // Draw path waypoints
                for (int i = 1; i < path.Waypoints.Count; i++)
                {
                    Gizmos.DrawLine(path.Waypoints[i - 1], path.Waypoints[i]);
                }

                // Draw waypoint spheres
                foreach (var waypoint in path.Waypoints)
                {
                    Gizmos.DrawSphere(waypoint, 0.05f);
                }
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/Off The Rails/Track Manager", false, 0)]
        private static void CreateTrackManager()
        {
            // Check if one already exists
            TrackManager existing = FindFirstObjectByType<TrackManager>();
            if (existing != null)
            {
                UnityEditor.Selection.activeGameObject = existing.gameObject;
                Debug.Log("TrackManager already exists in scene");
                return;
            }

            GameObject managerObject = new GameObject("TrackManager");
            managerObject.AddComponent<TrackManager>();
            UnityEditor.Selection.activeGameObject = managerObject;
            Debug.Log("Created TrackManager in scene");
        }
#endif
    }
}