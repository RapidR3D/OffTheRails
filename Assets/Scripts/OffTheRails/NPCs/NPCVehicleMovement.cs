using UnityEngine;

namespace OffTheRails.NPCs
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class NPCVehicleMovement : MonoBehaviour
    {
        public float speed = 5;
        private float startSpeed;
        //[SerializeField] private GameObject breakLights;
        [SerializeField] private AudioClip carHornSound;
        private Rigidbody2D rb;
        private bool shouldBrake = false;
        public float startPos;
        public float endPos;
        public bool isMovingRight = true; // For horizontal movement
        public bool isMovingHorizontal = true; // True if moving along the X-axis, false for Y-axis

        [Header("Pathing")]
        public WaypointPath waypointPath;
        private int currentWaypointIndex = 0;
        private bool isUsingWaypoints = false;

        void Start()
        {
            //Destroy(GetComponent<CarCollisionAlarm>());
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError("Rigidbody2D component is missing on " + gameObject.name);
                enabled = false;
                return;
            }
            rb.freezeRotation = true;

            // Check if we are using waypoints
            if (waypointPath != null && waypointPath.waypoints.Count > 0)
            {
                isUsingWaypoints = true;
                // Snap to first waypoint
                transform.position = waypointPath.waypoints[0].position;
                currentWaypointIndex = 0;
            }
            else
            {
                // Set start position based on movement direction
                startPos = isMovingHorizontal ? transform.position.x : transform.position.y;
                
                // Validate and Auto-Fix configuration
                if (isMovingHorizontal)
                {
                    if (isMovingRight && endPos <= startPos)
                    {
                        Debug.LogWarning($"{gameObject.name}: Moving Right but EndPos ({endPos}) <= StartPos ({startPos}). Auto-fixing EndPos to StartPos + 50.");
                        endPos = startPos + 50f;
                    }
                    else if (!isMovingRight && endPos >= startPos)
                    {
                        Debug.LogWarning($"{gameObject.name}: Moving Left but EndPos ({endPos}) >= StartPos ({startPos}). Auto-fixing EndPos to StartPos - 50.");
                        endPos = startPos - 50f;
                    }
                }
                else
                {
                    if (isMovingRight && endPos <= startPos)
                    {
                        Debug.LogWarning($"{gameObject.name}: Moving Up but EndPos ({endPos}) <= StartPos ({startPos}). Auto-fixing EndPos to StartPos + 50.");
                        endPos = startPos + 50f;
                    }
                    else if (!isMovingRight && endPos >= startPos)
                    {
                        Debug.LogWarning($"{gameObject.name}: Moving Down but EndPos ({endPos}) >= StartPos ({startPos}). Auto-fixing EndPos to StartPos - 50.");
                        endPos = startPos - 50f;
                    }
                }
            }

            startSpeed = speed;
            
            // Disable gravity for top-down movement
            rb.gravityScale = 0;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 start = transform.position;
            Vector3 end = transform.position;

            if (isMovingHorizontal)
            {
                start.x = startPos;
                end.x = endPos;
            }
            else
            {
                start.y = startPos;
                end.y = endPos;
            }

            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(start, 0.2f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(end, 0.2f);
        }

        void FixedUpdate()
        {
            if (isUsingWaypoints)
            {
                HandleWaypointMovement();
            }
            else
            {
                HandleLinearMovement();
            }
        }

        private void HandleWaypointMovement()
        {
            if (waypointPath == null || waypointPath.waypoints.Count == 0) return;

            // Get target waypoint
            // Safety check for index
            if (currentWaypointIndex >= waypointPath.waypoints.Count)
            {
                // End of path reached
                // For now, just stop or destroy. Let's destroy to match previous behavior of "leaving scene"
                Destroy(gameObject);
                return;
            }

            Transform target = waypointPath.waypoints[currentWaypointIndex];
            if (target == null) return;

            Vector2 direction = (target.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, target.position);

            // Move
            Vector2 newPos = rb.position + direction * speed * Time.fixedDeltaTime;
            rb.MovePosition(newPos);

            // Rotate
            if (direction != Vector2.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                rb.rotation = Mathf.LerpAngle(rb.rotation, angle, Time.fixedDeltaTime * 10f);
            }

            // Check if reached
            if (distance < 0.2f)
            {
                currentWaypointIndex++;
            }
        }

        private void HandleLinearMovement()
        {
            // Determine target direction based on settings
            Vector2 direction = isMovingHorizontal 
                ? (isMovingRight ? Vector2.right : Vector2.left)
                : (isMovingRight ? Vector2.up : Vector2.down);

            // Move
            Vector2 newPos = rb.position + direction * speed * Time.fixedDeltaTime;
            rb.MovePosition(newPos);

            // Rotate to face direction (assuming sprite faces Up)
            float angle = isMovingHorizontal 
                ? (isMovingRight ? -90 : 90) 
                : (isMovingRight ? 0 : 180);
            rb.rotation = angle;

            //HandleSpeedAndBrakeLights();
            HandlePositionCheck();
        }

        /*private void HandleSpeedAndBrakeLights()
        {
            if (shouldBrake)
            {
                if (!breakLights.activeSelf)
                {
                    breakLights.SetActive(true);
                }
                speed = Mathf.Max(speed - Time.fixedDeltaTime * (startSpeed * 2), 0);
                if (speed == 0)
                {
                    breakLights.SetActive(false);
                }
            }
            else
            {
                if (breakLights.activeSelf)
                {
                    breakLights.SetActive(false);
                }
                speed = Mathf.Min(speed + Time.fixedDeltaTime * 8, startSpeed);
            }
        }*/

        private void HandlePositionCheck()
        {
            // Prevent immediate respawn if start and end are configured incorrectly
            if (Time.timeSinceLevelLoad < 1.0f) return;

            if (isMovingHorizontal)
            {
                // Horizontal movement check
                if ((isMovingRight && rb.position.x > endPos) || (!isMovingRight && rb.position.x < endPos))
                {
                    // Safety check: if we started past the end point, don't respawn immediately
                    // (This implies the configuration is wrong, but prevents infinite loop)
                    float distToEnd = Mathf.Abs(rb.position.x - endPos);
                    float distFromStart = Mathf.Abs(rb.position.x - startPos);
                    
                    if (distFromStart > 0.5f) // Only trigger if we've moved a bit
                    {
                        InstantiateRandomCar();
                    }
                }
            }
            else
            {
                // Vertical movement check
                if ((isMovingRight && rb.position.y > endPos) || (!isMovingRight && rb.position.y < endPos))
                {
                    float distFromStart = Mathf.Abs(rb.position.y - startPos);
                    if (distFromStart > 0.5f)
                    {
                        InstantiateRandomCar();
                    }
                }
            }
        }

        private void InstantiateRandomCar()
        {
            // OverlapBox position will be updated for X or Y axis
            Vector2 overlapPosition = isMovingHorizontal ? new Vector2(startPos, transform.position.y) : new Vector2(transform.position.x, startPos);

            Collider2D[] colliders = Physics2D.OverlapBoxAll(overlapPosition, new Vector2(7f, 1.654827f), 0);

            if (colliders.Length > 0)
            {
                Invoke("InstantiateRandomCar", 0.5f);
                speed = 0;
                Destroy(GetComponent<PolygonCollider2D>());
                Destroy(GetComponent<BoxCollider2D>());
                GetComponent<NPCVehicleMovement>().enabled = false;
            }
            else
            {
                int randCar = Random.Range(1, 3);
                var carPrefab = Resources.Load("NPC Moving Cars/Car" + randCar) as GameObject;
                if (carPrefab == null)
                {
                    Debug.LogError("Could not load car prefab: NPC Moving Cars/Car" + randCar);
                    return;
                }
                GameObject newCar = Instantiate(carPrefab);

                // Set new car's position based on movement direction
                if (isMovingHorizontal)
                {
                    newCar.transform.position = new Vector2(startPos, transform.position.y);
                }
                else
                {
                    newCar.transform.position = new Vector2(transform.position.x, startPos);
                }

                newCar.transform.rotation = this.gameObject.transform.rotation;
                NPCVehicleMovement vehicleMovement = newCar.GetComponent<NPCVehicleMovement>();
                vehicleMovement.speed = this.startSpeed;
                vehicleMovement.startPos = this.startPos;
                vehicleMovement.endPos = this.endPos;
                vehicleMovement.isMovingRight = this.isMovingRight;
                vehicleMovement.isMovingHorizontal = this.isMovingHorizontal;
                Destroy(this.gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.gameObject.name.Contains("Coin")) return;

            // Adjust for position comparison depending on movement direction
            if (isMovingHorizontal)
            {
                if (isMovingRight)
                {
                    if (transform.position.x > col.transform.position.x) return;
                }
                else
                {
                    if (transform.position.x < col.transform.position.x) return;
                }
            }
            else
            {
                if (isMovingRight)
                {
                    if (transform.position.y > col.transform.position.y) return;
                }
                else
                {
                    if (transform.position.y < col.transform.position.y) return;
                }
            }

            shouldBrake = true;

           
        }

        void OnTriggerExit2D(Collider2D col)
        {

            // Adjust for position comparison depending on movement direction
            if (isMovingHorizontal)
            {
                if (isMovingRight && transform.position.x > col.transform.position.x) return;
                if (!isMovingRight && transform.position.x < col.transform.position.x) return;
            }
            else
            {
                if (isMovingRight && transform.position.y > col.transform.position.y) return;
                if (!isMovingRight && transform.position.y < col.transform.position.y) return;
            }

            shouldBrake = false;
        }

        void OnCollisionStay2D(Collision2D col)
        {
            speed = 0;
            shouldBrake = true;
        }
    }
}
