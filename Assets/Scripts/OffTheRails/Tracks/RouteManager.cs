using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using OffTheRails.Trains;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Manages routes and provides route-based pathfinding for trains.
    /// </summary>
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }
        
        [Title("Route Definitions")]
        [InfoBox("Add all route definitions for this track layout")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        [SerializeField] private List<RouteDefinition> routes = new List<RouteDefinition>();
        
        [Title("Debug")]
        [SerializeField] private bool showRouteGizmos = true;
        [SerializeField] private float gizmoWaypointRadius = 0.5f;
        
        // Cached paths for each route
        private Dictionary<RouteDefinition, TrackPath> routePaths = new Dictionary<RouteDefinition, TrackPath>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            BuildAllRoutePaths();
        }
        
        /// <summary>
        /// Build TrackPath objects for all defined routes
        /// </summary>
        [Button("Build All Route Paths"), GUIColor(0, 1, 0)]
        public void BuildAllRoutePaths()
        {
            routePaths.Clear();
            
            foreach (var route in routes)
            {
                if (route == null) continue;
                
                var path = BuildPathForRoute(route);
                if (path != null)
                {
                    routePaths[route] = path;
                    // Debug.Log($"[RouteManager] Built path for route '{route.routeName}': {path.Waypoints.Count} waypoints, length={path.TotalLength:F1}");
                }
            }
            
            // Debug.Log($"[RouteManager] Built {routePaths.Count} route paths");
        }
        
        /// <summary>
        /// Build a TrackPath from a RouteDefinition
        /// </summary>
        private TrackPath BuildPathForRoute(RouteDefinition route)
        {
            if (route.segments.Count == 0)
            {
                Debug.LogWarning($"[RouteManager] Route '{route.routeName}' has no segments");
                return null;
            }
            
            TrackPath path = new TrackPath();
            
            // Manually build the path from the route segments
            List<TrackPiece> tracks = new List<TrackPiece>();
            foreach (var seg in route.segments)
            {
                if (seg.track != null)
                    tracks.Add(seg.track);
            }
            
            if (tracks.Count == 0)
            {
                Debug.LogWarning($"[RouteManager] Route '{route.routeName}' has no valid tracks");
                return null;
            }
            
            // Build path with explicit track list and junction info
            path.BuildFromRouteSegments(route.segments);
            
            return path;
        }
        
        /// <summary>
        /// Get the path for a specific route
        /// </summary>
        public TrackPath GetPathForRoute(RouteDefinition route)
        {
            if (routePaths.TryGetValue(route, out var path))
                return path;
            return null;
        }
        
        /// <summary>
        /// Get the path for a route by name
        /// </summary>
        public TrackPath GetPathForRoute(string routeName)
        {
            foreach (var kvp in routePaths)
            {
                if (kvp.Key.routeName == routeName)
                    return kvp.Value;
            }
            return null;
        }
        
        /// <summary>
        /// Assign a train to a specific route
        /// </summary>
        /// <param name="train">The train to assign</param>
        /// <param name="route">The route to follow</param>
        /// <param name="forwardDirection">If true, travel in segment order. If false, travel in reverse.</param>
        public bool AssignTrainToRoute(Train train, RouteDefinition route, bool forwardDirection = true)
        {
            if (train == null || route == null)
            {
                Debug.LogError("[RouteManager] Train or route is null");
                return false;
            }
            
            // Check switch requirements
            if (!route.AreSwitchRequirementsMet())
            {
                Debug.LogWarning($"[RouteManager] Switch requirements not met for route '{route.routeName}'. Setting switches...");
                route.SetSwitchesForRoute();
            }
            
            // Get or build the path
            if (!routePaths.TryGetValue(route, out var path))
            {
                path = BuildPathForRoute(route);
                if (path != null)
                    routePaths[route] = path;
            }
            
            if (path == null || path.Waypoints.Count == 0)
            {
                Debug.LogError($"[RouteManager] No valid path for route '{route.routeName}'");
                return false;
            }
            
            // If reverse direction, create a reversed copy of the path
            TrackPath pathToUse = path;
            if (!forwardDirection)
            {
                // Create a new path with reversed waypoints
                pathToUse = new TrackPath();
                pathToUse.BuildFromRouteSegments(route.segments);
                pathToUse.Reverse();
                // Debug.Log($"[RouteManager] Reversed path for route '{route.routeName}'");
            }
            
            train.SetPath(pathToUse, 0f);
            
            string direction = forwardDirection ? "forward" : "reverse";
            // Debug.Log($"[RouteManager] Assigned train '{train.name}' to route '{route.routeName}' ({direction})");
            return true;
        }
        
        /// <summary>
        /// Assign a train to a route by name
        /// </summary>
        public bool AssignTrainToRoute(Train train, string routeName, bool forwardDirection = true)
        {
            var route = routes.Find(r => r.routeName == routeName);
            if (route == null)
            {
                Debug.LogError($"[RouteManager] Route '{routeName}' not found");
                return false;
            }
            return AssignTrainToRoute(train, route, forwardDirection);
        }
        
        /// <summary>
        /// Get all defined routes
        /// </summary>
        public List<RouteDefinition> GetAllRoutes() => routes;
        
        /// <summary>
        /// Find routes that go from one track to another
        /// </summary>
        public List<RouteDefinition> FindRoutesFromTo(TrackPiece from, TrackPiece to)
        {
            List<RouteDefinition> validRoutes = new List<RouteDefinition>();
            
            foreach (var route in routes)
            {
                if (route.segments.Count < 2) continue;
                
                var firstTrack = route.segments[0].track;
                var lastTrack = route.segments[route.segments.Count - 1].track;
                
                if ((firstTrack == from && lastTrack == to) ||
                    (firstTrack == to && lastTrack == from))
                {
                    validRoutes.Add(route);
                }
            }
            
            return validRoutes;
        }
        
        private void OnDrawGizmos()
        {
            if (!showRouteGizmos) return;
            
            foreach (var kvp in routePaths)
            {
                var route = kvp.Key;
                var path = kvp.Value;
                
                if (route == null || path == null || path.Waypoints.Count == 0) continue;
                
                Gizmos.color = route.gizmoColor;
                
                // Draw waypoints
                for (int i = 0; i < path.Waypoints.Count; i++)
                {
                    Gizmos.DrawSphere(path.Waypoints[i], gizmoWaypointRadius);
                    
                    if (i < path.Waypoints.Count - 1)
                    {
                        Gizmos.DrawLine(path.Waypoints[i], path.Waypoints[i + 1]);
                    }
                }
            }
        }
    }
}
