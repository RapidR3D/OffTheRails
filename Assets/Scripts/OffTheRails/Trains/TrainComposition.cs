using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using OffTheRails.Tracks;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Manages a complete train composition (locomotive + cars).
    /// Handles spacing and movement of all cars following the locomotive.
    /// </summary>
    public class TrainComposition : MonoBehaviour
    {
        [Title("Locomotive")]
        [Required]
        [InfoBox("The lead locomotive that drives the train. Must have Train or DynamicTrainController component.")]
        [SerializeField] private GameObject locomotive;
        
        [Title("Train Cars")]
        [InfoBox("Add cars in order from front to back. Each car will follow the one ahead of it.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField] private List<TrainCarConfig> cars = new List<TrainCarConfig>();
        
        [Title("Spacing Settings")]
        [Tooltip("Distance between the locomotive's rear coupler and the first car's front coupler")]
        [SerializeField] private float locomotiveToFirstCarSpacing = 2f;
        
        [Tooltip("Default spacing between cars (can be overridden per car)")]
        [SerializeField] private float defaultCarSpacing = 1.5f;
        
        [Title("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private Train trainComponent;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private DynamicTrainController dynamicTrainComponent;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private float locomotiveDistance;
        
        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime Info")]
        private int TotalCars => cars?.Count ?? 0;
        
        private void Awake()
        {
            if (locomotive != null)
            {
                trainComponent = locomotive.GetComponent<Train>();
                dynamicTrainComponent = locomotive.GetComponent<DynamicTrainController>();
                
                if (trainComponent == null && dynamicTrainComponent == null)
                {
                    Debug.LogError("[TrainComposition] Locomotive must have Train or DynamicTrainController component!");
                }
            }
        }
        
        private void LateUpdate()
        {
            UpdateCarPositions();
        }
        
        /// <summary>
        /// Update all car positions based on locomotive position
        /// </summary>
        private void UpdateCarPositions()
        {
            if (locomotive == null) return;
            
            // Different handling for Train vs DynamicTrainController
            if (trainComponent != null)
            {
                UpdateCarsFromTrackPath();
            }
            else if (dynamicTrainComponent != null)
            {
                UpdateCarsFromPositionHistory();
            }
        }
        
        /// <summary>
        /// Update cars using TrackPath (for Train component)
        /// </summary>
        private void UpdateCarsFromTrackPath()
        {
            TrackPath path = trainComponent.CurrentPath;
            if (path == null) return;
            
            locomotiveDistance = trainComponent.DistanceAlongPath;
            
            float cumulativeDistance = locomotiveDistance - locomotiveToFirstCarSpacing;
            
            for (int i = 0; i < cars.Count; i++)
            {
                var carConfig = cars[i];
                if (carConfig.car == null) continue;
                
                TrainCar trainCar = carConfig.car.GetComponent<TrainCar>();
                if (trainCar != null)
                {
                    trainCar.UpdatePosition(path, cumulativeDistance);
                }
                else
                {
                    PositionCarFromPath(carConfig.car, path, cumulativeDistance);
                }
                
                float spacing = carConfig.useCustomSpacing ? carConfig.customSpacing : defaultCarSpacing;
                float carLength = carConfig.carLength > 0 ? carConfig.carLength : 2f;
                cumulativeDistance -= (carLength + spacing);
            }
        }
        
        /// <summary>
        /// Update cars using position history (for DynamicTrainController)
        /// </summary>
        private void UpdateCarsFromPositionHistory()
        {
            // Distance from locomotive's rear to front coupler of first car
            float distanceToFrontCoupler = locomotiveToFirstCarSpacing;
            
            for (int i = 0; i < cars.Count; i++)
            {
                var carConfig = cars[i];
                if (carConfig.car == null) continue;
                
                float carLength = carConfig.carLength > 0 ? carConfig.carLength : 2f;
                float halfCarLength = carLength / 2f;
                
                // Position at center of car (front coupler + half length)
                float distanceToCarCenter = distanceToFrontCoupler + halfCarLength;
                
                // Get position from history
                if (dynamicTrainComponent.TryGetPositionAtDistanceBehind(distanceToCarCenter, out Vector2 position, out Vector2 direction))
                {
                    carConfig.car.transform.position = new Vector3(position.x, position.y, carConfig.car.transform.position.z);
                    
                    // Rotate to face direction
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    carConfig.car.transform.rotation = Quaternion.Euler(0, 0, angle);
                    // Debug.Log($"Car {i}: distBehind={distanceToCarCenter:F2}, pos={position}, dir={direction}");
                }
                
                // Calculate distance to next car's front coupler
                // (current car's rear coupler = front coupler + full car length)
                // Plus spacing between cars
                float spacing = carConfig.useCustomSpacing ? carConfig.customSpacing : defaultCarSpacing;
                distanceToFrontCoupler += carLength + spacing;
            }
        }
        
        /// <summary>
        /// Position a car from TrackPath
        /// </summary>
        private void PositionCarFromPath(GameObject car, TrackPath path, float distance)
        {
            float clampedDistance = Mathf.Max(0f, distance);
            Vector2 position = path.GetPositionAtDistance(clampedDistance);
            Vector2 direction = path.GetDirectionAtDistance(clampedDistance);
            
            car.transform.position = new Vector3(position.x, position.y, car.transform.position.z);
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            car.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        
        /// <summary>
        /// Add a car to the train at runtime
        /// </summary>
        [Button("Add Car")]
        public void AddCar(GameObject carObject, float carLength = 2f)
        {
            if (carObject == null) return;
            
            var config = new TrainCarConfig
            {
                car = carObject,
                carLength = carLength,
                useCustomSpacing = false
            };
            
            cars.Add(config);
            // Debug.Log($"[TrainComposition] Added car '{carObject.name}'. Total cars: {cars.Count}");
        }
        
        /// <summary>
        /// Remove the last car from the train
        /// </summary>
        [Button("Remove Last Car")]
        public void RemoveLastCar()
        {
            if (cars.Count > 0)
            {
                var removed = cars[cars.Count - 1];
                cars.RemoveAt(cars.Count - 1);
                // Debug.Log($"[TrainComposition] Removed car '{removed.car?.name}'. Total cars: {cars.Count}");
            }
        }
        
        /// <summary>
        /// Get total length of the train (locomotive + all cars)
        /// </summary>
        public float GetTotalTrainLength()
        {
            float totalLength = locomotiveToFirstCarSpacing;
            
            foreach (var carConfig in cars)
            {
                float spacing = carConfig.useCustomSpacing ? carConfig.customSpacing : defaultCarSpacing;
                float carLength = carConfig.carLength > 0 ? carConfig.carLength : 2f;
                totalLength += carLength + spacing;
            }
            
            return totalLength;
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || locomotive == null) return;
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(locomotive.transform.position, 0.3f);
            
            Vector3 lastPos = locomotive.transform.position;
            
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i].car == null) continue;
                
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, (float)i / Mathf.Max(1, cars.Count - 1));
                Vector3 carPos = cars[i].car.transform.position;
                
                Gizmos.DrawWireSphere(carPos, 0.25f);
                Gizmos.DrawLine(lastPos, carPos);
                
                lastPos = carPos;
            }
        }
    }
    
    /// <summary>
    /// Configuration for a single train car
    /// </summary>
    [System.Serializable]
    public class TrainCarConfig
    {
        [HorizontalGroup("Car", Width = 0.4f)]
        [HideLabel]
        [Required]
        public GameObject car;
        
        [HorizontalGroup("Car", Width = 0.2f)]
        [LabelText("Length")]
        [LabelWidth(45)]
        [Tooltip("Length of this car (distance from front to rear coupler)")]
        public float carLength = 2f;
        
        [HorizontalGroup("Car", Width = 0.15f)]
        [LabelText("Custom")]
        [LabelWidth(50)]
        [Tooltip("Use custom spacing after this car")]
        public bool useCustomSpacing = false;
        
        [HorizontalGroup("Car", Width = 0.25f)]
        [LabelText("Spacing")]
        [LabelWidth(50)]
        [ShowIf("useCustomSpacing")]
        [Tooltip("Custom spacing to the next car")]
        public float customSpacing = 1.5f;
    }
}
