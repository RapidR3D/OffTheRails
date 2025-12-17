# Track Snapping System

A comprehensive track placement and connection system for a top-down 2D train game in Unity.

## Overview

This system allows players to place track pieces that automatically snap together and generate paths for trains to follow. The system supports straight and curved tracks with intelligent connection detection and visual feedback.

## Features

- **Automatic Snapping**: Tracks automatically snap to nearby connection points
- **Visual Feedback**: Color-coded indicators show valid/invalid placements
- **Path Generation**: Automatically generates continuous paths from connected tracks
- **Extensible Design**: Easy to add new track types and features
- **Scene Gizmos**: Visual debugging tools for connection points and paths

## Components

### 1. TrackPiece.cs
Base component for all track segments.

**Key Features:**
- Stores connection points and track type
- Manages waypoints for train movement
- Auto-generates waypoints for straight and curved tracks
- Unique ID for each track piece

**Inspector Settings:**
- `Track Type`: Straight, Curved, or Junction
- `Local Waypoints`: Path points in local space
- `Auto Generate Waypoints`: Automatically create waypoints from connection points
- `Curved Waypoint Count`: Number of points for curved paths

### 2. ConnectionPoint.cs
Handles snap detection and validation between tracks.

**Key Features:**
- Position and direction-based snapping
- Configurable snap radius
- Direction compatibility checking
- Visual gizmos for debugging

**Inspector Settings:**
- `Direction`: Local space direction of this connection point
- `Snap Radius`: Distance for detecting nearby connections
- `Direction Tolerance`: How aligned directions must be (0-1)
- `Gizmo Colors`: Customizable visual feedback colors

### 3. TrackManager.cs
Singleton that manages all track pieces and paths.

**Key Features:**
- Centralized track registration
- Automatic path generation
- Connection management
- Query interface for train systems

**Inspector Settings:**
- `Default Snap Radius`: Global snap distance
- `Auto Generate Paths`: Create paths when tracks connect
- `Enable Debug Logs`: Toggle logging
- `Show Path Gizmos`: Visualize paths in scene view

### 4. TrackPath.cs
Represents a connected sequence of tracks.

**Key Features:**
- Combined waypoints from multiple tracks
- Total path length calculation
- Loop detection
- Position/direction queries along path

**Usage in Code:**
```csharp
// Get position along path
Vector2 pos = trackPath.GetPositionAtDistance(10.5f);

// Get direction for train orientation
Vector2 dir = trackPath.GetDirectionAtDistance(10.5f);

// Check if path loops
bool isLoop = trackPath.IsLoop;
```

### 5. TrackPlacer.cs
Handles player interaction for placing tracks.

**Key Features:**
- Drag and drop placement
- Real-time preview
- Automatic snapping during placement
- Keyboard rotation controls

**Inspector Settings:**
- `Track Prefabs`: Array of placeable track prefabs
- `Track Layer`: Layer for placed tracks
- `Track Sorting Layer`: Rendering layer
- `Snap To Grid`: Optional grid snapping
- `Visual Feedback Colors`: Preview colors

**Controls:**
- **Left Click**: Place track
- **Q**: Rotate counter-clockwise
- **E**: Rotate clockwise
- **1-9**: Select track prefab

## Setup Instructions

### Initial Scene Setup

1. **Create Track Manager**
   - In Unity Editor: `GameObject > Off The Rails > Track Manager`
   - This creates a TrackManager singleton in your scene
   - Configure settings in the Inspector

2. **Create Track Placer**
   - In Unity Editor: `GameObject > Off The Rails > Track Placer`
   - This creates a TrackPlacer for runtime track placement
   - Assign track prefabs (see below)

### Creating Track Prefabs

#### Straight Track

1. Create a new GameObject: `GameObject > 2D Object > Sprite`
2. Name it "Straight Track"
3. Assign the straight track sprite (StraightTrackScaled)
4. Add Component: `TrackPiece`
   - Set `Track Type` to "Straight"
   - Enable `Auto Generate Waypoints`

5. Create Connection Points:
   - Create child GameObject: "Connection Point 1"
     - Position: (-0.5, 0, 0) - left end
     - Add Component: `ConnectionPoint`
     - Set `Direction` to (1, 0) - pointing right
     - Set `Snap Radius` to 0.5
   
   - Create child GameObject: "Connection Point 2"
     - Position: (0.5, 0, 0) - right end
     - Add Component: `ConnectionPoint`
     - Set `Direction` to (-1, 0) - pointing left
     - Set `Snap Radius` to 0.5

6. Create Prefab:
   - Drag the GameObject to your Assets/Prefabs folder
   - Delete the instance from the scene

#### Curved Track (90-degree)

1. Create a new GameObject: `GameObject > 2D Object > Sprite`
2. Name it "Curved Track"
3. Assign the curved track sprite (TrackBendScaled)
4. Add Component: `TrackPiece`
   - Set `Track Type` to "Curved"
   - Enable `Auto Generate Waypoints`
   - Set `Curved Waypoint Count` to 10

5. Create Connection Points (assuming curve goes from left to top):
   - Create child GameObject: "Connection Point 1"
     - Position: (-0.5, 0, 0) - left end
     - Add Component: `ConnectionPoint`
     - Set `Direction` to (1, 0) - pointing right
   
   - Create child GameObject: "Connection Point 2"
     - Position: (0, 0.5, 0) - top end
     - Add Component: `ConnectionPoint`
     - Set `Direction` to (0, -1) - pointing down

6. Create Prefab:
   - Drag to Assets/Prefabs folder

**Note**: You'll need to create 4 rotations of the curved track or rely on runtime rotation with the TrackPlacer.

### Configuring the TrackPlacer

1. Select the TrackPlacer GameObject
2. In Inspector, set `Track Prefabs` array size
3. Assign your track prefabs to the array
4. Configure visual settings:
   - `Valid Placement Color`: Green (transparent)
   - `Invalid Placement Color`: Red (transparent)
   - `Snap Indicator Color`: Yellow

### Setting Up Layers

1. Create Sorting Layers:
   - Open `Edit > Project Settings > Tags and Layers`
   - Add sorting layer: "Tracks"
   - Add sorting layer: "Trains" (if not exists)

2. Configure TrackPlacer:
   - Set `Track Sorting Layer` to "Tracks"

## Usage

### Runtime Track Placement

1. Enter Play Mode
2. Press number keys (1-9) to select track type
3. Click and drag to place track
4. Use Q/E to rotate before releasing
5. Track will snap to nearby connection points automatically

### Accessing Paths in Code

```csharp
using OffTheRails.Tracks;

public class TrainController : MonoBehaviour
{
    private TrackPath currentPath;
    private float distanceAlongPath = 0f;

    void Start()
    {
        // Find a path to follow
        TrackPiece startTrack = FindObjectOfType<TrackPiece>();
        if (startTrack != null && TrackManager.Instance != null)
        {
            currentPath = TrackManager.Instance.FindPathContainingTrack(startTrack);
        }
    }

    void Update()
    {
        if (currentPath == null)
            return;

        // Move along path
        distanceAlongPath += Time.deltaTime * speed;

        // Get position and direction
        Vector2 position = currentPath.GetPositionAtDistance(distanceAlongPath);
        Vector2 direction = currentPath.GetDirectionAtDistance(distanceAlongPath);

        // Update train
        transform.position = position;
        transform.up = direction;

        // Handle looping
        if (currentPath.IsLoop && distanceAlongPath > currentPath.TotalLength)
        {
            distanceAlongPath = 0f;
        }
    }
}
```

### Querying Track System

```csharp
// Get all tracks
List<TrackPiece> allTracks = TrackManager.Instance.GetAllTracks();

// Get all paths
List<TrackPath> allPaths = TrackManager.Instance.GetAllPaths();

// Find nearest connection point
Vector2 position = transform.position;
ConnectionPoint nearest = TrackManager.Instance.FindNearestConnectionPoint(position);

// Get available (unconnected) connection points
List<ConnectionPoint> available = TrackManager.Instance.GetAvailableConnectionPoints();
```

### Manual Track Connection

```csharp
// Connect two tracks programmatically
TrackPiece track1 = // ... get track
TrackPiece track2 = // ... get track

ConnectionPoint cp1 = track1.GetConnectionPoint(0);
ConnectionPoint cp2 = track2.GetConnectionPoint(0);

if (cp1.CanConnectTo(cp2))
{
    TrackManager.Instance.ConnectPoints(cp1, cp2);
}
```

## Visual Debugging

### Scene View Gizmos

When a track is selected in the hierarchy, you'll see:
- **Connection Points**: Colored spheres (green = available, red = connected)
- **Direction Arrows**: Show connection point directions
- **Snap Radius**: Circle showing detection range
- **Waypoints**: Cyan path showing train route

### TrackManager Gizmos

When TrackManager is active:
- **Generated Paths**: Magenta lines showing all connected paths
- Toggle with `Show Path Gizmos` in inspector

## Extending the System

### Adding New Track Types

1. Create new TrackType enum value in `TrackPiece.cs`:
```csharp
public enum TrackType
{
    Straight,
    Curved,
    Junction,
    Switch  // New type
}
```

2. Add waypoint generation logic:
```csharp
case TrackType.Switch:
    GenerateSwitchWaypoints(start, end);
    break;
```

3. Create the track prefab following the setup instructions above

### Custom Connection Validation

Override connection validation in `ConnectionPoint.cs`:
```csharp
public virtual bool CanConnectTo(ConnectionPoint other)
{
    // Add custom logic
    if (customCondition)
        return false;
    
    return base.CanConnectTo(other);
}
```

### Track Removal

To support track removal:
```csharp
public void RemoveTrack(TrackPiece track)
{
    // Disconnect all connections
    foreach (var cp in track.ConnectionPoints)
    {
        cp.Disconnect();
    }
    
    // Unregister from manager
    TrackManager.Instance.UnregisterTrack(track);
    
    // Destroy object
    Destroy(track.gameObject);
}
```

## Performance Considerations

- Connection point searches are O(n) where n is number of tracks
- Path generation is O(n) for n connected tracks
- For large track networks, consider spatial partitioning
- Paths are only regenerated when connections change

## Troubleshooting

### Tracks Won't Snap
- Check that TrackManager exists in scene
- Verify connection point directions are opposite
- Ensure snap radius is large enough
- Check direction tolerance setting

### Waypoints Not Generated
- Enable `Auto Generate Waypoints` on TrackPiece
- Ensure track has at least 2 connection points
- Check that connection points are positioned correctly

### Preview Not Visible
- Check that camera is properly set as Main Camera
- Verify TrackPlacer has prefabs assigned
- Ensure sprites are assigned to prefabs

### Paths Not Updating
- Enable `Auto Generate Paths` on TrackManager
- Check that tracks are properly connected
- Verify tracks are registered with manager

## API Reference

### TrackPiece
- `string TrackID` - Unique identifier
- `TrackType Type` - Type of track
- `ConnectionPoint[] ConnectionPoints` - All connection points
- `Vector2[] WorldWaypoints` - Path waypoints
- `float Length` - Track length
- `void GenerateWaypoints()` - Regenerate waypoints
- `List<TrackPiece> GetConnectedTracks()` - Get connected tracks

### ConnectionPoint
- `ConnectionPoint ConnectedTo` - Connected point
- `bool IsConnected` - Connection status
- `Vector2 WorldPosition` - World position
- `Vector2 WorldDirection` - World direction
- `bool CanConnectTo(ConnectionPoint other)` - Validate connection
- `void ConnectTo(ConnectionPoint other)` - Create connection
- `void Disconnect()` - Remove connection

### TrackManager
- `static TrackManager Instance` - Singleton instance
- `void RegisterTrack(TrackPiece)` - Register track
- `void UnregisterTrack(TrackPiece)` - Unregister track
- `bool TrySnapTrack(TrackPiece)` - Attempt to snap track
- `List<TrackPath> GetAllPaths()` - Get all paths
- `void RegenerateAllPaths()` - Rebuild all paths

### TrackPath
- `List<Vector2> Waypoints` - All waypoints
- `float TotalLength` - Path length
- `bool IsLoop` - Is circular path
- `Vector2 GetPositionAtDistance(float)` - Position along path
- `Vector2 GetDirectionAtDistance(float)` - Direction along path

## License

This code is part of the Off The Rails project.

## Support

For issues or questions, please refer to the project repository.
