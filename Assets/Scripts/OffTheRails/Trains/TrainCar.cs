using UnityEngine;
using Sirenix.OdinInspector;
using OffTheRails.Tracks;

namespace OffTheRails.Trains
{
    /// <summary>
    /// A train car that follows behind a locomotive or another car.
    /// Cars sample the same path as the locomotive but at an offset distance.
    /// </summary>
    public class TrainCar : MonoBehaviour
    {
        [Title("Visual Settings")]
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private float rotationOffset = 0f;
        
        [Title("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.yellow;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private float distanceAlongPath = 0f;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private TrackPath currentPath;
        
        /// <summary>
        /// Update the car's position along the path
        /// </summary>
        public void UpdatePosition(TrackPath path, float distance)
        {
            currentPath = path;
            distanceAlongPath = distance;
            
            if (path == null || path.Waypoints.Count == 0)
                return;
            
            // Clamp distance to valid range
            float clampedDistance = Mathf.Max(0f, distance);
            
            // If we're "before" the start of the path, stay at start
            if (clampedDistance <= 0f)
            {
                clampedDistance = 0f;
            }
            
            // Get position from path
            Vector2 newPosition = path.GetPositionAtDistance(clampedDistance);
            transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);
            
            // Update rotation to face direction of travel
            if (rotateToDirection)
            {
                Vector2 direction = path.GetDirectionAtDistance(clampedDistance);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
            }
        }
        
        /// <summary>
        /// Get current world position
        /// </summary>
        public Vector2 GetPosition() => transform.position;
        
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
