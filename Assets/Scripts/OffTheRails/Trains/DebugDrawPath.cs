using UnityEngine;
using OffTheRails.Tracks;

public class DebugDrawPath : MonoBehaviour
{
    [SerializeField] private int pathIndex = 0;
    [SerializeField] private Color pathColor = Color.cyan;
    [SerializeField] private float waypointRadius = 0.3f;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (TrackManager.Instance == null) return;

        var paths = TrackManager.Instance.GetPaths();
        if (pathIndex >= paths.Count) return;

        var path = paths[pathIndex];

        // Draw waypoints as spheres
        Gizmos.color = pathColor;
        for (int i = 0; i < path.Waypoints.Count; i++)
        {
            Gizmos.DrawSphere(path.Waypoints[i], waypointRadius);
            
            // Draw waypoint number
#if UNITY_EDITOR
            UnityEditor.Handles.Label(path.Waypoints[i], i.ToString());
#endif
        }

        // Draw lines between waypoints
        for (int i = 1; i < path.Waypoints.Count; i++)
        {
            Gizmos.DrawLine(path.Waypoints[i - 1], path.Waypoints[i]);
        }
    }
}