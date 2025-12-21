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
            
            // Reverse waypoints
            Waypoints.Reverse();
            
            // Regenerate waypoints with correct junction directions
            GenerateWaypoints();
            CalculateTotalLength();
            
            Debug.Log($"[TrackPath] Reversed path: now {StartTrack?.name} → {EndTrack?.name}");
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
            
            float oldLength = TotalLength;
            Clear();
            
            List<TrackPiece> route = new List<TrackPiece>();
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            
            if (FindPathDFS(StartTrack, EndTrack, route, visited))
            {
                TrackPieces.AddRange(route);
                GenerateWaypoints();
                CalculateTotalLength();
                
                Debug.Log($"[TrackPath] Rebuilt path: {TrackPieces.Count} tracks, {Waypoints.Count} waypoints, length changed from {oldLength:F1} to {TotalLength:F1}");
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
                    
                    // Generate waypoints for the specific entry/exit CPs
                    if (entryCP >= 0 && exitCP >= 0)
                    {
                        track.GenerateJunctionWaypointsForCPs(entryCP, exitCP);
                    }
                    else
                    {
                        // Fallback to default generation
                        track.InvalidateWaypointCache();
                        track.GenerateWaypoints();
                    }
                }
                else
                {
                    // Normal track - regenerate waypoints
                    track.InvalidateWaypointCache();
                    track.GenerateWaypoints();
                }
                
                Vector2[] trackWaypoints = track.WorldWaypoints;
                
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
