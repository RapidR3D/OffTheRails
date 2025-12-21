using System.Collections.Generic;
using OffTheRails.Tracks;
using UnityEngine;
using System.Linq;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Represents a connected sequence of track pieces.
    /// Manages waypoints for trains to follow and updates when tracks connect/disconnect.
    /// </summary>
    public class TrackPath
    {
        /// <summary>
        /// Unique identifier for this path
        /// </summary>
        public string PathID { get; private set; }

        /// <summary>
        /// All waypoints in this path (in world space)
        /// </summary>
        public List<Vector2> Waypoints { get; private set; }

        /// <summary>
        /// All track pieces that make up this path
        /// </summary>
        public List<TrackPiece> TrackPieces { get; private set; }

        /// <summary>
        /// Total length of this path
        /// </summary>
        public float TotalLength { get; private set; }

        /// <summary>
        /// Whether this path forms a complete loop
        /// </summary>
        public bool IsLoop { get; private set; }

        /// <summary>
        /// Constructor
        /// Note: PathID uses Guid for uniqueness. For serialization/networking,
        /// consider implementing a custom ID management system.
        /// </summary>
        public TrackPath()
        {
            PathID = System.Guid.NewGuid().ToString();
            Waypoints = new List<Vector2>();
            TrackPieces = new List<TrackPiece>();
            TotalLength = 0f;
            IsLoop = false;
        }

        /// <summary>
        /// Build path from a starting track piece
        /// </summary>
        /// <param name="startTrack">The track piece to start from</param>
        public void BuildFromTrack(TrackPiece startTrack)
        {
            if (startTrack == null)
            {
                Debug.LogError("Cannot build path from null track");
                return;
            }

            Clear();

            // Step 1: Use BFS to find all connected tracks
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            Queue<TrackPiece> toVisit = new Queue<TrackPiece>();

            toVisit.Enqueue(startTrack);
            visited.Add(startTrack);

            List<TrackPiece> allTracks = new List<TrackPiece>();

            while (toVisit.Count > 0)
            {
                TrackPiece current = toVisit.Dequeue();
                allTracks.Add(current);

                // Visit connected tracks (ignore switch state for path building)
                foreach (var connected in current.GetConnectedTracks(respectSwitchState: false))
                {
                    if (!visited.Contains(connected))
                    {
                        visited.Add(connected);
                        toVisit.Enqueue(connected);
                    }
                }
            }

            Debug.Log($"[BuildFromTrack] Found {allTracks.Count} connected tracks via BFS");

            // Step 2: Find ACTUAL endpoints (tracks with unconnected connection points)
            TrackPiece startPoint = null;
            int minConnections = int.MaxValue;
            
            foreach (var track in allTracks)
            {
                // Count ACTUAL physical connections (not respecting switch state)
                int connectedCount = 0;
                
                foreach (var cp in track.ConnectionPoints)
                {
                    if (cp.IsConnected)
                    {
                        connectedCount++;
                    }
                }
                
                Debug.Log($"[BuildFromTrack] Track '{track.name}' has {connectedCount} connected points (out of {track.ConnectionPoints.Length} total)");
                
                // Prefer tracks with fewer connections (true endpoints)
                // A straight track with 1 connection = endpoint
                // A junction with 2 connections = also a valid start
                if (connectedCount < minConnections && connectedCount < track.ConnectionPoints.Length)
                {
                    minConnections = connectedCount;
                    startPoint = track;
                }
            }

            // If no endpoint found, use the original start
            if (startPoint == null)
            {
                startPoint = startTrack;
                Debug.LogWarning($"[BuildFromTrack] No clear endpoint found, using original start: {startTrack.name}");
            }
            else
            {
                Debug.Log($"[BuildFromTrack] Selected endpoint: {startPoint.name} with {minConnections} connections");
            }

            // Step 3: Traverse from the endpoint to build ordered list
            TrackPieces.Clear();
            visited.Clear();
            
            TrackPiece current2 = startPoint;
            TrackPieces.Add(current2);
            visited.Add(current2);

            Debug.Log($"[BuildFromTrack] Starting traversal from {startPoint.name}");
            
            int traversalStep = 0;
            while (true)
            {
                var possibleNext = current2.GetConnectedTracks(respectSwitchState: false);
                TrackPiece next = null;
    
                // Filter out already visited tracks
                List<TrackPiece> candidates = new List<TrackPiece>();
                foreach (var candidate in possibleNext)
                {
                    if (!visited.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
    
                if (candidates.Count == 0)
                {
                    Debug.Log($"[BuildFromTrack] Step {traversalStep}: At {current2.name} - DEAD END");
                    break;
                }
                else if (candidates.Count == 1)
                {
                    next = candidates[0];
                    Debug.Log($"[BuildFromTrack] Step {traversalStep}: {current2.name} → {next.name}");
                }
                else
                {
                    // Multiple paths - take the first one
                    next = candidates[0];
                    Debug.Log($"[BuildFromTrack] Step {traversalStep}: {current2.name} → {next.name} (chose from {candidates.Count} options)");
                }
    
                if (next == null)
                    break;
        
                TrackPieces.Add(next);
                visited.Add(next);
                current2 = next;
                traversalStep++;
            }
            Debug.Log($"[TrackPath] ✓ Ordered {TrackPieces.Count} tracks (started from {startPoint.name})");
        
            // Generate waypoints from the ordered track pieces
            GenerateWaypoints();   
        
            // Calculate total length
            CalculateTotalLength();  
        
            // Check if this is a loop
            CheckIfLoop(); 
            //Debug.Log($"[TrackPath] ✓ Ordered {TrackPieces.Count} tracks (started from {startPoint.name})");
            Debug.Log($"[TrackPath] Track order: {string.Join(" → ", System.Linq.Enumerable.Select(TrackPieces, t => t.name))}");
        }
        
        /// <summary>
        /// Remove a track piece from this path
        /// </summary>
        /// <param name="track">The track piece to remove</param>
        public void RemoveTrack(TrackPiece track)
        {
            if (track == null || !TrackPieces.Contains(track))
                return;

            TrackPieces.Remove(track);
            GenerateWaypoints();
            CalculateTotalLength();
        }

        /// <summary>
        /// Clear all tracks and waypoints from this path
        /// </summary>
        public void Clear()
        {
            TrackPieces.Clear();
            Waypoints.Clear();
            TotalLength = 0f;
            IsLoop = false;
        }

        /// <summary>
        /// Generate waypoints from track pieces (assumes TrackPieces is already ordered)
        /// </summary>
        private void GenerateWaypoints()
        {
            Waypoints.Clear();

            if (TrackPieces.Count == 0)
                return;

            Debug.Log($"[GenerateWaypoints] Processing {TrackPieces.Count} ordered tracks");

            // Combine waypoints from ordered tracks
            for (int i = 0; i < TrackPieces.Count; i++)
            {
                Vector2[] trackWaypoints = TrackPieces[i].WorldWaypoints;
                
                if (trackWaypoints.Length == 0)
                {
                    Debug.LogWarning($"[GenerateWaypoints] Track {TrackPieces[i].name} has no waypoints! Skipping...");
                    continue;
                }

                // For the first track, add all waypoints
                if (i == 0 || Waypoints.Count == 0)
                {
                    Waypoints.AddRange(trackWaypoints);
                    Debug.Log($"[GenerateWaypoints] Track {i} ({TrackPieces[i].name}): Added {trackWaypoints.Length} waypoints (Total: {Waypoints.Count})");
                }
                else
                {
                    // For subsequent tracks, check if we should reverse waypoints
                    Vector2 lastWaypoint = Waypoints[Waypoints.Count - 1];
                    Vector2 firstOfNew = trackWaypoints[0];
                    Vector2 lastOfNew = trackWaypoints[trackWaypoints.Length - 1];

                    float distToFirst = Vector2.Distance(lastWaypoint, firstOfNew);
                    float distToLast = Vector2.Distance(lastWaypoint, lastOfNew);

                    Debug.Log($"[GenerateWaypoints] Track {i} ({TrackPieces[i].name}): distToFirst={distToFirst:F2}, distToLast={distToLast:F2}");

                    // Tolerance for "close enough" connections (increased for junctions)
                    float tolerance = 1.0f;
                    
                    if (distToLast < distToFirst)
                    {
                        // Reverse the waypoints
                        int startIndex = (distToLast < tolerance) ? trackWaypoints.Length - 2 : trackWaypoints.Length - 1;
                        Debug.Log($" → Reversing waypoints (adding from index {startIndex} down to 0)");
                        
                        for (int j = startIndex; j >= 0; j--)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                    else
                    {
                        // Add waypoints in order
                        bool skipFirst = distToFirst < tolerance;
                        int startIndex = skipFirst ? 1 : 0;
                        Debug.Log($" → Adding in order (starting from index {startIndex})");
                        
                        for (int j = startIndex; j < trackWaypoints.Length; j++)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                    
                    Debug.Log($" → Total waypoints now: {Waypoints.Count}");
                }
            }
            
            Debug.Log($"[TrackPath] ✓ Generated {Waypoints.Count} total waypoints from {TrackPieces.Count} tracks");
        }

        /// <summary>
        /// Calculate the total length of this path
        /// </summary>
        private void CalculateTotalLength()
        {
            TotalLength = 0f;

            for (int i = 1; i < Waypoints.Count; i++)
            {
                TotalLength += Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
            }
        }

        /// <summary>
        /// Check if this path forms a complete loop
        /// </summary>
        private void CheckIfLoop()
        {
            if (Waypoints.Count < 3)
            {
                IsLoop = false;
                return;
            }

            // Check if first and last waypoints are close (forms a loop)
            float distance = Vector2.Distance(Waypoints[0], Waypoints[Waypoints.Count - 1]);
            IsLoop = distance < 0.1f;
        }

        /// <summary>
        /// Get position along the path at a given distance
        /// </summary>
        /// <param name="distance">Distance along the path</param>
        /// <returns>Position at that distance</returns>
        public Vector2 GetPositionAtDistance(float distance)
        {
            if (Waypoints.Count == 0)
                return Vector2.zero;

            if (Waypoints.Count == 1)
                return Waypoints[0];

            // Handle looping
            if (IsLoop && distance > TotalLength)
            {
                distance = distance % TotalLength;
            }
            else if (distance > TotalLength)
            {
                return Waypoints[Waypoints.Count - 1];
            }
            else if (distance < 0)
            {
                return Waypoints[0];
            }

            // Find the segment containing this distance
            float currentDistance = 0f;

            for (int i = 1; i < Waypoints.Count; i++)
            {
                float segmentLength = Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                
                if (currentDistance + segmentLength >= distance)
                {
                    // Interpolate along this segment
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
        /// <param name="distance">Distance along the path</param>
        /// <returns>Direction at that distance</returns>
        public Vector2 GetDirectionAtDistance(float distance)
        {
            if (Waypoints.Count < 2)
                return Vector2.right;

            // Handle looping
            if (IsLoop && distance > TotalLength)
            {
                distance = distance % TotalLength;
            }
            else if (distance > TotalLength)
            {
                // Return direction of last segment
                return (Waypoints[Waypoints.Count - 1] - Waypoints[Waypoints.Count - 2]).normalized;
            }
            else if (distance < 0)
            {
                // Return direction of first segment
                return (Waypoints[1] - Waypoints[0]).normalized;
            }

            // Find the segment containing this distance
            float currentDistance = 0f;

            for (int i = 1; i < Waypoints.Count; i++)
            {
                float segmentLength = Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                
                if (currentDistance + segmentLength >= distance)
                {
                    // Return direction of this segment
                    return (Waypoints[i] - Waypoints[i - 1]).normalized;
                }

                currentDistance += segmentLength;
            }

            return (Waypoints[Waypoints.Count - 1] - Waypoints[Waypoints.Count - 2]).normalized;
        }

        /// <summary>
        /// Check if a track piece is part of this path
        /// </summary>
        /// <param name="track">The track piece to check</param>
        /// <returns>True if the track is in this path</returns>
        public bool ContainsTrack(TrackPiece track)
        {
            return TrackPieces.Contains(track);
        }
        
        /// <summary>
        /// Build a path from a list of ordered track pieces
        /// </summary>
        public void BuildFromTrackList(List<TrackPiece> trackPieces)
        {
            Clear();
            TrackPieces.AddRange(trackPieces);
    
            GenerateWaypoints();
            CalculateTotalLength();
            CheckIfLoop();
    
            Debug.Log($"[TrackPath] Built path {PathID} with {TrackPieces.Count} tracks and {Waypoints.Count} waypoints");
        }
    }
}