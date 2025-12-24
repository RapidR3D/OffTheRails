using OffTheRails.Tracks;
using UnityEngine;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Train that follows a TrackPath. When paths are rebuilt (due to switch changes),
    /// the train is automatically repositioned on its new path.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Train : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] protected float speed = 5f;
        [SerializeField] protected Tracks.TrackPath currentPath;
        [SerializeField] protected float distanceAlongPath = 0f;
        [SerializeField] protected bool despawnAtEnd = true;

        [Header("Visual Settings")]
        [SerializeField] protected bool rotateToDirection = true;
        [SerializeField] protected float rotationOffset = 0f;

        private Train trainRD;
       
        public float Speed => speed;
        public Tracks.TrackPath CurrentPath => currentPath;
        public float DistanceAlongPath => distanceAlongPath;
        public bool IsActive { get; protected set; }
        
        public RouteDefinition RouteMR;

        protected virtual void Start()
        {
            IsActive = true;
            
            if (currentPath != null)
            {
                Debug.Log($"[Train {name}] Starting with path: {currentPath.TrackPieces.Count} tracks, {currentPath.Waypoints.Count} waypoints");
            }

            RouteManager.Instance.AssignTrainToRoute(trainRD, "RouteMR", true);
        }

        protected virtual void Update()
        {
            if (!IsActive || currentPath == null || currentPath.Waypoints.Count == 0)
                return;

            MovementUpdate();
        }

        protected virtual void MovementUpdate()
        {
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

            // Update rotation
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
        public virtual void SetPath(Tracks.TrackPath path, float startDistance = 0f)
        {
            currentPath = path;
            
            if (path == null || path.Waypoints.Count == 0)
            {
                Debug.LogWarning($"[Train {name}] SetPath called with invalid path");
                return;
            }
            
            distanceAlongPath = Mathf.Clamp(startDistance, 0f, path.TotalLength);
            IsActive = true;

            // Set position
            Vector2 startPosition = path.GetPositionAtDistance(distanceAlongPath);
            transform.position = new Vector3(startPosition.x, startPosition.y, transform.position.z);

            // Set rotation
            if (rotateToDirection)
            {
                Vector2 direction = path.GetDirectionAtDistance(distanceAlongPath);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
            }
            
            Debug.Log($"[Train {name}] SetPath: {path.Waypoints.Count} waypoints, starting at distance {distanceAlongPath:F1}/{path.TotalLength:F1}");
        }

        protected virtual void OnReachedPathEnd()
        {
            Debug.Log($"[Train {name}] Reached end of path");
            
            if (despawnAtEnd)
            {
                Despawn();
            }
            else
            {
                IsActive = false;
            }
        }

        public virtual void Stop() => IsActive = false;
        public virtual void Resume() => IsActive = true;
        public virtual void SetSpeed(float newSpeed) => speed = Mathf.Max(0, newSpeed);
        public virtual void Despawn() => Destroy(gameObject);
        
        public Vector2 GetPosition() => transform.position;
        
        public Vector2 GetDirection()
        {
            if (currentPath != null && currentPath.Waypoints.Count >= 2)
                return currentPath.GetDirectionAtDistance(distanceAlongPath);
            return Vector2.right;
        }

        public TrackPath GetCurrentPath() => currentPath;

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (currentPath != null && Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, 0.3f);

                Vector2 direction = GetDirection();
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction * 0.5f);
            }
        }
#endif
    }
}
