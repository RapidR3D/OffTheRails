using System.Collections.Generic;
using UnityEngine;

namespace OffTheRails.NPCs
{
    public class WaypointPath : MonoBehaviour
    {
        public List<Transform> waypoints = new List<Transform>();
        public Color gizmoColor = Color.yellow;
        public float gizmoRadius = 0.3f;

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;

                Gizmos.DrawSphere(waypoints[i].position, gizmoRadius);

                if (i < waypoints.Count - 1 && waypoints[i+1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
                }
            }
        }
        
        // Helper to auto-populate from children
        [ContextMenu("Populate From Children")]
        public void PopulateFromChildren()
        {
            waypoints.Clear();
            foreach (Transform child in transform)
            {
                waypoints.Add(child);
            }
        }
    }
}
