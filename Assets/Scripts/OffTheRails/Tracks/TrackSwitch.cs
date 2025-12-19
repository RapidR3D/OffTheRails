using UnityEngine;
using UnityEngine.Events;

namespace OffTheRails.Tracks  
{
    /// <summary>
    /// Handles the visual and logical state of a track switch. 
    /// </summary>
    public class TrackSwitch : MonoBehaviour
    {
        [Header("Switch State")]
        [SerializeField] private bool isDiverging = false;
        
        [Header("Events")]
        public UnityEvent OnSwitchToggled;

        public bool IsDiverging => isDiverging;
        public TrackPiece ParentTrack { get; private set; }

        private void Awake()
        {
            ParentTrack = GetComponent<TrackPiece>();
        }

        public void ToggleSwitch()
        {
            isDiverging = !isDiverging;
            UpdateVisuals();
            OnSwitchToggled?.Invoke();
            
            // Notify TrackManager to regenerate paths if needed
            if (TrackManager.Instance != null)
            {
                // We need to force the track piece to regenerate its waypoints first
                var trackPiece = GetComponent<TrackPiece>();
                if (trackPiece != null)
                {
                    trackPiece.GenerateWaypoints();
                }
                TrackManager.Instance.RegenerateAllPaths();
            }
        }

        private void UpdateVisuals()
        {
            // Add your visual update logic here
        }
    }
}