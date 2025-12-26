using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Represents a connected sequence of track pieces.
    /// Paths are built dynamically based on current switch states.
    /// </summary>
    public class TrackPath
    {
        public string PathID { get; private set; }
        public List<Vector2> Waypoints { get; private set; }
        public List<TrackPiece> TrackPieces { get; private set; }
        public float TotalLength { get; private set; }
        public bool IsLoop { get; private set; }
        
        /// <summary>
        /// The starting track piece (endpoint) for this path
        /// </summary>
        public TrackPiece StartTrack { get; private set; }
        
        /// <summary>
        /// The ending track piece (endpoint) for this path
        /// </summary>
        public TrackPiece EndTrack { get; private set; }

        public TrackPath()
        {
            PathID = System.Guid.NewGuid().ToString();
            Waypoints = new List<Vector2>();
            TrackPieces = new List<TrackPiece>();
            TotalLength = 0f;
            IsLoop = false;
        }

        public void Clear()
        {
            Waypoints.Clear();
            TrackPieces.Clear();
            TotalLength = 0f;
            IsLoop = false;
        }
        
        /// <summary>
        /// Reverse the path direction - swaps start/end and reverses waypoints.
        /// Useful when you want to travel the opposite direction.
        /// </summary>
        public void Reverse()
        {
            // Swap start and end tracks
            var temp = StartTrack;
            StartTrack = EndTrack;
            EndTrack = temp;
            
            // Reverse track pieces list
            TrackPieces.Reverse();
            
            // Reverse waypoints - this is all we need!
            // Don't regenerate - just reverse the existing waypoints
            Waypoints.Reverse();
            
            // Recalculate length (should be the same, but just in case)
            CalculateTotalLength();
            
            Debug.Log($"[TrackPath] Reversed path: now {StartTrack?.name} → {EndTrack?.name}, first waypoint={Waypoints[0]}, last waypoint={Waypoints[Waypoints.Count-1]}");
        }

        /// <summary>
        /// Build a path from start to end, following current switch states
        /// </summary>
        public void BuildFromEndpoints(TrackPiece start, TrackPiece end)
        {
            Clear();
            StartTrack = start;
            EndTrack = end;
            
            // Use DFS to find path, respecting current switch states
            List<TrackPiece> route = new List<TrackPiece>();
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            
            if (FindPathDFS(start, end, route, visited))
            {
                TrackPieces.AddRange(route);
                GenerateWaypoints();
                CalculateTotalLength();
                CheckIfLoop();
                
                Debug.Log($"[TrackPath] Built path from {start.name} to {end.name}: {TrackPieces.Count} tracks, {Waypoints.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"[TrackPath] Could not find path from {start.name} to {end.name}");
            }
        }
        
        /// <summary>
        /// Build a path from explicit route segments (from RouteDefinition).
        /// This bypasses automatic pathfinding and uses the exact tracks specified.
        /// </summary>
        public void BuildFromRouteSegments(List<RouteSegment> segments)
        {
            Clear();
            
            if (segments == null || segments.Count == 0)
            {
                Debug.LogWarning("[TrackPath] No segments provided");
                return;
            }
            
            // Store junction info for waypoint generation
            routeSegments = segments;
            
            // Add all tracks
            foreach (var seg in segments)
            {
                if (seg.track != null)
                    TrackPieces.Add(seg.track);
            }
            
            if (TrackPieces.Count == 0)
            {
                Debug.LogWarning("[TrackPath] No valid tracks in segments");
                return;
            }
            
            StartTrack = TrackPieces[0];
            EndTrack = TrackPieces[TrackPieces.Count - 1];
            
            // Generate waypoints using route segment info for junctions
            GenerateWaypointsFromRouteSegments();
            CalculateTotalLength();
            CheckIfLoop();
            
            Debug.Log($"[TrackPath] Built path from route: {TrackPieces.Count} tracks, {Waypoints.Count} waypoints, length={TotalLength:F1}");
        }
        
        // Store route segments for junction waypoint generation
        private List<RouteSegment> routeSegments;
        
        /// <summary>
        /// Generate waypoints using explicit route segment entry/exit points for junctions
        /// </summary>
        private void GenerateWaypointsFromRouteSegments()
        {
            Waypoints.Clear();
            
            if (TrackPieces.Count == 0 || routeSegments == null)
                return;
            
            for (int i = 0; i < TrackPieces.Count; i++)
            {
                var track = TrackPieces[i];
                var segment = (i < routeSegments.Count) ? routeSegments[i] : null;
                Vector2[] trackWaypoints;
                
                // For junctions, use the explicit entry/exit CPs from the route segment
                if (track.Type == TrackType.Junction && segment != null)
                {
                    int entryCP = segment.entryCP;
                    int exitCP = segment.exitCP;
                    
                    Debug.Log($"[GenerateWaypointsFromRoute] Junction {track.name}: CP[{entryCP}] → CP[{exitCP}]");
                    
                    trackWaypoints = GenerateJunctionWaypointsLocal(track, entryCP, exitCP);
                }
                else
                {
                    // Normal track - use its waypoints
                    trackWaypoints = track.WorldWaypoints;
                }
                
                if (trackWaypoints == null || trackWaypoints.Length == 0)
                {
                    Debug.LogWarning($"[GenerateWaypointsFromRoute] Track {track.name} has no waypoints!");
                    continue;
                }
                
                if (i == 0 || Waypoints.Count == 0)
                {
                    Waypoints.AddRange(trackWaypoints);
                }
                else
                {
                    // Determine if we need to reverse waypoints to connect properly
                    Vector2 lastWaypoint = Waypoints[Waypoints.Count - 1];
                    Vector2 firstOfNew = trackWaypoints[0];
                    Vector2 lastOfNew = trackWaypoints[trackWaypoints.Length - 1];
                    
                    float distToFirst = Vector2.Distance(lastWaypoint, firstOfNew);
                    float distToLast = Vector2.Distance(lastWaypoint, lastOfNew);
                    
                    float tolerance = 1.5f;
                    
                    if (distToLast < distToFirst)
                    {
                        // Reverse the waypoints
                        int startIndex = (distToLast < tolerance) ? trackWaypoints.Length - 2 : trackWaypoints.Length - 1;
                        startIndex = Mathf.Max(0, startIndex);
                        for (int j = startIndex; j >= 0; j--)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                    else
                    {
                        // Add waypoints in order
                        int startIndex = (distToFirst < tolerance) ? 1 : 0;
                        for (int j = startIndex; j < trackWaypoints.Length; j++)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Rebuild this path using current switch states
        /// Call this when any switch in the network changes
        /// </summary>
        public void RebuildPath()
        {
            if (StartTrack == null || EndTrack == null)
            {
                Debug.LogWarning("[TrackPath] Cannot rebuild - no start/end tracks set");
                return;
            }
            
            Debug.Log($"[TrackPath] Rebuilding path from {StartTrack.name} to {EndTrack.name}...");
            
            float oldLength = TotalLength;
            
            // Don't clear yet - we might need to keep the old path if rebuild fails
            List<TrackPiece> route = new List<TrackPiece>();
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            
            if (FindPathDFS(StartTrack, EndTrack, route, visited))
            {
                // Success - update the path
                Clear();
                TrackPieces.AddRange(route);
                GenerateWaypoints();
                CalculateTotalLength();
                
                Debug.Log($"[TrackPath] Rebuilt path: {TrackPieces.Count} tracks, {Waypoints.Count} waypoints, length changed from {oldLength:F1} to {TotalLength:F1}");
            }
            else
            {
                Debug.LogWarning($"[TrackPath] REBUILD FAILED: No path exists from {StartTrack.name} to {EndTrack.name} with current switch states!");
                // Keep the old path data so the train doesn't get stranded
            }
        }
        
        /// <summary>
        /// DFS pathfinding that respects current switch states
        /// </summary>
        private bool FindPathDFS(TrackPiece current, TrackPiece target, List<TrackPiece> path, HashSet<TrackPiece> visited)
        {
            path.Add(current);
            visited.Add(current);
            
            if (current == target)
            {
                return true;
            }
            
            // Get connections respecting switch state - this is the key!
            var connections = current.GetConnectedTracks(respectSwitchState: true);
            
            foreach (var next in connections)
            {
                if (!visited.Contains(next))
                {
                    if (FindPathDFS(next, target, path, visited))
                    {
                        return true;
                    }
                }
            }
            
            // Backtrack
            path.RemoveAt(path.Count - 1);
            visited.Remove(current);
            
            return false;
        }

        /// <summary>
        /// Generate waypoints from the ordered track pieces.
        /// For junctions, generates waypoints based on travel direction through the junction.
        /// </summary>
        private void GenerateWaypoints()
        {
            Waypoints.Clear();

            if (TrackPieces.Count == 0)
                return;

            for (int i = 0; i < TrackPieces.Count; i++)
            {
                var track = TrackPieces[i];
                Vector2[] trackWaypoints;
                
                // For junctions, we need to generate waypoints based on which CPs
                // connect to the previous and next tracks in the path
                if (track.Type == TrackType.Junction)
                {
                    TrackPiece prevTrack = (i > 0) ? TrackPieces[i - 1] : null;
                    TrackPiece nextTrack = (i < TrackPieces.Count - 1) ? TrackPieces[i + 1] : null;
                    
                    // Find which CPs connect to prev/next tracks
                    int entryCP = -1;
                    int exitCP = -1;
                    
                    for (int cpIdx = 0; cpIdx < track.ConnectionPoints.Length; cpIdx++)
                    {
                        var cp = track.ConnectionPoints[cpIdx];
                        if (cp.IsConnected && cp.ConnectedTo != null)
                        {
                            if (prevTrack != null && cp.ConnectedTo.ParentTrack == prevTrack)
                            {
                                entryCP = cpIdx;
                            }
                            if (nextTrack != null && cp.ConnectedTo.ParentTrack == nextTrack)
                            {
                                exitCP = cpIdx;
                            }
                        }
                    }
                    
                    Debug.Log($"[GenerateWaypoints] Junction {track.name}: entry=CP[{entryCP}] from {prevTrack?.name}, exit=CP[{exitCP}] to {nextTrack?.name}");
                    
                    // Generate waypoints locally for this path (don't modify the TrackPiece)
                    if (entryCP >= 0 && exitCP >= 0)
                    {
                        trackWaypoints = GenerateJunctionWaypointsLocal(track, entryCP, exitCP);
                    }
                    else
                    {
                        // Fallback to track's default waypoints
                        trackWaypoints = track.WorldWaypoints;
                    }
                }
                else
                {
                    // Normal track - use its waypoints
                    trackWaypoints = track.WorldWaypoints;
                }
                
                if (trackWaypoints == null || trackWaypoints.Length == 0)
                {
                    Debug.LogWarning($"[GenerateWaypoints] Track {track.name} has no waypoints!");
                    continue;
                }

                if (i == 0 || Waypoints.Count == 0)
                {
                    Waypoints.AddRange(trackWaypoints);
                }
                else
                {
                    // Determine if we need to reverse waypoints to connect properly
                    Vector2 lastWaypoint = Waypoints[Waypoints.Count - 1];
                    Vector2 firstOfNew = trackWaypoints[0];
                    Vector2 lastOfNew = trackWaypoints[trackWaypoints.Length - 1];

                    float distToFirst = Vector2.Distance(lastWaypoint, firstOfNew);
                    float distToLast = Vector2.Distance(lastWaypoint, lastOfNew);

                    float tolerance = 1.5f;
                    
                    if (distToLast < distToFirst)
                    {
                        // Reverse the waypoints
                        int startIndex = (distToLast < tolerance) ? trackWaypoints.Length - 2 : trackWaypoints.Length - 1;
                        startIndex = Mathf.Max(0, startIndex);
                        for (int j = startIndex; j >= 0; j--)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                    else
                    {
                        // Add waypoints in order
                        int startIndex = (distToFirst < tolerance) ? 1 : 0;
                        for (int j = startIndex; j < trackWaypoints.Length; j++)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generate junction waypoints locally for this path without modifying the TrackPiece.
        /// This allows multiple paths to use the same junction with different directions.
        /// </summary>
        private Vector2[] GenerateJunctionWaypointsLocal(TrackPiece junction, int entryCPIndex, int exitCPIndex)
        {
            var connectionPoints = junction.ConnectionPoints;
            ConnectionPoint entryCP = connectionPoints[entryCPIndex];
            ConnectionPoint exitCP = connectionPoints[exitCPIndex];
            
            Vector2 start = entryCP.WorldPosition;
            Vector2 end = exitCP.WorldPosition;
            Vector2 startDir = entryCP.WorldDirection;
            Vector2 endDir = exitCP.WorldDirection;

            float distance = Vector2.Distance(start, end);
            float controlLength = distance * 0.48f; // bezier control strength

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
        
        private void CalculateTotalLength()
        {
            TotalLength = 0f;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                TotalLength += Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
            }
        }

        private void CheckIfLoop()
        {
            if (Waypoints.Count < 3)
            {
                IsLoop = false;
                return;
            }
            float distance = Vector2.Distance(Waypoints[0], Waypoints[Waypoints.Count - 1]);
            IsLoop = distance < 0.1f;
        }

        /// <summary>
        /// Get position along the path at a given distance
        /// </summary>
        public Vector2 GetPositionAtDistance(float distance)
        {
            if (Waypoints.Count == 0) return Vector2.zero;
            if (Waypoints.Count == 1) return Waypoints[0];

            if (IsLoop && distance > TotalLength)
            {
                distance = distance % TotalLength;
            }
            else if (distance >= TotalLength)
            {
                return Waypoints[Waypoints.Count - 1];
            }
            else if (distance <= 0)
            {
                return Waypoints[0];
            }

            float currentDistance = 0f;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                float segmentLength = Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                
                if (currentDistance + segmentLength >= distance)
                {
                    float t = (distance - currentDistance) / segmentLength;
                    return Vector2.Lerp(Waypoints[i - 1], Waypoints[i], t);
                }
                currentDistance += segmentLength;
            }

            return Waypoints[Waypoints.Count - 1];
        }

        /// <summary>
        /// Get direction along the path at a given distance
        /// </summary>
        public Vector2 GetDirectionAtDistance(float distance)
        {
            if (Waypoints.Count < 2) return Vector2.right;

            if (IsLoop && distance > TotalLength)
            {
                distance = distance % TotalLength;
            }
            else if (distance >= TotalLength)
            {
                return (Waypoints[Waypoints.Count - 1] - Waypoints[Waypoints.Count - 2]).normalized;
            }
            else if (distance <= 0)
            {
                return (Waypoints[1] - Waypoints[0]).normalized;
            }

            float currentDistance = 0f;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                float segmentLength = Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                
                if (currentDistance + segmentLength >= distance)
                {
                    return (Waypoints[i] - Waypoints[i - 1]).normalized;
                }
                currentDistance += segmentLength;
            }

            return (Waypoints[Waypoints.Count - 1] - Waypoints[Waypoints.Count - 2]).normalized;
        }

        /// <summary>
        /// Find the closest distance along this path to a world position
        /// </summary>
        public float GetClosestDistanceToPoint(Vector2 worldPoint)
        {
            if (Waypoints.Count < 2) return 0f;
                
            float closestDist = float.MaxValue;
            float bestDistanceAlongPath = 0f;
            float currentDistanceAlongPath = 0f;
            
            for (int i = 0; i < Waypoints.Count - 1; i++)
            {
                Vector2 segStart = Waypoints[i];
                Vector2 segEnd = Waypoints[i + 1];
                float segmentLength = Vector2.Distance(segStart, segEnd);
                
                Vector2 closest = GetClosestPointOnSegment(worldPoint, segStart, segEnd);
                float dist = Vector2.Distance(worldPoint, closest);
                
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestDistanceAlongPath = currentDistanceAlongPath + Vector2.Distance(segStart, closest);
                }
                
                currentDistanceAlongPath += segmentLength;
            }
            
            return bestDistanceAlongPath;
        }
        
        private Vector2 GetClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 lineDir = end - start;
            float lineLength = lineDir.magnitude;

            if (lineLength < 0.001f) return start;

            lineDir /= lineLength;
            Vector2 pointDir = point - start;
            float projection = Mathf.Clamp(Vector2.Dot(pointDir, lineDir), 0f, lineLength);

            return start + lineDir * projection;
        }

        public bool ContainsTrack(TrackPiece track)
        {
            return TrackPieces.Contains(track);
        }
    }
}
