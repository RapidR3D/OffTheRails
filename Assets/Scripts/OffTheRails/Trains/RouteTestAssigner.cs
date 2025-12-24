using UnityEngine;
using OffTheRails.Tracks;
using OffTheRails.Trains;
using Sirenix.OdinInspector;

/// <summary>
/// Simple test script to assign a train to a route at startup.
/// Attach this to any GameObject in the scene.
/// </summary>
public class RouteTestAssigner : MonoBehaviour
{
    [Title("Setup")]
    [SerializeField, Required] private Train train;
    [SerializeField, Required] private RouteDefinition route;
    
    [Title("Options")]
    [SerializeField] private bool startFromBeginning = true;
    
    [Title("Debug")]
    [Button("Assign Train to Route"), GUIColor(0, 1, 0)]
    [EnableIf("@UnityEngine.Application.isPlaying")]
    private void AssignTrainToRoute()
    {
        if (train == null || route == null)
        {
            Debug.LogError("Train or Route is not assigned!");
            return;
        }
        
        if (RouteManager.Instance == null)
        {
            Debug.LogError("RouteManager.Instance is null! Make sure RouteManager exists in scene.");
            return;
        }
        
        RouteManager.Instance.AssignTrainToRoute(train, route, startFromBeginning);
    }
    
    void Start()
    {
        // Auto-assign on start
        AssignTrainToRoute();
    }
}