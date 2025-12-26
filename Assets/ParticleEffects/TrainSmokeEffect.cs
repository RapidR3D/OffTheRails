using OffTheRails.Tracks;
using UnityEngine;
using Sirenix.OdinInspector;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Adds a smoke particle effect to a train locomotive.
    /// Attach this to the train GameObject and assign a particle system.
    /// </summary>
    public class TrainSmokeEffect : MonoBehaviour
    {
        [Title("Particle System")]
        [InfoBox("Assign a Particle System child object, or click 'Create Smoke System' to auto-generate one.")]
        [SerializeField] private ParticleSystem smokeParticles;
        
        [Title("Smoke Position")]
        [SerializeField] private Vector3 smokeOffset = new Vector3(0.5f, 0.3f, 0f);
        [Tooltip("If true, smoke offset flips based on train direction")]
        [SerializeField] private bool flipOffsetWithDirection = true;
        
        [Title("Smoke Settings")]
        [SerializeField] private float baseEmissionRate = 15f;
        [SerializeField] private float maxEmissionRate = 40f;
        [SerializeField] private bool scaleWithSpeed = true;
        
        [Title("Puff Settings")]
        [Tooltip("Enable for classic choo-choo puff effect")]
        [SerializeField] private bool enablePuffs = true;
        [SerializeField] private float puffInterval = 0.3f;
        [SerializeField] private int particlesPerPuff = 5;
        
        [Title("References")]
        [SerializeField] private Train train;
        [SerializeField] private DynamicTrainController dynamicTrain;
        
        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.MainModule mainModule;
        private float puffTimer = 0f;
        private bool isMoving = false;
        private Vector2 lastDirection = Vector2.right;
        
        private void Awake()
        {
            // Try to find train component if not assigned
            if (train == null)
                train = GetComponent<Train>();
            if (dynamicTrain == null)
                dynamicTrain = GetComponent<DynamicTrainController>();
                
            if (smokeParticles != null)
            {
                emissionModule = smokeParticles.emission;
                mainModule = smokeParticles.main;
                // Debug.Log($"[TrainSmoke] Found particle system with {smokeParticles.main.maxParticles} max particles");
            }
            else
            {
                // Debug.LogWarning("[TrainSmoke] No particle system assigned!");
            }
            
            if (train == null && dynamicTrain == null)
            {
                // Debug.LogWarning("[TrainSmoke] No Train or DynamicTrainController found!");
            }
        }
        
        private void Start()
        {
            UpdateSmokePosition();
            
            // Force play on start for testing
            if (smokeParticles != null)
            {
                smokeParticles.Play();
                // Debug.Log($"[TrainSmoke] Started playing. IsPlaying={smokeParticles.isPlaying}, ParticleCount={smokeParticles.particleCount}");
            }
        }
        
        private void Update()
        {
            // Check if train is moving
            bool wasMoving = isMoving;
            isMoving = IsTrainMoving();
            
            if (smokeParticles == null) return;
            
            // Update emission based on movement
            if (isMoving)
            {
                if (!wasMoving)
                {
                    smokeParticles.Play();
                }
                
                // Update emission rate based on speed
                if (scaleWithSpeed)
                {
                    float speed = GetTrainSpeed();
                    float normalizedSpeed = Mathf.Clamp01(speed / 10f); // Assuming max speed ~10
                    float rate = Mathf.Lerp(baseEmissionRate, maxEmissionRate, normalizedSpeed);
                    emissionModule.rateOverTime = rate;
                }
                
                // Handle puff effect
                if (enablePuffs)
                {
                    puffTimer += Time.deltaTime;
                    if (puffTimer >= puffInterval)
                    {
                        puffTimer = 0f;
                        smokeParticles.Emit(particlesPerPuff);
                    }
                }
                
                // Update smoke position based on direction
                UpdateSmokePosition();
            }
            else
            {
                if (wasMoving)
                {
                    // Keep some residual smoke when stopping
                    emissionModule.rateOverTime = baseEmissionRate * 0.3f;
                }
            }
        }
        
        private void UpdateSmokePosition()
        {
            if (smokeParticles == null) return;
            
            Vector3 offset = smokeOffset;
            
            if (flipOffsetWithDirection)
            {
                Vector2 dir = GetTrainDirection();
                if (dir != Vector2.zero)
                {
                    lastDirection = dir;
                }
                
                // Flip X offset based on direction
                if (lastDirection.x < 0)
                {
                    offset.x = -Mathf.Abs(offset.x);
                }
                else
                {
                    offset.x = Mathf.Abs(offset.x);
                }
            }
            
            smokeParticles.transform.localPosition = offset;
        }
        
        private bool IsTrainMoving()
        {
            if (train != null)
                return train.IsActive && train.Speed > 0.1f;
            if (dynamicTrain != null)
                return dynamicTrain.IsActive;
            return false;
        }
        
        private float GetTrainSpeed()
        {
            if (train != null)
                return train.Speed;
            // DynamicTrainController doesn't expose speed, use default
            return 5f;
        }
        
        private Vector2 GetTrainDirection()
        {
            if (train != null)
                return train.GetDirection();
            if (dynamicTrain != null)
                return dynamicTrain.GetDirection();
            return Vector2.right;
        }
        
        [Button("Create Smoke System"), GUIColor(0.5f, 1f, 0.5f)]
        private void CreateSmokeSystem()
        {
            if (smokeParticles != null)
            {
                // Debug.LogWarning("Smoke system already exists!");
                return;
            }
            
            // Create child GameObject with ParticleSystem
            GameObject smokeObj = new GameObject("SmokeEffect");
            smokeObj.transform.SetParent(transform);
            smokeObj.transform.localPosition = smokeOffset;
            smokeObj.transform.localRotation = Quaternion.identity;
            
            smokeParticles = smokeObj.AddComponent<ParticleSystem>();
            
            // Configure main module
            var main = smokeParticles.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.1f; // Float upward
            
            // Configure emission
            var emission = smokeParticles.emission;
            emission.rateOverTime = baseEmissionRate;
            
            // Configure shape - emit from a small area
            var shape = smokeParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            
            // Configure size over lifetime - grow as it rises
            var sizeOverLifetime = smokeParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(1f, 1.5f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            
            // Configure color over lifetime - fade out
            var colorOverLifetime = smokeParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.white, 0f), 
                    new GradientColorKey(Color.gray, 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.6f, 0f), 
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            colorOverLifetime.color = gradient;
            
            // Note: Skipping velocityOverLifetime to avoid curve mode conflicts
            // The negative gravity modifier already makes smoke float upward
            
            // Configure renderer
            var renderer = smokeParticles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "Default"; // Adjust to your sorting layer
            renderer.sortingOrder = 10; // Above train
            
            // Try to use default particle material
            renderer.material = GetDefaultParticleMaterial();
            
            // Store references
            emissionModule = smokeParticles.emission;
            mainModule = smokeParticles.main;
            
            // Debug.Log("Created smoke particle system! You may want to adjust the material and sorting layer.");
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(smokeObj);
            #endif
        }
        
        private Material GetDefaultParticleMaterial()
        {
            // Try to find a default particle material
            Material mat = Resources.Load<Material>("Default-Particle");
            if (mat == null)
            {
                // Create a simple unlit material
                mat = new Material(Shader.Find("Particles/Standard Unlit"));
            }
            return mat;
        }
        
        [Button("Remove Smoke System"), GUIColor(1f, 0.5f, 0.5f)]
        private void RemoveSmokeSystem()
        {
            if (smokeParticles != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(smokeParticles.gameObject);
                }
                else
                {
                    DestroyImmediate(smokeParticles.gameObject);
                }
                smokeParticles = null;
                // Debug.Log("Removed smoke particle system");
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Show smoke offset position
            Gizmos.color = Color.gray;
            Vector3 worldOffset = transform.TransformPoint(smokeOffset);
            Gizmos.DrawWireSphere(worldOffset, 0.2f);
            Gizmos.DrawLine(transform.position, worldOffset);
        }
    }
}
