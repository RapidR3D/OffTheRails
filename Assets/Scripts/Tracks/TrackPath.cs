using System.Collections.Generic;
using UnityEngine;

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

            // Use breadth-first search to find all connected tracks
            HashSet<TrackPiece> visited = new HashSet<TrackPiece>();
            Queue<TrackPiece> toVisit = new Queue<TrackPiece>();

            toVisit.Enqueue(startTrack);
            visited.Add(startTrack);

            while (toVisit.Count > 0)
            {
                TrackPiece current = toVisit.Dequeue();
                TrackPieces.Add(current);

                // Visit connected tracks
                foreach (var connected in current.GetConnectedTracks())
                {
                    if (!visited.Contains(connected))
                    {
                        visited.Add(connected);
                        toVisit.Enqueue(connected);
                    }
                }
            }

            // Generate waypoints from the track pieces
            GenerateWaypoints();
            
            // Calculate total length
            CalculateTotalLength();

            // Check if this is a loop
            CheckIfLoop();

            Debug.Log($"Built path {PathID} with {TrackPieces.Count} tracks and {Waypoints.Count} waypoints");
        }

        /// <summary>
        /// Add a track piece to this path
        /// </summary>
        /// <param name="track">The track piece to add</param>
        public void AddTrack(TrackPiece track)
        {
            if (track == null || TrackPieces.Contains(track))
                return;

            TrackPieces.Add(track);
            RegenerateWaypoints();
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
            RegenerateWaypoints();
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
        /// Regenerate waypoints and recalculate properties
        /// </summary>
        public void RegenerateWaypoints()
        {
            GenerateWaypoints();
            CalculateTotalLength();
            CheckIfLoop();
        }

        /// <summary>
        /// Generate waypoints from track pieces
        /// </summary>
        private void GenerateWaypoints()
        {
            Waypoints.Clear();

            if (TrackPieces.Count == 0)
                return;

            // For a simple connected path, we need to order the tracks correctly
            // and combine their waypoints without duplicating connection points

            // Start with the first track
            TrackPiece currentTrack = TrackPieces[0];
            HashSet<TrackPiece> processed = new HashSet<TrackPiece>();
            List<TrackPiece> orderedTracks = new List<TrackPiece>();

            // Build ordered list of tracks
            orderedTracks.Add(currentTrack);
            processed.Add(currentTrack);

            while (orderedTracks.Count < TrackPieces.Count)
            {
                bool foundNext = false;

                foreach (var connected in currentTrack.GetConnectedTracks())
                {
                    if (!processed.Contains(connected) && TrackPieces.Contains(connected))
                    {
                        orderedTracks.Add(connected);
                        processed.Add(connected);
                        currentTrack = connected;
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext)
                {
                    // No more connected tracks in sequence, might be a branch or disconnected
                    // Add remaining unprocessed tracks
                    foreach (var track in TrackPieces)
                    {
                        if (!processed.Contains(track))
                        {
                            orderedTracks.Add(track);
                            processed.Add(track);
                            currentTrack = track;
                            break;
                        }
                    }
                }
            }

            // Combine waypoints from ordered tracks
            for (int i = 0; i < orderedTracks.Count; i++)
            {
                Vector2[] trackWaypoints = orderedTracks[i].WorldWaypoints;
                
                if (trackWaypoints.Length == 0)
                    continue;

                // For the first track, add all waypoints
                if (i == 0)
                {
                    Waypoints.AddRange(trackWaypoints);
                }
                else
                {
                    // For subsequent tracks, check if we should reverse waypoints
                    // and skip the first waypoint if it's close to the last one (connection point)
                    Vector2 lastWaypoint = Waypoints[Waypoints.Count - 1];
                    Vector2 firstOfNew = trackWaypoints[0];
                    Vector2 lastOfNew = trackWaypoints[trackWaypoints.Length - 1];

                    // Check which end of the new track connects to the last waypoint
                    float distToFirst = Vector2.Distance(lastWaypoint, firstOfNew);
                    float distToLast = Vector2.Distance(lastWaypoint, lastOfNew);

                    if (distToLast < distToFirst)
                    {
                        // Reverse the waypoints
                        for (int j = trackWaypoints.Length - 2; j >= 0; j--)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                    else
                    {
                        // Add waypoints in order, skipping the first if it's a duplicate
                        for (int j = (distToFirst < 0.1f ? 1 : 0); j < trackWaypoints.Length; j++)
                        {
                            Waypoints.Add(trackWaypoints[j]);
                        }
                    }
                }
            }
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
    }
}
