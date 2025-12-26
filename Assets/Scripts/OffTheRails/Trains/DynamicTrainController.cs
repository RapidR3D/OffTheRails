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
        
        [Title("Position History (for train cars)")]
        [Tooltip("How much position history to keep for following cars")]
        [SerializeField] private float maxHistoryDistance = 100f;
        [Tooltip("Minimum distance between recorded positions (smaller = smoother curves, 0.2-0.5 recommended)")]
        [SerializeField] private float historyRecordInterval = 0.2f;
        
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
        
        // Position history for cars to follow
        private List<PositionRecord> positionHistory = new List<PositionRecord>();
        private float totalDistanceTraveled = 0f;
        private Vector2 lastRecordedPosition;
        
        public bool IsActive => isActive;
        public TrackPiece CurrentTrack => currentTrack;
        public float Speed => speed;
        public float TotalDistanceTraveled => totalDistanceTraveled;
        
        /// <summary>
        /// Get position at a distance behind the locomotive
        /// </summary>
        public bool TryGetPositionAtDistanceBehind(float distanceBehind, out Vector2 position, out Vector2 direction)
        {
            position = transform.position;
            direction = GetDirection();
            
            if (positionHistory.Count < 2)
            {
                // No history yet, position behind locomotive in a straight line
                position = (Vector2)transform.position - direction * distanceBehind;
                return true;
            }
            
            float targetDistance = totalDistanceTraveled - distanceBehind;
            
            // Find the two records to interpolate between for POSITION
            for (int i = 1; i < positionHistory.Count; i++)
            {
                if (positionHistory[i - 1].distance <= targetDistance && positionHistory[i].distance >= targetDistance)
                {
                    float t = Mathf.InverseLerp(positionHistory[i - 1].distance, positionHistory[i].distance, targetDistance);
                    position = Vector2.Lerp(positionHistory[i - 1].position, positionHistory[i].position, t);
                    
                    // For DIRECTION, we need to look at points BEFORE the car's position
                    // This prevents the car from "seeing" the curve before it reaches it
                    // Use the point at i-1 and the point before that (i-2)
                    if (i >= 2)
                    {
                        direction = (positionHistory[i - 1].position - positionHistory[i - 2].position).normalized;
                    }
                    else if (i >= 1)
                    {
                        // Not enough history behind, use stored direction from oldest relevant point
                        direction = positionHistory[i - 1].direction;
                    }
                    
                    if (direction == Vector2.zero || float.IsNaN(direction.x))
                        direction = Vector2.right;
                        
                    return true;
                }
            }
            
            // If target is before all history, use oldest record and extend backwards
            if (positionHistory.Count > 0 && targetDistance < positionHistory[0].distance)
            {
                var oldest = positionHistory[0];
                float extraDist = oldest.distance - targetDistance;
                position = oldest.position - oldest.direction * extraDist;
                direction = oldest.direction;
                return true;
            }
            
            // If target is after all history (shouldn't happen), use current position
            return true;
        }
        
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
        /// <param name="fromCP">Which connection point to start near</param>
        /// <param name="travelAway">If true, travel away from fromCP. If false, travel toward it.</param>
        [Button("Initialize On Track")]
        public void InitializeOnTrack(TrackPiece track, int fromCP, bool travelAway = true)
        {
            currentTrack = track;
            
            // Determine entry and exit CPs based on travel direction
            if (travelAway)
            {
                // We're at fromCP and want to travel AWAY from it
                // So we "entered" from fromCP and exit to the other side
                entryCP = fromCP;
                exitCP = FindExitCP(track, fromCP);
            }
            else
            {
                // We want to travel TOWARD fromCP
                // So fromCP is our exit, and we need to find where we "entered" from
                exitCP = fromCP;
                // For regular tracks, entry is the other CP
                // For junctions, we need to find a valid entry that leads to fromCP
                entryCP = FindEntryCP(track, fromCP);
            }
            
            // Debug.Log($"[DynamicTrain {name}] Setting up on {track.name}: travelAway={travelAway}, fromCP={fromCP} → entry=CP[{entryCP}], exit=CP[{exitCP}]");
            
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
                
                // Seed position history with initial position and backwards along the track
                // This gives cars something to follow from the start
                SeedPositionHistory();
            }
            
            // Debug.Log($"[DynamicTrain {name}] Initialized on {track.name}, entry=CP[{entryCP}], exit=CP[{exitCP}], waypoints={currentWaypoints.Count}");
        }
        
        /// <summary>
        /// Seed position history so cars have something to follow from the start.
        /// Seeds along the actual track waypoints going backwards.
        /// </summary>
        private void SeedPositionHistory()
        {
            positionHistory.Clear();
            totalDistanceTraveled = 0f;
            
            // Build a list of waypoints going backwards from current position
            // We need to trace back through the track network
            List<Vector2> backwardsPath = new List<Vector2>();
            
            // Start with current waypoints in reverse (from current position backwards)
            for (int i = currentWaypointIndex; i >= 0; i--)
            {
                backwardsPath.Add(currentWaypoints[i]);
            }
            
            // Now trace backwards through connected tracks
            TrackPiece traceTrack = currentTrack;
            int traceEntryCP = entryCP;
            float totalBackwardsDistance = 0f;
            
            // Calculate distance of waypoints we already have
            for (int i = 1; i < backwardsPath.Count; i++)
            {
                totalBackwardsDistance += Vector2.Distance(backwardsPath[i - 1], backwardsPath[i]);
            }
            
            // Keep tracing backwards until we have enough history
            int safetyCounter = 0;
            while (totalBackwardsDistance < maxHistoryDistance && safetyCounter < 20)
            {
                safetyCounter++;
                
                // Find the track connected to our entry point
                if (traceTrack == null || traceEntryCP < 0 || traceEntryCP >= traceTrack.ConnectionPoints.Length)
                    break;
                    
                var entryPoint = traceTrack.ConnectionPoints[traceEntryCP];
                if (!entryPoint.IsConnected || entryPoint.ConnectedTo == null)
                    break;
                
                // Get the previous track
                TrackPiece prevTrack = entryPoint.ConnectedTo.ParentTrack;
                int prevExitCP = GetCPIndex(prevTrack, entryPoint.ConnectedTo);
                
                // Get waypoints for previous track
                Vector2[] prevWaypoints = prevTrack.WorldWaypoints;
                if (prevWaypoints == null || prevWaypoints.Length == 0)
                    break;
                
                // Determine which direction to add waypoints
                Vector2 connectionPoint = entryPoint.WorldPosition;
                float distToFirst = Vector2.Distance(connectionPoint, prevWaypoints[0]);
                float distToLast = Vector2.Distance(connectionPoint, prevWaypoints[prevWaypoints.Length - 1]);
                
                // Add waypoints in the correct order (away from connection point)
                if (distToLast < distToFirst)
                {
                    // Last waypoint is near connection, so add from last-1 to first
                    for (int i = prevWaypoints.Length - 2; i >= 0; i--)
                    {
                        backwardsPath.Add(prevWaypoints[i]);
                        if (backwardsPath.Count > 1)
                        {
                            totalBackwardsDistance += Vector2.Distance(
                                backwardsPath[backwardsPath.Count - 2], 
                                backwardsPath[backwardsPath.Count - 1]
                            );
                        }
                    }
                }
                else
                {
                    // First waypoint is near connection, so add from 1 to last
                    for (int i = 1; i < prevWaypoints.Length; i++)
                    {
                        backwardsPath.Add(prevWaypoints[i]);
                        if (backwardsPath.Count > 1)
                        {
                            totalBackwardsDistance += Vector2.Distance(
                                backwardsPath[backwardsPath.Count - 2], 
                                backwardsPath[backwardsPath.Count - 1]
                            );
                        }
                    }
                }
                
                // Find the entry CP for the previous track (to continue tracing)
                int prevEntryCP = -1;
                for (int i = 0; i < prevTrack.ConnectionPoints.Length; i++)
                {
                    if (i != prevExitCP)
                    {
                        // For simple tracks, just pick the other CP
                        // For junctions, this is more complex but we'll take what we can get
                        if (prevTrack.ConnectionPoints[i].IsConnected)
                        {
                            prevEntryCP = i;
                            break;
                        }
                    }
                }
                
                traceTrack = prevTrack;
                traceEntryCP = prevEntryCP;
            }
            
            // Now convert backwards path to position history with distances
            // The backwards path goes: current position -> ... -> oldest position
            // We need to assign distances where current = 0, older = negative
            
            float distanceFromStart = 0f;
            Vector2 prevPos = backwardsPath[0];
            
            for (int i = 0; i < backwardsPath.Count; i++)
            {
                Vector2 pos = backwardsPath[i];
                
                if (i > 0)
                {
                    distanceFromStart -= Vector2.Distance(prevPos, pos);
                }
                
                Vector2 dir;
                if (i < backwardsPath.Count - 1)
                {
                    dir = (backwardsPath[i] - backwardsPath[i + 1]).normalized;
                }
                else if (i > 0)
                {
                    dir = (backwardsPath[i - 1] - backwardsPath[i]).normalized;
                }
                else
                {
                    dir = GetDirection();
                }
                
                // Insert at beginning so oldest is first
                positionHistory.Add(new PositionRecord
                {
                    position = pos,
                    direction = dir,
                    distance = distanceFromStart
                });
                
                prevPos = pos;
            }
            
            // Reverse so oldest (most negative distance) is first
            positionHistory.Reverse();
            
            // Set the last recorded position to current
            lastRecordedPosition = transform.position;
            
            // Debug.Log($"[DynamicTrain {name}] Seeded {positionHistory.Count} history points, distance range: {positionHistory[0].distance:F1} to {positionHistory[positionHistory.Count-1].distance:F1}");
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
        
        /// <summary>
        /// Find which CP we should "enter" from to exit at the given exitCP
        /// </summary>
        private int FindEntryCP(TrackPiece track, int targetExitCP)
        {
            if (track.Type == TrackType.Junction && track.TrackSwitch != null)
            {
                // For junctions, if we want to exit at CP[0], we could enter from CP[1] or CP[2]
                // depending on switch state
                if (targetExitCP == 0)
                {
                    // Trailing point movement - enter from the active branch
                    return track.TrackSwitch.IsDiverging ? 2 : 1;
                }
                else if (targetExitCP == 1 || targetExitCP == 2)
                {
                    // Facing point movement - must enter from CP[0]
                    return 0;
                }
            }
            
            // Regular track - find the OTHER connection point
            var connectionPoints = track.ConnectionPoints;
            for (int i = 0; i < connectionPoints.Length; i++)
            {
                if (i != targetExitCP)
                    return i;
            }
            
            return -1;
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
                        // Debug.LogWarning($"[DynamicTrain {name}] DERAILMENT! Entered junction {track.name} from CP[{entryFromCP}] but switch is set for CP[{expectedEntry}]");
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
                // Debug.Log($"[DynamicTrain {name}] Generated {trackWaypoints?.Length ?? 0} junction waypoints for {currentTrack.name}");
            }
            else
            {
                trackWaypoints = currentTrack.WorldWaypoints;
                // Debug.Log($"[DynamicTrain {name}] Using {trackWaypoints?.Length ?? 0} track waypoints for {currentTrack.name}");
            }
            
            if (trackWaypoints == null || trackWaypoints.Length == 0)
            {
                // Debug.LogWarning($"[DynamicTrain {name}] No waypoints for track {currentTrack.name}");
                return;
            }
            
            // Determine if we need to reverse waypoints based on entry/exit
            // Compare first/last waypoint positions to entry/exit CP positions
            Vector2 entryCPPos = GetCPWorldPosition(currentTrack, entryCP);
            Vector2 exitCPPos = GetCPWorldPosition(currentTrack, exitCP);
            Vector2 firstWP = trackWaypoints[0];
            Vector2 lastWP = trackWaypoints[trackWaypoints.Length - 1];
            
            float distFirstToEntry = Vector2.Distance(firstWP, entryCPPos);
            float distLastToEntry = Vector2.Distance(lastWP, entryCPPos);
            
            // Debug.Log($"[DynamicTrain {name}] Waypoint alignment check: firstWP={firstWP}, lastWP={lastWP}, entryCP={entryCPPos}, exitCP={exitCPPos}");
            // Debug.Log($"[DynamicTrain {name}] distFirstToEntry={distFirstToEntry:F2}, distLastToEntry={distLastToEntry:F2}");
            
            if (distLastToEntry < distFirstToEntry)
            {
                // Need to reverse - last waypoint is closer to entry
                // Debug.Log($"[DynamicTrain {name}] Reversing waypoints (last WP closer to entry)");
                for (int i = trackWaypoints.Length - 1; i >= 0; i--)
                {
                    currentWaypoints.Add(trackWaypoints[i]);
                }
            }
            else
            {
                // Normal order
                // Debug.Log($"[DynamicTrain {name}] Using normal waypoint order");
                currentWaypoints.AddRange(trackWaypoints);
            }
            
            // Debug.Log($"[DynamicTrain {name}] Final waypoints: {currentWaypoints.Count}, first={currentWaypoints[0]}, last={currentWaypoints[currentWaypoints.Count-1]}");
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
            RecordPositionHistory();
        }
        
        /// <summary>
        /// Record position history for train cars to follow
        /// </summary>
        private void RecordPositionHistory()
        {
            Vector2 currentPos = transform.position;
            
            // Calculate distance moved since last record
            float distMoved = Vector2.Distance(lastRecordedPosition, currentPos);
            
            // Only record if we've moved enough (distance-based, not time-based)
            if (distMoved >= historyRecordInterval)
            {
                totalDistanceTraveled += distMoved;
                lastRecordedPosition = currentPos;
                
                positionHistory.Add(new PositionRecord
                {
                    position = currentPos,
                    direction = GetDirection(),
                    distance = totalDistanceTraveled
                });
                
                // Trim old history
                while (positionHistory.Count > 2 && 
                       totalDistanceTraveled - positionHistory[0].distance > maxHistoryDistance)
                {
                    positionHistory.RemoveAt(0);
                }
            }
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
                // Debug.Log($"[DynamicTrain {name}] Reached end of track at {currentTrack.name} CP[{exitCP}]");
                OnReachedEnd();
                return;
            }
            
            // Get the next track
            TrackPiece nextTrack = exitPoint.ConnectedTo.ParentTrack;
            int nextEntryCP = GetCPIndex(nextTrack, exitPoint.ConnectedTo);
            
            // Debug.Log($"[DynamicTrain {name}] Moving from {currentTrack.name} CP[{exitCP}] → {nextTrack.name} CP[{nextEntryCP}]");
            
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
        
        private void OnReachedEnd()
        {
            // Debug.Log($"[DynamicTrain {name}] Reached end of line");
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
    
    /// <summary>
    /// A recorded position in the locomotive's travel history
    /// </summary>
    public struct PositionRecord
    {
        public Vector2 position;
        public Vector2 direction;
        public float distance;
    }
}
