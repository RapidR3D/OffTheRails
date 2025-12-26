using System;
using System.Collections.Generic;
using UnityEngine;

namespace OffTheRails.Tracks
{
    public enum TrackType
    {
        Straight,
        Curved,
        Junction
    }

    /// <summary>
    /// Base component for all track segments.
    /// For Y-junctions (TrackType.Junction):
    /// - ConnectionPoints[0] = Common/entry point (left side of Y)
    /// - ConnectionPoints[1] = Straight exit (right side, straight through)
    /// - ConnectionPoints[2] = Diverging exit (top of Y, the branch)
    /// </summary>
    [ExecuteAlways]
    public class TrackPiece : MonoBehaviour
    {
        [Header("Track Configuration")] 
        [SerializeField] private TrackType trackType = TrackType.Straight;
        [SerializeField] private List<Vector2> localWaypoints = new List<Vector2>();
        [SerializeField] private bool autoGenerateWaypoints = true;
        [SerializeField] private int curvedWaypointCount = 10;
        [SerializeField] private float bezierControlStrength = 0.33f;

        [Header("Visual Settings")] 
        [SerializeField] private bool showWaypointGizmos = true;
        [SerializeField] private Color waypointColor = Color.cyan;

        public string TrackID { get; private set; }
        public TrackType Type => trackType;
        public ConnectionPoint[] ConnectionPoints { get; private set; }
        public TrackSwitch TrackSwitch { get; private set; }

        private Vector2[] cachedWorldWaypoints = null;
        private Vector3 lastCachedPosition;
        private Quaternion lastCachedRotation;

        public Vector2[] WorldWaypoints
        {
            get
            {
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

        public List<Vector2> LocalWaypoints => new List<Vector2>(localWaypoints);

        public void InvalidateWaypointCache()
        {
            cachedWorldWaypoints = null;
        }

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
            TrackID = Guid.NewGuid().ToString();
            ConnectionPoints = GetComponentsInChildren<ConnectionPoint>();
            TrackSwitch = GetComponentInChildren<TrackSwitch>();

            if (ConnectionPoints.Length == 0)
            {
                Debug.LogWarning($"TrackPiece {gameObject.name} has no connection points!", this);
            }

            if (autoGenerateWaypoints)
            {
                GenerateWaypoints();
            }
        }

        private void OnEnable()
        {
            var trackManager = TrackManager.Instance;
            if (trackManager != null)
            {
                trackManager.RegisterTrack(this);
            }
        }

        private void OnDisable()
        {
            if (this == null || gameObject == null) return;
            if (Application.isPlaying && Time.frameCount < 2) return;

            var trackManager = TrackManager.Instance;
            if (trackManager != null)
            {
                trackManager.UnregisterTrack(this);
            }
        }

        private void OnValidate()
        {
            if (autoGenerateWaypoints && ConnectionPoints != null && ConnectionPoints.Length >= 2)
            {
                GenerateWaypoints();
            }
        }

        public void GenerateWaypoints()
        {
            localWaypoints.Clear();
            cachedWorldWaypoints = null;

            if (ConnectionPoints == null || ConnectionPoints.Length < 2)
            {
                Debug.LogWarning($"Cannot generate waypoints for {gameObject.name}: need at least 2 connection points");
                return;
            }

            switch (trackType)
            {
                case TrackType.Straight:
                    GenerateStraightWaypoints();
                    break;
                case TrackType.Curved:
                    GenerateCurvedWaypoints();
                    break;
                case TrackType.Junction:
                    GenerateJunctionWaypoints();
                    break;
            }
        }

        public void RegenerateWaypoints()
        {
            if (autoGenerateWaypoints)
            {
                GenerateWaypoints();
            }
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        private void GenerateStraightWaypoints()
        {
            Vector2 start = transform.InverseTransformPoint(ConnectionPoints[0].transform.position);
            Vector2 end = transform.InverseTransformPoint(ConnectionPoints[1].transform.position);
            localWaypoints.Add(start);
            localWaypoints.Add(end);
        }

        private void GenerateCurvedWaypoints()
        {
            GenerateBezierWaypoints(ConnectionPoints[0], ConnectionPoints[1]);
        }

        private void GenerateJunctionWaypoints()
        {
            if (ConnectionPoints.Length < 3)
            {
                Debug.LogWarning($"Junction {gameObject.name} needs at least 3 connection points");
                return;
            }

            // Y-Track layout:
            // ConnectionPoints[0] = Common/entry (left)
            // ConnectionPoints[1] = Straight exit (right, straight through)
            // ConnectionPoints[2] = Diverging exit (top branch)
            
            ConnectionPoint startPoint = ConnectionPoints[0];
            ConnectionPoint endPoint;

            // Choose endpoint based on switch state
            if (TrackSwitch != null && TrackSwitch.IsDiverging)
            {
                endPoint = ConnectionPoints[2]; // Diverging path
                // Debug.Log($"[Junction {name}] Switch is DIVERGING - waypoints go to ConnectionPoint[2]");
            }
            else
            {
                endPoint = ConnectionPoints[1]; // Straight path
                // Debug.Log($"[Junction {name}] Switch is STRAIGHT - waypoints go to ConnectionPoint[1]");
            }
            
            GenerateBezierWaypoints(startPoint, endPoint);
        }
        
        /// <summary>
        /// Generate waypoints for a junction using specific entry and exit connection point indices.
        /// Used by TrackPath to generate waypoints based on actual travel direction.
        /// </summary>
        public void GenerateJunctionWaypointsForCPs(int entryCPIndex, int exitCPIndex)
        {
            if (trackType != TrackType.Junction)
            {
                Debug.LogWarning($"{name} is not a junction");
                return;
            }
            
            if (entryCPIndex < 0 || entryCPIndex >= ConnectionPoints.Length ||
                exitCPIndex < 0 || exitCPIndex >= ConnectionPoints.Length)
            {
                Debug.LogWarning($"[Junction {name}] Invalid CP indices: entry={entryCPIndex}, exit={exitCPIndex}");
                return;
            }
            
            localWaypoints.Clear();
            cachedWorldWaypoints = null;
            
            ConnectionPoint entryCP = ConnectionPoints[entryCPIndex];
            ConnectionPoint exitCP = ConnectionPoints[exitCPIndex];
            
            // Debug.Log($"[Junction {name}] Generating waypoints for path: CP[{entryCPIndex}] → CP[{exitCPIndex}]");
            
            GenerateBezierWaypoints(entryCP, exitCP);
        }

        private void GenerateBezierWaypoints(ConnectionPoint startCP, ConnectionPoint endCP)
        {
            Vector2 start = transform.InverseTransformPoint(startCP.transform.position);
            Vector2 end = transform.InverseTransformPoint(endCP.transform.position);
            Vector2 startDir = transform.InverseTransformDirection(startCP.WorldDirection);
            Vector2 endDir = transform.InverseTransformDirection(endCP.WorldDirection);

            float distance = Vector2.Distance(start, end);
            float controlLength = distance * Mathf.Abs(bezierControlStrength);

            Vector2 p0 = start;
            Vector2 p3 = end;
            Vector2 p1 = p0 + startDir * controlLength;
            Vector2 p2 = p3 + endDir * controlLength;

            localWaypoints.Add(p0);
            for (int i = 1; i < curvedWaypointCount - 1; i++)
            {
                float t = (float)i / (curvedWaypointCount - 1);
                localWaypoints.Add(CalculateCubicBezier(t, p0, p1, p2, p3));
            }
            localWaypoints.Add(p3);
        }

        private Vector2 CalculateCubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }

        /// <summary>
        /// Get connected track pieces.
        /// For junctions with switches, this filters connections based on switch state.
        /// </summary>
        /// <param name="respectSwitchState">If true, only return the active branch for junctions</param>
        public List<TrackPiece> GetConnectedTracks(bool respectSwitchState = true)
        {
            List<TrackPiece> connectedTracks = new List<TrackPiece>();

            // For junctions with a switch, filter based on switch state
            if (respectSwitchState && trackType == TrackType.Junction && TrackSwitch != null && ConnectionPoints.Length >= 3)
            {
                // ConnectionPoints[0] = Common - always include its connection
                // ConnectionPoints[1] = Straight - include if switch is NOT diverging
                // ConnectionPoints[2] = Diverging - include if switch IS diverging
                
                // Always add the track connected to the common point
                if (ConnectionPoints[0].IsConnected && ConnectionPoints[0].ConnectedTo != null)
                {
                    var connectedTrack = ConnectionPoints[0].ConnectedTo.ParentTrack;
                    if (connectedTrack != null)
                    {
                        connectedTracks.Add(connectedTrack);
                    }
                }
                
                // Add only the active branch based on switch state
                int activeBranchIndex = TrackSwitch.IsDiverging ? 2 : 1;
                
                if (ConnectionPoints[activeBranchIndex].IsConnected && 
                    ConnectionPoints[activeBranchIndex].ConnectedTo != null)
                {
                    var connectedTrack = ConnectionPoints[activeBranchIndex].ConnectedTo.ParentTrack;
                    if (connectedTrack != null)
                    {
                        connectedTracks.Add(connectedTrack);
                    }
                }
                
                // Debug.Log($"[{name}] GetConnectedTracks (switch={TrackSwitch.IsDiverging}): returning {connectedTracks.Count} tracks");
            }
            else
            {
                // Return ALL connections for non-junction tracks or when ignoring switch state
                foreach (var cp in ConnectionPoints)
                {
                    if (cp.IsConnected && cp.ConnectedTo != null)
                    {
                        var connectedTrack = cp.ConnectedTo.ParentTrack;
                        if (connectedTrack != null && !connectedTracks.Contains(connectedTrack))
                        {
                            connectedTracks.Add(connectedTrack);
                        }
                    }
                }
            }

            return connectedTracks;
        }

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

            Gizmos.color = waypointColor;
            Vector2[] worldPoints = WorldWaypoints;

            foreach (var point in worldPoints)
            {
                Gizmos.DrawSphere(point, 0.1f);
            }

            for (int i = 1; i < worldPoints.Length; i++)
            {
                Gizmos.DrawLine(worldPoints[i - 1], worldPoints[i]);
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (localWaypoints.Count == 0) return;

            Gizmos.color = Color.yellow;
            Vector2[] worldPoints = WorldWaypoints;
            for (int i = 0; i < worldPoints.Length; i++)
            {
                Gizmos.DrawWireSphere(worldPoints[i], 0.15f);
            }
        }

        [UnityEditor.MenuItem("GameObject/Off The Rails/Track Piece", false, 10)]
        private static void CreateTrackPiece()
        {
            GameObject trackPiece = new GameObject("Track Piece");
            trackPiece.AddComponent<TrackPiece>();
            
            GameObject cp1 = new GameObject("Connection Point 1");
            cp1.transform.SetParent(trackPiece.transform);
            cp1.transform.localPosition = new Vector3(-0.5f, 0, 0);
            cp1.AddComponent<ConnectionPoint>();

            GameObject cp2 = new GameObject("Connection Point 2");
            cp2.transform.SetParent(trackPiece.transform);
            cp2.transform.localPosition = new Vector3(0.5f, 0, 0);
            cp2.AddComponent<ConnectionPoint>();

            UnityEditor.Selection.activeGameObject = trackPiece;
        }
        #endif
    }
}
