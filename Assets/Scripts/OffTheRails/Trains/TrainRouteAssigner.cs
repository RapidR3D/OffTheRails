using UnityEngine;
using System.Collections.Generic;
using OffTheRails.Tracks;
using OffTheRails.Trains;
using Sirenix.OdinInspector;

namespace OffTheRails.Trains
{
    /// <summary>
    /// Assigns trains to routes at startup. Supports multiple train/route assignments.
    /// Use this for NPC trains that follow predefined routes.
    /// </summary>
    public class TrainRouteAssigner : MonoBehaviour
    {
        [Title("Train Assignments")]
        [InfoBox("Add trains and their assigned routes here. Each train will be assigned to its route at startup.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField] private List<TrainAssignment> assignments = new List<TrainAssignment>();
        
        [Title("Options")]
        [SerializeField] private bool assignOnStart = true;
        [SerializeField] private float delayBetweenAssignments = 0f;
        
        [Title("Debug")]
        [ShowInInspector, ReadOnly]
        private int AssignedCount => assignments?.Count ?? 0;
        
        void Start()
        {
            if (assignOnStart)
            {
                if (delayBetweenAssignments > 0)
                {
                    StartCoroutine(AssignAllWithDelay());
                }
                else
                {
                    AssignAll();
                }
            }
        }
        
        [Button("Assign All Trains"), GUIColor(0, 1, 0)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void AssignAll()
        {
            if (RouteManager.Instance == null)
            {
                Debug.LogError("[TrainRouteAssigner] RouteManager.Instance is null!");
                return;
            }
            
            int successCount = 0;
            foreach (var assignment in assignments)
            {
                if (assignment.train == null || assignment.route == null)
                {
                    Debug.LogWarning("[TrainRouteAssigner] Skipping assignment with null train or route");
                    continue;
                }
                
                if (RouteManager.Instance.AssignTrainToRoute(assignment.train, assignment.route, assignment.forwardDirection))
                {
                    successCount++;
                }
            }
            
            Debug.Log($"[TrainRouteAssigner] Assigned {successCount}/{assignments.Count} trains to routes");
        }
        
        private System.Collections.IEnumerator AssignAllWithDelay()
        {
            if (RouteManager.Instance == null)
            {
                Debug.LogError("[TrainRouteAssigner] RouteManager.Instance is null!");
                yield break;
            }
            
            int successCount = 0;
            foreach (var assignment in assignments)
            {
                if (assignment.train == null || assignment.route == null)
                {
                    Debug.LogWarning("[TrainRouteAssigner] Skipping assignment with null train or route");
                    continue;
                }
                
                if (RouteManager.Instance.AssignTrainToRoute(assignment.train, assignment.route, assignment.forwardDirection))
                {
                    successCount++;
                }
                
                if (delayBetweenAssignments > 0)
                {
                    yield return new WaitForSeconds(delayBetweenAssignments);
                }
            }
            
            Debug.Log($"[TrainRouteAssigner] Assigned {successCount}/{assignments.Count} trains to routes");
        }
        
        /// <summary>
        /// Add a new assignment at runtime
        /// </summary>
        public void AddAssignment(Train train, RouteDefinition route, bool forward = true)
        {
            assignments.Add(new TrainAssignment
            {
                train = train,
                route = route,
                forwardDirection = forward
            });
        }
        
        /// <summary>
        /// Assign a specific train immediately
        /// </summary>
        public bool AssignTrain(int index)
        {
            if (index < 0 || index >= assignments.Count)
            {
                Debug.LogError($"[TrainRouteAssigner] Invalid index {index}");
                return false;
            }
            
            var assignment = assignments[index];
            if (assignment.train == null || assignment.route == null)
            {
                Debug.LogError("[TrainRouteAssigner] Train or route is null");
                return false;
            }
            
            return RouteManager.Instance.AssignTrainToRoute(
                assignment.train, 
                assignment.route, 
                assignment.forwardDirection
            );
        }
    }
    
    /// <summary>
    /// A single train-to-route assignment
    /// </summary>
    [System.Serializable]
    public class TrainAssignment
    {
        [HorizontalGroup("Main", Width = 0.35f)]
        [HideLabel]
        [Required]
        public Train train;
        
        [HorizontalGroup("Main", Width = 0.35f)]
        [HideLabel]
        [Required]
        public RouteDefinition route;
        
        [HorizontalGroup("Main", Width = 0.2f)]
        [LabelText("Fwd")]
        [LabelWidth(30)]
        [Tooltip("If true, travel in segment order. If false, travel in reverse.")]
        public bool forwardDirection = true;
        
        [HorizontalGroup("Main", Width = 0.1f)]
        [Button("Go"), GUIColor(0.5f, 1f, 0.5f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void AssignNow()
        {
            if (train == null || route == null) return;
            if (RouteManager.Instance == null) return;
            RouteManager.Instance.AssignTrainToRoute(train, route, forwardDirection);
        }
    }
}