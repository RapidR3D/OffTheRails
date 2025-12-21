using UnityEngine;
using OffTheRails.Tracks;

public class DebugConnections : MonoBehaviour
{
    void Start()
    {
        if (TrackManager.Instance == null)
        {
            Debug.LogError("No TrackManager!");
            return;
        }

        Debug.Log("=== CONNECTION DEBUG ===");
        
        foreach (var track in TrackManager.Instance.GetAllTracks())
        {
            Debug.Log($"\n--- {track.name} ---");
            Debug.Log($" Type: {track.Type}");
            Debug.Log($" Connection Points: {track.ConnectionPoints.Length}");
            
            for (int i = 0; i < track.ConnectionPoints.Length; i++)
            {
                var cp = track.ConnectionPoints[i];
                Debug.Log($" CP[{i}]: IsConnected={cp.IsConnected}, ConnectedTo={cp.ConnectedTo?.ParentTrack?.name ?? "NULL"}");
                Debug.Log($" Position={cp.WorldPosition}, Direction={cp.WorldDirection}");
            }
            
            var connected = track.GetConnectedTracks();
            Debug.Log($" GetConnectedTracks() returned: {connected.Count} tracks");
            foreach (var c in connected)
            {
                Debug.Log($" - {c.name}");
            }
        }
    }
}