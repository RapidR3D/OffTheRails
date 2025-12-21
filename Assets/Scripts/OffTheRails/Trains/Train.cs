using OffTheRails.Tracks;
using UnityEngine;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Base class for all train entities.  Handles movement along a TrackPath.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Train : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Speed of the train in units per second")]
        [SerializeField] protected float speed = 5f;
        
        [Tooltip("Current track path this train is following")]
        [SerializeField] protected Tracks.TrackPath currentPath;
        
        [Tooltip("Distance traveled along the current path")]
        [SerializeField] protected float distanceAlongPath = 0f;
        
        [Tooltip("Should the train be destroyed when reaching the end of the path?")]
        [SerializeField] protected bool despawnAtEnd = true;

        [Header("Visual Settings")]
        [Tooltip("Should the train rotate to face movement direction?")]
        [SerializeField] protected bool rotateToDirection = true;
        
        [Tooltip("Offset rotation in degrees (use if sprite faces wrong direction)")]
        [SerializeField] protected float rotationOffset = 0f;

        /// <summary>
        /// Current speed of the train
        /// </summary>
        public float Speed => speed;

        /// <summary>
        /// Current path the train is following
        /// </summary>
        public Tracks.TrackPath CurrentPath => currentPath;

        /// <summary>
        /// Distance traveled along the current path
        /// </summary>
        public float DistanceAlongPath => distanceAlongPath;

        /// <summary>
        /// Whether this train is active and moving
        /// </summary>
        public bool IsActive { get; protected set; }

        protected virtual void Start()
        {
            IsActive = true;
        }

        protected virtual void Update()
        {
            if (!IsActive || currentPath == null)
                return;

            MovementUpdate();
        }

        /// <summary>
        /// Handle movement along the path
        /// </summary>
        protected virtual void MovementUpdate()
        {
            // Move along the path
            distanceAlongPath += speed * Time.deltaTime;

            // Check if we've reached the end
            if (distanceAlongPath >= currentPath.TotalLength)
            {
                OnReachedPathEnd();
                return;
            }

            // Update position
            Vector2 newPosition = currentPath.GetPositionAtDistance(distanceAlongPath);
            transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);

            // Update rotation to face movement direction
            if (rotateToDirection)
            {
                Vector2 direction = currentPath.GetDirectionAtDistance(distanceAlongPath);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
            }
        }

        /// <summary>
        /// Set the path for this train to follow
        /// </summary>
        /// <param name="path">The TrackPath to follow</param>
        /// <param name="startDistance">Starting distance along the path (default: 0)</param>
        public virtual void SetPath(Tracks.TrackPath path, float startDistance = 0f)
        {
            currentPath = path;
            distanceAlongPath = startDistance;
            IsActive = true;

            if (path != null)
            {
                // Set initial position
                Vector2 startPosition = path.GetPositionAtDistance(distanceAlongPath);
                transform.position = new Vector3(startPosition.x, startPosition.y, transform.position.z);

                // Set initial rotation
                if (rotateToDirection)
                {
                    Vector2 direction = path.GetDirectionAtDistance(distanceAlongPath);
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
                }
            }
        }

        /// <summary>
        /// Called when the train reaches the end of its path
        /// </summary>
        protected virtual void OnReachedPathEnd()
        {
            if (despawnAtEnd)
            {
                Despawn();
            }
            else
            {
                IsActive = false;
            }
        }

        /// <summary>
        /// Stop the train's movement
        /// </summary>
        public virtual void Stop()
        {
            IsActive = false;
        }

        /// <summary>
        /// Resume the train's movement
        /// </summary>
        public virtual void Resume()
        {
            IsActive = true;
        }

        /// <summary>
        /// Set the train's speed
        /// </summary>
        public virtual void SetSpeed(float newSpeed)
        {
            speed = Mathf.Max(0, newSpeed);
        }

        /// <summary>
        /// Destroy this train
        /// </summary>
        public virtual void Despawn()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// Get the current world position of the train
        /// </summary>
        public Vector2 GetPosition()
        {
            return transform.position;
        }

        /// <summary>
        /// Get the current movement direction
        /// </summary>
        public Vector2 GetDirection()
        {
            if (currentPath != null)
            {
                return currentPath.GetDirectionAtDistance(distanceAlongPath);
            }
            return Vector2.right;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (currentPath != null && Application.isPlaying)
            {
                // Draw current position on train path
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, 0.3f);

                // Draw direction
                Vector2 direction = GetDirection();
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction * 0.5f);
            }
        }
#endif
        /// <summary>
        /// Get the current path (for external systems to check)
        /// </summary>
        public TrackPath GetCurrentPath()
        {
            return currentPath;
        }

        /// <summary>
        /// Update the path reference (when paths are regenerated)
        /// </summary>
        public void UpdatePath(TrackPath newPath)
        {
            if (newPath == null)
                return;
        
            // Keep current distance along path
            float currentDistance = distanceAlongPath;
    
            currentPath = newPath;
    
            // Try to maintain position (but clamp to new path length)
            distanceAlongPath = Mathf.Clamp(currentDistance, 0f, newPath.TotalLength);
    
            Debug.Log($"Train {name} updated to new path (distance maintained: {distanceAlongPath:F1})");
        }
    }
}