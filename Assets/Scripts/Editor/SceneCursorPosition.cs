using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

/// <summary>
/// Editor tool that displays the cursor position in the Scene view.
/// Shows world coordinates where the mouse is hovering.
/// Press C to copy position while hovering in Scene view!
/// </summary>
public class SceneCursorPosition : OdinEditorWindow
{
    [ShowInInspector, ReadOnly, BoxGroup("World Position")]
    [SuffixLabel("units", Overlay = true)]
    private static float X => cursorWorldPos.x;
    
    [ShowInInspector, ReadOnly, BoxGroup("World Position")]
    [SuffixLabel("units", Overlay = true)]
    private static float Y => cursorWorldPos.y;
    
    [ShowInInspector, ReadOnly, BoxGroup("World Position")]
    [SuffixLabel("units", Overlay = true)]
    private static float Z => cursorWorldPos.z;
    
    [ShowInInspector, ReadOnly, BoxGroup("Formatted")]
    [DisplayAsString, GUIColor(0.3f, 1f, 0.3f)]
    private static string Position2D => $"({cursorWorldPos.x:F2}, {cursorWorldPos.y:F2})";
    
    [ShowInInspector, ReadOnly, BoxGroup("Formatted")]
    [DisplayAsString]
    private static string Position3D => $"({cursorWorldPos.x:F2}, {cursorWorldPos.y:F2}, {cursorWorldPos.z:F2})";
    
    [InfoBox("Press F1 in Scene view to copy position!\nPress F2 to copy as Vector2")]
    [ToggleLeft]
    public bool trackCursor = true;
    
    [ToggleLeft]
    public bool showLabelInScene = true;
    
    [ShowInInspector, ReadOnly, BoxGroup("Last Copied")]
    [GUIColor(1f, 1f, 0.5f)]
    private static string lastCopied = "(none)";
    
    private static Vector3 cursorWorldPos;
    private static SceneCursorPosition instance;
    
    [MenuItem("Tools/Scene Cursor Position")]
    public static void ShowWindow()
    {
        instance = GetWindow<SceneCursorPosition>("Cursor Position");
        instance.Show();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        SceneView.duringSceneGui += OnSceneGUI;
        instance = this;
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    
    [Button(ButtonSizes.Large), GUIColor(0, 1, 0.5f)]
    [BoxGroup("Actions")]
    private void CopyPosition2D()
    {
        DoCopyPosition2D();
    }
    
    [Button(ButtonSizes.Medium)]
    [BoxGroup("Actions")]
    private void CopyAsVector2()
    {
        DoCopyAsVector2();
    }
    
    [Button(ButtonSizes.Medium)]
    [BoxGroup("Actions")]
    private void CopyAsVector3()
    {
        lastCopied = $"new Vector3({cursorWorldPos.x:F2}f, {cursorWorldPos.y:F2}f, {cursorWorldPos.z:F2}f)";
        EditorGUIUtility.systemCopyBuffer = lastCopied;
        Debug.Log($"Copied: {lastCopied}");
    }
    
    private static void DoCopyPosition2D()
    {
        lastCopied = $"({cursorWorldPos.x:F2}, {cursorWorldPos.y:F2})";
        EditorGUIUtility.systemCopyBuffer = lastCopied;
        Debug.Log($"Copied: {lastCopied}");
    }
    
    private static void DoCopyAsVector2()
    {
        lastCopied = $"new Vector2({cursorWorldPos.x:F2}f, {cursorWorldPos.y:F2}f)";
        EditorGUIUtility.systemCopyBuffer = lastCopied;
        Debug.Log($"Copied: {lastCopied}");
    }
    
    private static void OnSceneGUI(SceneView sceneView)
    {
        if (instance == null || !instance.trackCursor) return;
        
        Event e = Event.current;
        
        // HOTKEY: Press F1 to copy position, F2 to copy as Vector2
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.F1)
            {
                DoCopyPosition2D();
                e.Use();
            }
            else if (e.keyCode == KeyCode.F2)
            {
                DoCopyAsVector2();
                e.Use();
            }
        }
        
        // Get mouse position in scene view
        Vector3 mousePos = e.mousePosition;
        
        // Convert to world coordinates (for 2D, we use a plane at Z=0)
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        
        if (plane.Raycast(ray, out float distance))
        {
            cursorWorldPos = ray.GetPoint(distance);
        }
        
        // Draw position label at cursor
        if (instance.showLabelInScene)
        {
            Handles.BeginGUI();
            Vector2 guiPos = HandleUtility.WorldToGUIPoint(cursorWorldPos);
            
            // Draw background box
            GUI.Box(new Rect(guiPos.x + 12, guiPos.y - 12, 140, 22), "");
            
            // Draw label
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(guiPos.x + 15, guiPos.y - 10, 200, 20), 
                $"({cursorWorldPos.x:F1}, {cursorWorldPos.y:F1}) [F1]", 
                style);
            Handles.EndGUI();
        }
        
        // Force repaint
        sceneView.Repaint();
        
        // Also repaint our window
        if (instance != null)
        {
            instance.Repaint();
        }
    }
}