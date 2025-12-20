using UnityEngine;

namespace OffTheRails.NPCs
{
    public class NPCVehicleMovement : MonoBehaviour
    {
        public float speed = 5;
        private float startSpeed;
        [SerializeField] private GameObject breakLights;
        [SerializeField] private AudioClip carHornSound;
        private Rigidbody2D rb;
        private bool shouldBrake = false;
        public float startPos;
        public float endPos;
        public bool isMovingRight = true; // For horizontal movement
        public bool isMovingHorizontal = true; // True if moving along the X-axis, false for Y-axis

        void Start()
        {
            //Destroy(GetComponent<CarCollisionAlarm>());
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;

            // Set start position based on movement direction
            startPos = isMovingHorizontal ? transform.position.x : transform.position.y;
            startSpeed = speed;
        }

        void FixedUpdate()
        {
            Vector2 newPos = Vector2.zero;

            // Move in the proper direction (up for Y, right for X)
            newPos = rb.transform.position + rb.transform.up * -speed * Time.fixedDeltaTime;
            rb.MovePosition(newPos);

            HandleSpeedAndBrakeLights();
            HandlePositionCheck();
        }

        private void HandleSpeedAndBrakeLights()
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
        }

        private void HandlePositionCheck()
        {
            if (isMovingHorizontal)
            {
                // Horizontal movement check
                if ((isMovingRight && rb.position.x > endPos) || (!isMovingRight && rb.position.x < endPos))
                {
                    InstantiateRandomCar();
                }
            }
            else
            {
                // Vertical movement check
                if ((isMovingRight && rb.position.y > endPos) || (!isMovingRight && rb.position.y < endPos))
                {
                    InstantiateRandomCar();
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
                int randCar = Random.Range(1, 37);
                GameObject newCar = Instantiate(Resources.Load("NPC Moving Cars/Car" + randCar) as GameObject);

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

            if (col.gameObject.GetComponent<CarController>() != null) // Check if it's the player's vehicle
            {
                if (PlayerPrefs.GetInt("SoundSetting") == 1) return;
                if (GameObject.Find("CarHornSound") == null)
                {
                    GameObject carHornSoundGameObject = Instantiate(Resources.Load("CarHornSound")) as GameObject;
                    carHornSoundGameObject.name = "CarHornSound";
                    carHornSoundGameObject.GetComponent<AudioSource>().clip = carHornSound;
                    carHornSoundGameObject.GetComponent<AudioSource>().Play();
                }
            }
        }

        void OnTriggerExit2D(Collider2D col)
        {
            if (col.gameObject.GetComponent<CarController>() != null) // Check if it's the player's vehicle
            {
                shouldBrake = false;
                return;
            }

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
