using UnityEngine;
using OffTheRails.Tracks;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Represents a connection point on a track piece where tracks can snap together. 
    /// Handles snap detection, validation, and visual feedback.
    /// </summary>
    public class ConnectionPoint : MonoBehaviour
    {
        [Header("Connection Settings")]
        [Tooltip("Direction this connection point faces (in local space)")]
        [SerializeField] private Vector2 direction = Vector2.right;
        
        [Tooltip("Radius within which this point can snap to other connection points")]
        [SerializeField] private float snapRadius = 0.5f;
        
        [Tooltip("Tolerance for direction alignment (0-1, where 1 is exact opposite)")]
        [SerializeField] private float directionTolerance = 0.9f;
        
        [Header("Visual Feedback")]
        [Tooltip("Color when connection is available")]
        [SerializeField] private Color availableColor = Color.green;
        
        [Tooltip("Color when connection is not available")]
        [SerializeField] private Color unavailableColor = Color.red;
        
        [Tooltip("Size of the gizmo sphere")]
        [SerializeField] private float gizmoSize = 0.2f;

        /// <summary>
        /// The connection point this is currently connected to
        /// </summary>
        public ConnectionPoint ConnectedTo { get; private set; }

        /// <summary>
        /// The track piece this connection point belongs to
        /// </summary>
        public TrackPiece ParentTrack { get; private set; }

        /// <summary>
        /// Whether this connection point is currently connected
        /// </summary>
        public bool IsConnected => ConnectedTo != null;

        /// <summary>
        /// World space position of this connection point
        /// </summary>
        public Vector2 WorldPosition => transform.position;

        /// <summary>
        /// World space direction of this connection point
        /// </summary>
        public Vector2 WorldDirection
        {
            get
            {
                // Convert local direction to world space using the transform's rotation
                float angle = transform.eulerAngles.z * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                return new Vector2(
                    direction.x * cos - direction.y * sin,
                    direction.x * sin + direction.y * cos
                ).normalized;
            }
        }

        /// <summary>
        /// Snap radius for this connection point
        /// </summary>
        public float SnapRadius => snapRadius;

        private void Awake()
        {
            ParentTrack = GetComponentInParent<TrackPiece>();
            if (ParentTrack == null)
            {
                Debug.LogError($"ConnectionPoint on {gameObject.name} has no TrackPiece parent!", this);
            }
        }

        /// <summary>
        /// Check if this connection point can connect to another connection point
        /// </summary>
        /// <param name="other">The other connection point to check</param>
        /// <returns>True if connection is valid</returns>
        public bool CanConnectTo(ConnectionPoint other, bool skipDirectionCheck = false)
        {
            if (other == null || other == this)
                return false;

            // Can't connect if already connected
            if (IsConnected || other.IsConnected)
                return false;

            // Can't connect to same track piece
            if (other.ParentTrack == ParentTrack)
                return false;

            // Check if within snap radius
            float distance = Vector2.Distance(WorldPosition, other.WorldPosition);
            if (distance > snapRadius)
            {
                Debug.Log($"CanConnectTo FAIL: Distance {distance:F3} > {snapRadius}");
                return false;
            }
            
            // skip direction check if requested (after alignment calculation)
            if (skipDirectionCheck)
            {
                Debug.Log($"CanConnectTo SUCCESS: Direction check skipped, distance OK ({distance:F3})");
                return true;
            }
                

            // Check if directions are opposite (or nearly opposite)
            Vector2 thisDir = WorldDirection;
            Vector2 otherDir = other.WorldDirection;
            float dot = Vector2.Dot(thisDir, otherDir);
            
            bool directionsOK = dot <= -directionTolerance;
            
            Debug.Log($"CanConnectTo: Distance={distance:F3}, Dot={dot:F3}, Threshold={-directionTolerance:F3}, Result={directionsOK}");

            return directionsOK;
        }

        /// <summary>
        /// Establish a connection between this point and another
        /// </summary>
        /// <param name="other">The connection point to connect to</param>
        public void ConnectTo(ConnectionPoint other)
        {
            // Don't check CanConnectTo here if we're being called after alignment
            // The caller should have already validated
    
            if (other == null || other == this)
            {
                Debug.LogWarning($"Cannot connect: invalid target");
                return;
            }

            if (IsConnected || other.IsConnected)
            {
                Debug.LogWarning($"Cannot connect: already connected");
                return;
            }
            
            // Safety check: verify they're actually close
            float distance = Vector2.Distance(WorldPosition, other.WorldPosition);
            if (distance > 0.5f)
            {
                Debug.LogWarning($"Cannot connect {gameObject.name} to {other.gameObject.name}: too far apart ({distance:F3} units)");
                return;
            }

            ConnectedTo = other;
            other.ConnectedTo = this;

            Debug.Log($"✓ Connected {ParentTrack.gameObject.name} to {other.ParentTrack.gameObject.name}");
        }

        /// <summary>
        /// Disconnect this connection point
        /// </summary>
        public void Disconnect()
        {
            if (ConnectedTo != null)
            {
                ConnectionPoint other = ConnectedTo;
                ConnectedTo = null;
                other.ConnectedTo = null;

                Debug.Log($"Disconnected {ParentTrack.gameObject.name} from {other.ParentTrack.gameObject.name}");
            }
        }

        /// <summary>
        /// Find the nearest valid connection point within snap radius
        /// </summary>
        /// <returns>The nearest valid connection point, or null if none found</returns>
        public ConnectionPoint FindNearestValidConnection()
        {
            if (TrackManager.Instance == null)
                return null;

            ConnectionPoint nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var track in TrackManager.Instance.GetAllTracks())
            {
                if (track == ParentTrack)
                    continue;

                foreach (var point in track.ConnectionPoints)
                {
                    if (CanConnectTo(point))
                    {
                        float distance = Vector2.Distance(WorldPosition, point.WorldPosition);
                        if (distance < nearestDistance)
                        {
                            nearest = point;
                            nearestDistance = distance;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Calculate the position and rotation needed to align with another connection point
        /// </summary>
        /// <param name="target">The target connection point to align with</param>
        /// <param name="position">Output: The position for the parent track</param>
        /// <param name="rotation">Output: The rotation for the parent track</param>
        /// <returns>True if alignment was calculated successfully</returns>
        public bool CalculateAlignmentTo(ConnectionPoint target, out Vector3 position, out Quaternion rotation)
{
    position = Vector3.zero;
    rotation = Quaternion.identity;

    if (target == null || ParentTrack == null)
        return false;

    // Get the target connection point's world direction
    Vector2 targetDir = target.WorldDirection;
    
    // We want THIS connection point to face OPPOSITE to the target
    Vector2 desiredWorldDir = -targetDir;
    
    // Current direction of this connection point in world space
    Vector2 currentWorldDir = WorldDirection;
    
    // Calculate the angle between current direction and desired direction
    float currentAngle = Mathf.Atan2(currentWorldDir.y, currentWorldDir.x) * Mathf.Rad2Deg;
    float desiredAngle = Mathf.Atan2(desiredWorldDir.y, desiredWorldDir.x) * Mathf.Rad2Deg;
    float rotationDelta = desiredAngle - currentAngle;
    
    // Apply this rotation delta to the parent track's current rotation
    float newRotation = ParentTrack.transform.eulerAngles.z + rotationDelta;
    rotation = Quaternion.Euler(0, 0, newRotation);

    // Now calculate position
    Vector2 targetPos = target.WorldPosition;
    
    // Calculate where our parent track needs to be
    Vector2 localOffset = transform.localPosition;
    
    // Rotate this local offset by the new rotation
    float angleRad = newRotation * Mathf.Deg2Rad;
    float cos = Mathf.Cos(angleRad);
    float sin = Mathf.Sin(angleRad);
    Vector2 rotatedOffset = new Vector2(
        localOffset.x * cos - localOffset.y * sin,
        localOffset.x * sin + localOffset.y * cos
    );
    
    // Parent position = target position - rotated offset
    position = new Vector3(
        targetPos.x - rotatedOffset.x, 
        targetPos.y - rotatedOffset.y, 
        ParentTrack.transform.position.z
    );

    // DEBUG VISUALIZATION
    Debug.DrawLine(WorldPosition, target.WorldPosition, Color.yellow, 2f);
    Debug.DrawLine(position, targetPos, Color.cyan, 2f);
    Debug.Log($"[CalculateAlignment] {gameObject.name} → {target.gameObject.name}");
    Debug.Log($"Target pos: {targetPos}, Our current pos: {WorldPosition}");
    Debug.Log($"Local offset: {localOffset}, Rotated offset: {rotatedOffset}");
    Debug.Log($"Final parent position: {position}");
    Debug.Log($"Rotation: {ParentTrack.transform.eulerAngles.z:F1}° → {newRotation:F1}°");

    return true;
}

        private void OnDrawGizmos()
        {
            // Draw connection point
            Color color = IsConnected ? unavailableColor : availableColor;
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, gizmoSize);

            // Draw direction arrow
            Vector2 worldDir = WorldDirection;
            Vector3 start = transform.position;
            Vector3 end = start + new Vector3(worldDir.x, worldDir.y, 0) * (gizmoSize * 3);
            
            Gizmos.color = color;
            Gizmos.DrawLine(start, end);
            
            // Draw arrow head
            Vector3 right = Quaternion.Euler(0, 0, 30) * (start - end).normalized * gizmoSize;
            Vector3 left = Quaternion.Euler(0, 0, -30) * (start - end).normalized * gizmoSize;
            Gizmos.DrawLine(end, end + right);
            Gizmos.DrawLine(end, end + left);

            // Draw snap radius
            if (!IsConnected)
            {
                Gizmos.color = new Color(color.r, color.g, color.b, 0.2f);
                DrawCircle(transform.position, snapRadius, 20);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw enhanced gizmos when selected
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.5f);
        }

        public ConnectionPoint FindNearestConnectionForSnapping(float searchRadius = 3.0f)
        {
            if (TrackManager.Instance == null)
                return null;

            ConnectionPoint nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var track in TrackManager.Instance.GetAllTracks())
            {
                if (track == ParentTrack)
                    continue;

                foreach (var point in track.ConnectionPoints)
                {
                    // Skip same parent track
                    if (point.ParentTrack == ParentTrack)
                        continue;

                    // Check distance only (no direction check, no "already connected" check)
                    float distance = Vector2.Distance(WorldPosition, point.WorldPosition);
            
                    if (distance < searchRadius && distance < nearestDistance)
                    {
                        nearest = point;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }
#endif
    }
}