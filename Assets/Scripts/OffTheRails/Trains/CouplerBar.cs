using UnityEngine;
using Sirenix.OdinInspector;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Visual coupler bar that stretches between two train cars.
    /// Attach this to a sprite object (a simple rectangle/bar).
    /// </summary>
    public class CouplerBar : MonoBehaviour
    {
        [Title("Connected Cars")]
        [Required]
        [Tooltip("The car in front (the bar connects to its rear)")]
        [SerializeField] private Transform frontCar;
        
        [Required]
        [Tooltip("The car behind (the bar connects to its front)")]
        [SerializeField] private Transform rearCar;
        
        [Title("Coupler Offsets")]
        [Tooltip("Offset from front car's center to its rear coupler (usually negative X)")]
        [SerializeField] private Vector2 frontCarRearOffset = new Vector2(-1f, 0f);
        
        [Tooltip("Offset from rear car's center to its front coupler (usually positive X)")]
        [SerializeField] private Vector2 rearCarFrontOffset = new Vector2(1f, 0f);
        
        [Title("Bar Settings")]
        [Tooltip("Width of the coupler bar sprite (height in local space)")]
        [SerializeField] private float barWidth = 0.2f;
        
        [Tooltip("Minimum length to prevent bar from disappearing")]
        [SerializeField] private float minLength = 0.1f;
        
        private SpriteRenderer spriteRenderer;
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        private void LateUpdate()
        {
            if (frontCar == null || rearCar == null) return;
            
            // Get world positions of coupler points
            Vector2 frontCouplerPos = GetWorldCouplerPosition(frontCar, frontCarRearOffset);
            Vector2 rearCouplerPos = GetWorldCouplerPosition(rearCar, rearCarFrontOffset);
            
            // Position bar at midpoint
            Vector2 midpoint = (frontCouplerPos + rearCouplerPos) / 2f;
            transform.position = new Vector3(midpoint.x, midpoint.y, transform.position.z);
            
            // Calculate direction and length
            Vector2 direction = rearCouplerPos - frontCouplerPos;
            float length = Mathf.Max(direction.magnitude, minLength);
            
            // Rotate to face the direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            
            // Scale to stretch between the two points
            // Assuming the sprite is 1 unit wide by default
            transform.localScale = new Vector3(length, barWidth, 1f);
        }
        
        /// <summary>
        /// Get world position of a coupler point, accounting for car rotation
        /// </summary>
        private Vector2 GetWorldCouplerPosition(Transform car, Vector2 localOffset)
        {
            // Transform local offset by car's rotation
            Vector3 worldOffset = car.TransformDirection(new Vector3(localOffset.x, localOffset.y, 0f));
            return (Vector2)car.position + (Vector2)worldOffset;
        }
        
        private void OnDrawGizmosSelected()
        {
            if (frontCar == null || rearCar == null) return;
            
            Vector2 frontCouplerPos = GetWorldCouplerPosition(frontCar, frontCarRearOffset);
            Vector2 rearCouplerPos = GetWorldCouplerPosition(rearCar, rearCarFrontOffset);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(frontCouplerPos, 0.1f);
            Gizmos.DrawSphere(rearCouplerPos, 0.1f);
            Gizmos.DrawLine(frontCouplerPos, rearCouplerPos);
        }
    }
}