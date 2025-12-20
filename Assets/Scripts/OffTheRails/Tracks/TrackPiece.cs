using System;
using System.Collections.Generic;
using UnityEngine;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Type of track piece
    /// </summary>
    public enum TrackType
    {
        Straight,
        Curved,
        Junction
    }

    /// <summary>
    /// Base component for all track segments.
    /// Stores connection points, track type, and path waypoints.
    /// </summary>
    [ExecuteAlways]
    public class TrackPiece : MonoBehaviour
    {
        [Header("Track Configuration")] [Tooltip("Type of this track piece")] [SerializeField]
        private TrackType trackType = TrackType.Straight;

        [Tooltip("Waypoints along this track piece for trains to follow (in local space)")] [SerializeField]
        private List<Vector2> localWaypoints = new List<Vector2>();

        [Tooltip("Should waypoints be auto-generated based on connection points?")] [SerializeField]
        private bool autoGenerateWaypoints = true;

        [Tooltip("Number of waypoints to generate for curved tracks")] [SerializeField]
        private int curvedWaypointCount = 10;

        [Tooltip("Control point strength for bezier curves (0-1 relative to distance)")] [SerializeField]
        private float bezierControlStrength = 0.33f;

        [Header("Visual Settings")] [Tooltip("Show waypoint gizmos in scene view")] [SerializeField]
        private bool showWaypointGizmos = true;

        [Tooltip("Color for waypoint gizmos")] [SerializeField]
        private Color waypointColor = Color.cyan;

        /// <summary>
        /// Unique identifier for this track piece
        /// </summary>
        public string TrackID { get; private set; }

        /// <summary>
        /// Type of this track piece
        /// </summary>
        public TrackType Type => trackType;

        /// <summary>
        /// All connection points on this track piece
        /// </summary>
        public ConnectionPoint[] ConnectionPoints { get; private set; }

        /// <summary>
        /// Optional switch component for junctions
        /// </summary>
        public TrackSwitch TrackSwitch { get; private set; }

        // Cached world waypoints
        private Vector2[] cachedWorldWaypoints = null;
        private Vector3 lastCachedPosition;
        private Quaternion lastCachedRotation;

        /// <summary>
        /// Waypoints in world space for trains to follow
        /// </summary>
        public Vector2[] WorldWaypoints
        {
            get
            {
                // Recalculate if transform changed or cache is invalid
                if (cachedWorldWaypoints == null ||
                    cachedWorldWaypoints.Length != localWaypoints.Count ||
                    transform.position != lastCachedPosition ||
                    transform.rotation != lastCachedRotation)
                {
                    cachedWorldWaypoints = new Vector2[localWaypoints.Count];
                    for (int i = 0; i < localWaypoints.Count; i++)
                    {
                        cachedWorldWaypoints[i] = transform.TransformPoint(localWaypoints[i]);
                    }

                    lastCachedPosition = transform.position;
                    lastCachedRotation = transform.rotation;
                }

                return cachedWorldWaypoints;
            }
        }

        /// <summary>
        /// Waypoints in local space
        /// </summary>
        public List<Vector2> LocalWaypoints => new List<Vector2>(localWaypoints);

        /// <summary>
        /// Force invalidation of the waypoint cache (useful when switch state changes)
        /// </summary>
        public void InvalidateWaypointCache()
        {
            cachedWorldWaypoints = null;
        }

        /// <summary>
        /// Length of the track piece
        /// </summary>
        public float Length
        {
            get
            {
                float length = 0f;
                Vector2[] worldPoints = WorldWaypoints;
                for (int i = 1; i < worldPoints.Length; i++)
                {
                    length += Vector2.Distance(worldPoints[i - 1], worldPoints[i]);
                }

                return length;
            }
        }

        private void Awake()
        {
            // Generate unique ID
            TrackID = Guid.NewGuid().ToString();

            // Find all connection points
            ConnectionPoints = GetComponentsInChildren<ConnectionPoint>();
            
            // Find switch component (may be on a child GameObject)
            TrackSwitch = GetComponentInChildren<TrackSwitch>();

            if (ConnectionPoints.Length == 0)
            {
                Debug.LogWarning($"TrackPiece {gameObject.name} has no connection points!", this);
            }

            // Auto-generate waypoints if needed
            if (autoGenerateWaypoints)
            {
                GenerateWaypoints();
            }
        }

        private void OnEnable()
        {
            // Register with track manager
            var trackManager = TrackManager.Instance;
            if (trackManager != null)
            {
                trackManager.RegisterTrack(this);
            }
        }

        private void OnDisable()
            {
                // Unregister from track manager
                if (this == null || gameObject == null) return;
    
                var trackManager = TrackManager.Instance;
                if (trackManager != null)
                {
                    trackManager.UnregisterTrack(this);
                }
    
                if (!Application.isPlaying && gameObject != null)
                {
                    Debug.Log($"Disabling {gameObject.name} ({TrackID})");
                }
            }

        private void OnValidate()
        {
            if (autoGenerateWaypoints && ConnectionPoints != null && ConnectionPoints.Length >= 2)
            {
                GenerateWaypoints();
            }
        }

        /// <summary>
        /// Generate waypoints based on connection points and track type
        /// </summary>
        public void GenerateWaypoints()
        {
            localWaypoints.Clear();
            cachedWorldWaypoints = null; // Invalidate cache - IMPORTANT for switch changes!

            if (ConnectionPoints.Length < 2)
            {
                Debug.LogWarning($"Cannot generate waypoints for {gameObject.name}: need at least 2 connection points");
                return;
            }

            Vector2 start = transform.InverseTransformPoint(ConnectionPoints[0].transform.position);
            Vector2 end = transform.InverseTransformPoint(ConnectionPoints[1].transform.position);

            switch (trackType)
            {
                case TrackType.Straight:
                    GenerateStraightWaypoints(start, end);
                    break;

                case TrackType.Curved:
                    // Use Bezier for better curve handling
                    GenerateBezierWaypoints(ConnectionPoints[0], ConnectionPoints[1]);
                    break;

                case TrackType.Junction:
                    GenerateJunctionWaypoints();
                    break;
            }
            
            Debug.Log($"Generated {localWaypoints.Count} waypoints for {gameObject.name} (Switch state: {(TrackSwitch != null && TrackSwitch.IsDiverging ? "DIVERGING" : "STRAIGHT")})");
        }

        private void GenerateStraightWaypoints(Vector2 start, Vector2 end)
        {
            localWaypoints.Add(start);
            localWaypoints.Add(end);
        }

        private void GenerateJunctionWaypoints()
        {
            if (ConnectionPoints.Length < 3)
            {
                Debug.LogWarning("Junction needs at least 3 connection points");
                return;
            }

            // Log all connection points for debugging
            Debug.Log($"=== Junction {gameObject.name} Connection Points ===");
            for (int i = 0; i < ConnectionPoints.Length; i++)
            {
                Debug.Log($"  ConnectionPoints[{i}] = '{ConnectionPoints[i].name}' at position {ConnectionPoints[i].WorldPosition}");
            }

            // Y-Track Connection Point Layout:
            // ConnectionPoints[0] = First connection point (usually the common/input)
            // ConnectionPoints[1] = Second connection point (straight path)
            // ConnectionPoints[2] = Third connection point (diverging path)
            
            ConnectionPoint startPoint = ConnectionPoints[0]; // First connection point
            ConnectionPoint endPoint = ConnectionPoints[1];   // Default to second (Straight)

            bool usingDivergingPath = TrackSwitch != null && TrackSwitch.IsDiverging;
            
            Debug.Log($"Switch detection: TrackSwitch={TrackSwitch?.name ?? "null"}, IsDiverging={TrackSwitch?.IsDiverging ?? false}, usingDivergingPath={usingDivergingPath}");
            
            if (usingDivergingPath)
            {
                endPoint = ConnectionPoints[2]; // Switch to third (Diverging)
            }

            Debug.Log($"→ Generating JUNCTION path: '{startPoint.name}' → '{endPoint.name}' (Diverging={usingDivergingPath})");
            
            GenerateBezierWaypoints(startPoint, endPoint);
        }

        private void GenerateBezierWaypoints(ConnectionPoint startCP, ConnectionPoint endCP)
        {
            Vector2 start = transform.InverseTransformPoint(startCP.transform.position);
            Vector2 end = transform.InverseTransformPoint(endCP.transform. position);
    
            // Get directions in local space
            // For connection points: they should point in the direction a train would ENTER/EXIT
            Vector2 startDir = transform.InverseTransformDirection(startCP.WorldDirection);
            Vector2 endDir = transform.InverseTransformDirection(endCP.WorldDirection);

            float distance = Vector2.Distance(start, end);
            float controlLength = distance * Mathf.Abs(bezierControlStrength); // Use absolute value

            Vector2 p0 = start;
            Vector2 p3 = end;
            
            // Control points should extend from connection points in the direction of the curve
            Vector2 p1 = p0 + startDir * controlLength; // the + is the correct operation here, subtracting generating the bezier curve outside the confines of the track
            Vector2 p2 = p3 + endDir * controlLength;

            localWaypoints.Add(p0);

            for (int i = 1; i < curvedWaypointCount - 1; i++)
            {
                float t = (float)i / (curvedWaypointCount - 1);
                Vector2 point = CalculateCubicBezier(t, p0, p1, p2, p3);
                localWaypoints.Add(point);
            }

            localWaypoints.Add(p3);
        }

        private Vector2 CalculateCubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        /// <summary>
        /// Get the connected track pieces, respecting switch state
        /// </summary>
        /// <returns>List of connected track pieces</returns>
        public List<TrackPiece> GetConnectedTracks()
        {
            List<TrackPiece> connected = new List<TrackPiece>();

            for (int i = 0; i < ConnectionPoints.Length; i++)
            {
                // Skip inactive connection points for junctions
                if (trackType == TrackType.Junction && TrackSwitch != null)
                {
                    // Assume 0 is common, 1 is Green, 2 is Yellow
                    // If Diverging (Yellow), skip 1 (Green)
                    // If Straight (Green), skip 2 (Yellow)
                    if (TrackSwitch.IsDiverging && i == 1) continue;
                    if (!TrackSwitch.IsDiverging && i == 2) continue;
                }

                var connectionPoint = ConnectionPoints[i];
                if (connectionPoint.IsConnected && connectionPoint.ConnectedTo != null)
                {
                    TrackPiece connectedTrack = connectionPoint.ConnectedTo.ParentTrack;
                    if (connectedTrack != null && !connected.Contains(connectedTrack))
                    {
                        connected.Add(connectedTrack);
                    }
                }
            }

            return connected;
        }

        /// <summary>
        /// Check if this track is connected to any other tracks
        /// </summary>
        /// <returns>True if connected to at least one other track</returns>
        public bool HasConnections()
        {
            foreach (var point in ConnectionPoints)
            {
                if (point.IsConnected)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get a connection point by index
        /// </summary>
        /// <param name="index">Index of the connection point</param>
        /// <returns>The connection point, or null if index is invalid</returns>
        public ConnectionPoint GetConnectionPoint(int index)
        {
            if (index >= 0 && index < ConnectionPoints.Length)
                return ConnectionPoints[index];
            return null;
        }

        private void OnDrawGizmos()
        {
            if (!showWaypointGizmos || localWaypoints.Count == 0)
                return;

            // Draw waypoints
            Gizmos.color = waypointColor;
            Vector2[] worldPoints = WorldWaypoints;

            // Draw waypoint spheres
            foreach (var point in worldPoints)
            {
                Gizmos.DrawSphere(point, 0.1f);
            }

            // Draw lines between waypoints
            for (int i = 1; i < worldPoints.Length; i++)
            {
                Gizmos.DrawLine(worldPoints[i - 1], worldPoints[i]);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (localWaypoints.Count == 0)
                return;

            // Draw enhanced waypoint visualization when selected
            Gizmos.color = Color.yellow;
            Vector2[] worldPoints = WorldWaypoints;

            for (int i = 0; i < worldPoints.Length; i++)
            {
                Gizmos.DrawWireSphere(worldPoints[i], 0.15f);
                
                // Draw waypoint numbers
                // UnityEditor.Handles.Label(worldPoints[i], i.ToString());
            }
        }

        [UnityEditor.MenuItem("GameObject/Off The Rails/Track Piece", false, 10)]
        private static void CreateTrackPiece()
        {
            GameObject trackPiece = new GameObject("Track Piece");
            trackPiece.AddComponent<TrackPiece>();
            
            // Create two connection points
            GameObject cp1 = new GameObject("Connection Point 1");
            cp1.transform.SetParent(trackPiece.transform);
            cp1.transform.localPosition = new Vector3(-0.5f, 0, 0);
            var connection1 = cp1.AddComponent<ConnectionPoint>();

            GameObject cp2 = new GameObject("Connection Point 2");
            cp2.transform.SetParent(trackPiece.transform);
            cp2.transform.localPosition = new Vector3(0.5f, 0, 0);
            var connection2 = cp2.AddComponent<ConnectionPoint>();

            UnityEditor.Selection.activeGameObject = trackPiece;
        }
#endif
    }
}