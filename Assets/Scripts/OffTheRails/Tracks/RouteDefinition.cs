using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OffTheRails.Tracks
{
    /// <summary>
    /// Defines a route through the track network.
    /// Routes specify an ordered sequence of tracks and required switch states.
    /// Attach this to a GameObject in the scene (not a ScriptableObject asset).
    /// </summary>
    public class RouteDefinition : MonoBehaviour
    {
        [Title("Route Info")]
        [PropertyOrder(-1)]
        public string routeName = "New Route";
        
        [TextArea(2, 3)]
        public string description;
        
        [Title("Route Color")]
        [ColorPalette]
        public Color gizmoColor = Color.green;
        
        [Title("Track Sequence")]
        [InfoBox("Add tracks in order from start to end. For junctions, specify which connection points to use.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<RouteSegment> segments = new List<RouteSegment>();
        
        [Title("Switch Requirements")]
        [InfoBox("Specify which switches must be set to which state for this route to be valid.")]
        [ListDrawerSettings(ShowIndexLabels = false)]
        public List<SwitchRequirement> switchRequirements = new List<SwitchRequirement>();
        
        [Title("Route Properties")]
        [ShowInInspector, ReadOnly]
        public int TrackCount => segments?.Count ?? 0;
        
        [ShowInInspector, ReadOnly]
        public string StartTrackName => segments?.Count > 0 ? segments[0].track?.name ?? "None" : "None";
        
        [ShowInInspector, ReadOnly]
        public string EndTrackName => segments?.Count > 0 ? segments[segments.Count - 1].track?.name ?? "None" : "None";
        
        /// <summary>
        /// Check if all switch requirements are met
        /// </summary>
        public bool AreSwitchRequirementsMet()
        {
            foreach (var req in switchRequirements)
            {
                if (req.trackSwitch == null) continue;
                
                if (req.trackSwitch.IsDiverging != req.requireDiverging)
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Set all switches to match this route's requirements
        /// </summary>
        [Button("Set Switches For Route"), GUIColor(0.5f, 1f, 0.5f)]
        public void SetSwitchesForRoute()
        {
            foreach (var req in switchRequirements)
            {
                if (req.trackSwitch == null) continue;
                req.trackSwitch.SetState(req.requireDiverging);
            }
            // Debug.Log($"[Route {routeName}] Set {switchRequirements.Count} switches");
        }
        
        /// <summary>
        /// Validate the route configuration
        /// </summary>
        [Button("Validate Route"), GUIColor(1f, 1f, 0.5f)]
        public void ValidateRoute()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            
            if (segments.Count == 0)
            {
                errors.Add("Route has no segments!");
            }
            
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                
                if (seg.track == null)
                {
                    errors.Add($"Segment {i}: Track is null");
                    continue;
                }
                
                // Check junction entry/exit points
                if (seg.track.Type == TrackType.Junction)
                {
                    if (seg.entryCP < 0 || seg.entryCP >= seg.track.ConnectionPoints.Length)
                        errors.Add($"Segment {i} ({seg.track.name}): Invalid entry CP {seg.entryCP}");
                    
                    if (seg.exitCP < 0 || seg.exitCP >= seg.track.ConnectionPoints.Length)
                        errors.Add($"Segment {i} ({seg.track.name}): Invalid exit CP {seg.exitCP}");
                    
                    // Check for valid junction traversal (can't go CP1 <-> CP2)
                    if ((seg.entryCP == 1 && seg.exitCP == 2) || (seg.entryCP == 2 && seg.exitCP == 1))
                        errors.Add($"Segment {i} ({seg.track.name}): Invalid junction traversal CP{seg.entryCP} → CP{seg.exitCP} (would derail!)");
                }
                
                // Check connections between consecutive segments
                if (i < segments.Count - 1)
                {
                    var nextSeg = segments[i + 1];
                    if (nextSeg.track == null) continue;
                    
                    // Verify tracks are actually connected
                    bool connected = false;
                    foreach (var cp in seg.track.ConnectionPoints)
                    {
                        if (cp.IsConnected && cp.ConnectedTo?.ParentTrack == nextSeg.track)
                        {
                            connected = true;
                            break;
                        }
                    }
                    
                    if (!connected)
                        errors.Add($"Segment {i} ({seg.track.name}) is not connected to segment {i + 1} ({nextSeg.track.name})");
                }
            }
            
            // Check switch requirements
            foreach (var req in switchRequirements)
            {
                if (req.trackSwitch == null)
                    warnings.Add("Switch requirement has null switch reference");
            }
            
            // Log results
            if (errors.Count == 0 && warnings.Count == 0)
            {
                // Debug.Log($"[Route {routeName}] ✓ Validation passed!");
            }
            else
            {
                foreach (var error in errors)
                    Debug.LogError($"[Route {routeName}] {error}");
                foreach (var warning in warnings)
                    Debug.LogWarning($"[Route {routeName}] {warning}");
            }
        }
        
        /// <summary>
        /// Get the list of tracks in this route
        /// </summary>
        public List<TrackPiece> GetTracks()
        {
            List<TrackPiece> tracks = new List<TrackPiece>();
            foreach (var seg in segments)
            {
                if (seg.track != null)
                    tracks.Add(seg.track);
            }
            return tracks;
        }
    }
    
    /// <summary>
    /// A single segment in a route
    /// </summary>
    [System.Serializable]
    public class RouteSegment
    {
        [HorizontalGroup("Main", Width = 0.6f)]
        [HideLabel]
        public TrackPiece track;
        
        [HorizontalGroup("Main")]
        [ShowIf("IsJunction")]
        [LabelText("Entry")]
        [LabelWidth(35)]
        public int entryCP = 0;
        
        [HorizontalGroup("Main")]
        [ShowIf("IsJunction")]
        [LabelText("Exit")]
        [LabelWidth(30)]
        public int exitCP = 1;
        
        private bool IsJunction => track != null && track.Type == TrackType.Junction;
        
        [ShowInInspector, ReadOnly, HideLabel]
        [HorizontalGroup("Main", Width = 0.15f)]
        [ShowIf("@track != null")]
        private string TrackTypeDisplay => track?.Type.ToString() ?? "";
    }
    
    /// <summary>
    /// A switch state requirement for a route
    /// </summary>
    [System.Serializable]
    public class SwitchRequirement
    {
        [HorizontalGroup("Req")]
        [HideLabel]
        public TrackSwitch trackSwitch;
        
        [HorizontalGroup("Req", Width = 100)]
        [LabelText("Diverging")]
        [LabelWidth(60)]
        public bool requireDiverging = false;
        
        [ShowInInspector, ReadOnly]
        [HorizontalGroup("Req", Width = 80)]
        [HideLabel]
        [GUIColor("GetStateColor")]
        private string CurrentState => trackSwitch != null ? (trackSwitch.IsDiverging ? "DIV" : "STR") : "?";
        
        private Color GetStateColor()
        {
            if (trackSwitch == null) return Color.gray;
            bool matches = trackSwitch.IsDiverging == requireDiverging;
            return matches ? Color.green : Color.red;
        }
    }
}
