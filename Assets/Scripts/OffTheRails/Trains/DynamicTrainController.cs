using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Dynamic train controller that follows tracks in real-time.
    /// Instead of pre-calculated paths, trains follow the physical track connections
    /// and check switch states when they reach junctions.
    /// </summary>
    public class DynamicTrainController : MonoBehaviour
    {
        [Title("Train Settings")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private bool isActive = true;
        [SerializeField] private bool despawnAtEnd = true;
        
        [Title("Visual Settings")]
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private float rotationOffset = 0f;
        
        [Title("Starting Configuration")]
        [SerializeField, Required] private TrackPiece startingTrack;
        [SerializeField] private int startingConnectionPoint = 0;
        [Tooltip("If true, train travels AWAY from the starting CP. If false, travels TOWARD it.")]
        [SerializeField] private bool travelAwayFromStartCP = true;
        
        [Title("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.cyan;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private TrackPiece currentTrack;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private int entryCP = -1;  // Which CP we entered the current track from
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private int exitCP = -1;   // Which CP we're heading toward
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private List<Vector2> currentWaypoints = new List<Vector2>();
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private int currentWaypointIndex = 0;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private float distanceAlongTrack = 0f;
        
        public bool IsActive => isActive;
        public TrackPiece CurrentTrack => currentTrack;
        
        private void Start()
        {
            if (startingTrack != null)
            {
                InitializeOnTrack(startingTrack, startingConnectionPoint, travelAwayFromStartCP);
            }
            else
            {
                Debug.LogError($"[DynamicTrain {name}] No starting track assigned!");
            }
        }
        
        /// <summary>
        /// Initialize the train on a specific track
        /// </summary>
        /// <param name="track">The track to start on</param>
        /// <param name="fromCP">Which connection point we're "coming from"</param>
        /// <param name="travelAway">If true, travel away from fromCP. If false, travel toward it.</param>
        [Button("Initialize On Track")]
        public void InitializeOnTrack(TrackPiece track, int fromCP, bool travelAway = true)
        {
            currentTrack = track;
            
            if (travelAway)
            {
                // We entered from this CP, find exit
                entryCP = fromCP;
                exitCP = FindExitCP(track, fromCP);
            }
            else
            {
                // We're traveling TOWARD this CP
                exitCP = fromCP;
                entryCP = FindExitCP(track, fromCP);
            }
            
            GenerateWaypointsForCurrentTrack();
            
            // Position train at start of waypoints
            if (currentWaypoints.Count > 0)
            {
                transform.position = currentWaypoints[0];
                currentWaypointIndex = 0;
                
                // Face the right direction
                if (rotateToDirection && currentWaypoints.Count > 1)
                {
                    Vector2 dir = (currentWaypoints[1] - currentWaypoints[0]).normalized;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
                }
            }
            
            Debug.Log($"[DynamicTrain {name}] Initialized on {track.name}, entry=CP[{entryCP}], exit=CP[{exitCP}], waypoints={currentWaypoints.Count}");
        }
        
        /// <summary>
        /// Find which CP to exit from, given the entry CP
        /// For junctions, this checks the current switch state
        /// </summary>
        private int FindExitCP(TrackPiece track, int entryFromCP)
        {
            if (track.Type == TrackType.Junction && track.TrackSwitch != null)
            {
                // Junction logic
                var cps = track.ConnectionPoints;
                
                if (entryFromCP == 0)
                {
                    // Entering from facing point - switch determines exit
                    return track.TrackSwitch.IsDiverging ? 2 : 1;
                }
                else if (entryFromCP == 1 || entryFromCP == 2)
                {
                    // Entering from trailing point - must exit to CP[0]
                    // Check if switch is aligned correctly
                    int expectedEntry = track.TrackSwitch.IsDiverging ? 2 : 1;
                    if (entryFromCP != expectedEntry)
                    {
                        Debug.LogWarning($"[DynamicTrain {name}] DERAILMENT! Entered junction {track.name} from CP[{entryFromCP}] but switch is set for CP[{expectedEntry}]");
                        // Could trigger derailment event here
                    }
                    return 0;
                }
            }
            
            // Regular track - find the OTHER connection point
            var connectionPoints = track.ConnectionPoints;
            for (int i = 0; i < connectionPoints.Length; i++)
            {
                if (i != entryFromCP)
                    return i;
            }
            
            return -1; // Dead end
        }
        
        /// <summary>
        /// Generate waypoints for traversing the current track from entry to exit CP
        /// </summary>
        private void GenerateWaypointsForCurrentTrack()
        {
            currentWaypoints.Clear();
            currentWaypointIndex = 0;
            
            if (currentTrack == null) return;
            
            Vector2[] trackWaypoints;
            
            // For junctions, generate specific waypoints for entry→exit path
            if (currentTrack.Type == TrackType.Junction && entryCP >= 0 && exitCP >= 0)
            {
                trackWaypoints = GenerateJunctionWaypoints(currentTrack, entryCP, exitCP);
            }
            else
            {
                trackWaypoints = currentTrack.WorldWaypoints;
            }
            
            if (trackWaypoints == null || trackWaypoints.Length == 0)
            {
                Debug.LogWarning($"[DynamicTrain {name}] No waypoints for track {currentTrack.name}");
                return;
            }
            
            // Determine if we need to reverse waypoints based on entry/exit
            // Compare first/last waypoint positions to entry/exit CP positions
            Vector2 entryCPPos = GetCPWorldPosition(currentTrack, entryCP);
            Vector2 firstWP = trackWaypoints[0];
            Vector2 lastWP = trackWaypoints[trackWaypoints.Length - 1];
            
            float distFirstToEntry = Vector2.Distance(firstWP, entryCPPos);
            float distLastToEntry = Vector2.Distance(lastWP, entryCPPos);
            
            if (distLastToEntry < distFirstToEntry)
            {
                // Need to reverse - last waypoint is closer to entry
                for (int i = trackWaypoints.Length - 1; i >= 0; i--)
                {
                    currentWaypoints.Add(trackWaypoints[i]);
                }
            }
            else
            {
                // Normal order
                currentWaypoints.AddRange(trackWaypoints);
            }
        }
        
        /// <summary>
        /// Generate waypoints for a specific path through a junction
        /// </summary>
        private Vector2[] GenerateJunctionWaypoints(TrackPiece junction, int entryCPIndex, int exitCPIndex)
        {
            var connectionPoints = junction.ConnectionPoints;
            if (entryCPIndex >= connectionPoints.Length || exitCPIndex >= connectionPoints.Length)
                return junction.WorldWaypoints;
            
            ConnectionPoint entryPoint = connectionPoints[entryCPIndex];
            ConnectionPoint exitPoint = connectionPoints[exitCPIndex];
            
            Vector2 start = entryPoint.WorldPosition;
            Vector2 end = exitPoint.WorldPosition;
            Vector2 startDir = entryPoint.WorldDirection;
            Vector2 endDir = exitPoint.WorldDirection;
            
            float distance = Vector2.Distance(start, end);
            float controlLength = distance * 0.48f;
            
            Vector2 p0 = start;
            Vector2 p3 = end;
            Vector2 p1 = p0 + startDir * controlLength;
            Vector2 p2 = p3 + endDir * controlLength;
            
            int waypointCount = 10;
            List<Vector2> waypoints = new List<Vector2>();
            
            waypoints.Add(p0);
            for (int i = 1; i < waypointCount - 1; i++)
            {
                float t = (float)i / (waypointCount - 1);
                waypoints.Add(CalculateCubicBezier(t, p0, p1, p2, p3));
            }
            waypoints.Add(p3);
            
            return waypoints.ToArray();
        }
        
        private Vector2 CalculateCubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }
        
        private Vector2 GetCPWorldPosition(TrackPiece track, int cpIndex)
        {
            if (track == null || cpIndex < 0 || cpIndex >= track.ConnectionPoints.Length)
                return track != null ? (Vector2)track.transform.position : Vector2.zero;
            
            return track.ConnectionPoints[cpIndex].WorldPosition;
        }
        
        private void Update()
        {
            if (!isActive || currentTrack == null || currentWaypoints.Count == 0)
                return;
            
            MoveAlongWaypoints();
        }
        
        private void MoveAlongWaypoints()
        {
            if (currentWaypointIndex >= currentWaypoints.Count - 1)
            {
                // Reached end of current track, move to next
                MoveToNextTrack();
                return;
            }
            
            Vector2 currentPos = transform.position;
            Vector2 targetPos = currentWaypoints[currentWaypointIndex + 1];
            
            float distToTarget = Vector2.Distance(currentPos, targetPos);
            float moveDistance = speed * Time.deltaTime;
            
            if (moveDistance >= distToTarget)
            {
                // Reached waypoint, move to next
                transform.position = targetPos;
                currentWaypointIndex++;
                
                // Update rotation
                if (rotateToDirection && currentWaypointIndex < currentWaypoints.Count - 1)
                {
                    Vector2 dir = (currentWaypoints[currentWaypointIndex + 1] - targetPos).normalized;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
                }
            }
            else
            {
                // Move toward waypoint
                Vector2 dir = (targetPos - currentPos).normalized;
                transform.position = currentPos + dir * moveDistance;
                
                // Update rotation
                if (rotateToDirection)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
                }
            }
        }
        
        /// <summary>
        /// Move to the next connected track
        /// </summary>
        private void MoveToNextTrack()
        {
            if (currentTrack == null || exitCP < 0) 
            {
                OnReachedEnd();
                return;
            }
            
            var connectionPoints = currentTrack.ConnectionPoints;
            if (exitCP >= connectionPoints.Length)
            {
                OnReachedEnd();
                return;
            }
            
            ConnectionPoint exitPoint = connectionPoints[exitCP];
            
            if (!exitPoint.IsConnected || exitPoint.ConnectedTo == null)
            {
                // Dead end - end of the line
                Debug.Log($"[DynamicTrain {name}] Reached end of track at {currentTrack.name} CP[{exitCP}]");
                OnReachedEnd();
                return;
            }
            
            // Get the next track
            TrackPiece nextTrack = exitPoint.ConnectedTo.ParentTrack;
            int nextEntryCP = GetCPIndex(nextTrack, exitPoint.ConnectedTo);
            
            Debug.Log($"[DynamicTrain {name}] Moving from {currentTrack.name} CP[{exitCP}] → {nextTrack.name} CP[{nextEntryCP}]");
            
            // Update to next track
            currentTrack = nextTrack;
            entryCP = nextEntryCP;
            exitCP = FindExitCP(nextTrack, nextEntryCP);
            
            GenerateWaypointsForCurrentTrack();
            currentWaypointIndex = 0;
            
            // Skip first waypoint if we're already there (to avoid stutter)
            if (currentWaypoints.Count > 1)
            {
                Vector2 currentPos = transform.position;
                if (Vector2.Distance(currentPos, currentWaypoints[0]) < 0.5f)
                {
                    currentWaypointIndex = 0;
                }
            }
        }
        
        /// <summary>
        /// Get the index of a ConnectionPoint in its parent track
        /// </summary>
        private int GetCPIndex(TrackPiece track, ConnectionPoint cp)
        {
            for (int i = 0; i < track.ConnectionPoints.Length; i++)
            {
                if (track.ConnectionPoints[i] == cp)
                    return i;
            }
            return -1;
        }
        
        private void OnReachedEnd()
        {
            Debug.Log($"[DynamicTrain {name}] Reached end of line");
            isActive = false;
            
            if (despawnAtEnd)
            {
                Destroy(gameObject);
            }
        }
        
        public void Stop() => isActive = false;
        public void Resume() => isActive = true;
        public void SetSpeed(float newSpeed) => speed = Mathf.Max(0, newSpeed);
        
        /// <summary>
        /// Get current position
        /// </summary>
        public Vector2 GetPosition() => transform.position;
        
        /// <summary>
        /// Get current movement direction
        /// </summary>
        public Vector2 GetDirection()
        {
            if (currentWaypoints.Count > currentWaypointIndex + 1)
            {
                return (currentWaypoints[currentWaypointIndex + 1] - (Vector2)transform.position).normalized;
            }
            return transform.right;
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || currentWaypoints == null || currentWaypoints.Count == 0)
                return;
            
            Gizmos.color = gizmoColor;
            
            // Draw waypoints
            for (int i = 0; i < currentWaypoints.Count; i++)
            {
                Gizmos.DrawSphere(currentWaypoints[i], 0.2f);
                
                if (i < currentWaypoints.Count - 1)
                {
                    Gizmos.DrawLine(currentWaypoints[i], currentWaypoints[i + 1]);
                }
            }
            
            // Highlight current target
            if (currentWaypointIndex < currentWaypoints.Count - 1)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(currentWaypoints[currentWaypointIndex + 1], 0.3f);
            }
        }
    }
}
