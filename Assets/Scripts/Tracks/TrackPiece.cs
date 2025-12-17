using System;
using System.Collections.Generic;
using UnityEngine;

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
    public class TrackPiece : MonoBehaviour
    {
        [Header("Track Configuration")]
        [Tooltip("Type of this track piece")]
        [SerializeField] private TrackType trackType = TrackType.Straight;

        [Tooltip("Waypoints along this track piece for trains to follow (in local space)")]
        [SerializeField] private List<Vector2> localWaypoints = new List<Vector2>();

        [Tooltip("Should waypoints be auto-generated based on connection points?")]
        [SerializeField] private bool autoGenerateWaypoints = true;

        [Tooltip("Number of waypoints to generate for curved tracks")]
        [SerializeField] private int curvedWaypointCount = 10;

        [Header("Visual Settings")]
        [Tooltip("Show waypoint gizmos in scene view")]
        [SerializeField] private bool showWaypointGizmos = true;

        [Tooltip("Color for waypoint gizmos")]
        [SerializeField] private Color waypointColor = Color.cyan;

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

            if (ConnectionPoints.Length == 0)
            {
                Debug.LogWarning($"TrackPiece {gameObject.name} has no connection points!", this);
            }

            // Auto-generate waypoints if needed
            if (autoGenerateWaypoints && localWaypoints.Count == 0)
            {
                GenerateWaypoints();
            }
        }

        private void OnEnable()
        {
            // Register with track manager
            if (TrackManager.Instance != null)
            {
                TrackManager.Instance.RegisterTrack(this);
            }
        }

        private void OnDisable()
        {
            // Unregister from track manager
            if (TrackManager.Instance != null)
            {
                TrackManager.Instance.UnregisterTrack(this);
            }
        }

        /// <summary>
        /// Generate waypoints based on connection points and track type
        /// </summary>
        public void GenerateWaypoints()
        {
            localWaypoints.Clear();
            cachedWorldWaypoints = null; // Invalidate cache

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
                    GenerateCurvedWaypoints(start, end);
                    break;

                case TrackType.Junction:
                    // For junctions, we'd need more complex logic
                    // For now, just create a straight path
                    GenerateStraightWaypoints(start, end);
                    break;
            }
        }

        private void GenerateStraightWaypoints(Vector2 start, Vector2 end)
        {
            localWaypoints.Add(start);
            localWaypoints.Add(end);
        }

        private void GenerateCurvedWaypoints(Vector2 start, Vector2 end)
        {
            // Create a 90-degree curve
            // Assume the curve goes from start to end with a circular arc
            
            // Find the center point of the curve
            // For a 90-degree curve, the center is equidistant from start and end
            Vector2 midpoint = (start + end) / 2f;
            Vector2 direction = (end - start).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            
            // The center is offset perpendicular to the line between start and end
            float radius = Vector2.Distance(start, end) / Mathf.Sqrt(2);
            Vector2 center = midpoint + perpendicular * (radius / Mathf.Sqrt(2));

            // Generate waypoints along the arc
            localWaypoints.Add(start);

            for (int i = 1; i < curvedWaypointCount - 1; i++)
            {
                float t = (float)i / (curvedWaypointCount - 1);
                float angle = t * 90f * Mathf.Deg2Rad;

                // Calculate point on the arc
                Vector2 startDir = (start - center).normalized;
                Vector2 endDir = (end - center).normalized;

                // Interpolate angle
                float startAngle = Mathf.Atan2(startDir.y, startDir.x);
                float endAngle = Mathf.Atan2(endDir.y, endDir.x);
                
                // Handle angle wrapping
                if (Mathf.Abs(endAngle - startAngle) > Mathf.PI)
                {
                    if (endAngle < startAngle)
                        endAngle += 2 * Mathf.PI;
                    else
                        startAngle += 2 * Mathf.PI;
                }

                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
                Vector2 point = center + new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * radius;

                localWaypoints.Add(point);
            }

            localWaypoints.Add(end);
        }

        /// <summary>
        /// Get the connected track pieces
        /// </summary>
        /// <returns>List of connected track pieces</returns>
        public List<TrackPiece> GetConnectedTracks()
        {
            List<TrackPiece> connected = new List<TrackPiece>();

            foreach (var connectionPoint in ConnectionPoints)
            {
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
                UnityEditor.Handles.Label(worldPoints[i], i.ToString());
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
