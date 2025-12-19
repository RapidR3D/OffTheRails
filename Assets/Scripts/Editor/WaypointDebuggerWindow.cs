using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class WaypointDebuggerWindow : EditorWindow
    {
        private Vector3 currentPosition;
        private bool hasPosition = false;
        private List<Vector3> waypoints = new List<Vector3>();
        private float planeHeight = -11f; // Default to your track height
        private Vector2 scrollPos;
        private bool showVisualGuides = true;
        private string debugInfo = "";

        [MenuItem("Tools/Waypoint Debugger Window")]
        public static void ShowWindow()
        {
            GetWindow<WaypointDebuggerWindow>("Waypoint Debugger");
        }

        private void OnGUI()
        {
            GUILayout.Label("Waypoint Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Plane height control
            EditorGUILayout.BeginHorizontal();
            planeHeight = EditorGUILayout.FloatField("Plane Height (Y):", planeHeight);
            if (GUILayout.Button("-1", GUILayout.Width(40))) planeHeight -= 1f;
            if (GUILayout.Button("-0.1", GUILayout.Width(40))) planeHeight -= 0.1f;
            if (GUILayout.Button("+0.1", GUILayout.Width(40))) planeHeight += 0.1f;
            if (GUILayout.Button("+1", GUILayout.Width(40))) planeHeight += 1f;
            EditorGUILayout.EndHorizontal();

            showVisualGuides = EditorGUILayout.Toggle("Show Visual Guides", showVisualGuides);
        
            // Debug info
            if (!string.IsNullOrEmpty(debugInfo))
            {
                EditorGUILayout.HelpBox(debugInfo, MessageType.Info);
            }
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Cursor Position:", EditorStyles.boldLabel);
        
            if (hasPosition)
            {
                // Make these big and easy to read
                GUIStyle bigText = new GUIStyle(GUI.skin.label);
                bigText.fontSize = 14;
                bigText.fontStyle = FontStyle.Bold;
            
                EditorGUILayout.LabelField($"X: {currentPosition.x:F3}", bigText);
                EditorGUILayout.LabelField($"Y: {currentPosition.y:F3}", bigText);
                EditorGUILayout.LabelField($"Z: {currentPosition.z:F3}", bigText);
            
                EditorGUILayout.Space();
            
                if (GUILayout.Button("Log Current Position (F2)", GUILayout.Height(30)))
                {
                    waypoints.Add(currentPosition);
                    Debug.Log($"Waypoint {waypoints.Count}: {currentPosition}");
                }
            
                if (GUILayout.Button("Copy Current Position"))
                {
                    EditorGUIUtility.systemCopyBuffer = $"new Vector3({currentPosition.x:F3}f, {currentPosition.y:F3}f, {currentPosition.z:F3}f)";
                    Debug.Log($"Copied: {currentPosition}");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Move mouse over Scene view to see position", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Logged Waypoints: {waypoints.Count}", EditorStyles.boldLabel);
        
            if (waypoints.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy All as Array"))
                {
                    CopyWaypointsAsArray();
                }
            
                if (GUILayout.Button("Clear All"))
                {
                    waypoints.Clear();
                }
                EditorGUILayout.EndHorizontal();
            
                EditorGUILayout.Space();
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            
                for (int i = 0; i < waypoints.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}:", GUILayout.Width(30));
                    EditorGUILayout.SelectableLabel($"({waypoints[i].x:F2}, {waypoints[i].y:F2}, {waypoints[i].z:F2})", GUILayout.Height(16));
                    if (GUILayout.Button("Del", GUILayout.Width(40)))
                    {
                        waypoints.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            
                EditorGUILayout.EndScrollView();
            }
        
            // Force constant repainting to update cursor position
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (sceneView == null) return;
        
            Event e = Event.current;
        
            // Handle F2 hotkey for logging waypoint
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F2 && hasPosition)
            {
                waypoints.Add(currentPosition);
                Debug.Log($"Waypoint {waypoints.Count}: {currentPosition}");
                e.Use();
            }

            Vector2 mousePos = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        
            debugInfo = $"Mouse: {mousePos} | Ray Origin: {ray.origin:F1}";
        
            // ALWAYS use plane projection (since tracks have no colliders)
            Plane plane = new Plane(Vector3.up, new Vector3(0, planeHeight, 0));
            if (plane.Raycast(ray, out float enter))
            {
                currentPosition = ray.GetPoint(enter);
                hasPosition = true;
                debugInfo += " | HIT!";
            }
            else
            {
                hasPosition = false;
                debugInfo += " | NO HIT";
            }

            // Draw visual guides
            if (showVisualGuides && hasPosition)
            {
                // Draw crosshair at cursor position
                Handles.color = Color.cyan;
                float size = 1.0f;
                Handles.DrawLine(currentPosition + Vector3.left * size, currentPosition + Vector3.right * size);
                Handles.DrawLine(currentPosition + Vector3.forward * size, currentPosition + Vector3.back * size);
                Handles.DrawWireCube(currentPosition, Vector3.one * 0.5f);
            
                // Draw a vertical line to show the plane height
                Handles.color = Color.red;
                Handles.DrawLine(currentPosition, currentPosition + Vector3.up * 2f);
                Handles.DrawLine(currentPosition, currentPosition + Vector3.down * 2f);
            
                // Draw reference grid at plane height
                Handles.color = new Color(0, 1, 0, 0.3f);
                Vector3 gridCenter = new Vector3(
                    Mathf.Round(currentPosition.x / 5f) * 5f,
                    planeHeight,
                    Mathf.Round(currentPosition.z / 5f) * 5f
                );
            
                for (int x = -5; x <= 5; x++)
                {
                    Vector3 start = gridCenter + new Vector3(x * 5f, 0, -25f);
                    Vector3 end = gridCenter + new Vector3(x * 5f, 0, 25f);
                    Handles.DrawLine(start, end);
                }
            
                for (int z = -5; z <= 5; z++)
                {
                    Vector3 start = gridCenter + new Vector3(-25f, 0, z * 5f);
                    Vector3 end = gridCenter + new Vector3(25f, 0, z * 5f);
                    Handles.DrawLine(start, end);
                }
            
                // Draw waypoints
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Handles.color = Color.green;
                    Handles.SphereHandleCap(0, waypoints[i], Quaternion.identity, 0.5f, EventType.Repaint);
                
                    // Draw label
                    Handles.Label(waypoints[i] + Vector3.up * 1f, $"WP{i + 1}", new GUIStyle()
                    {
                        normal = { textColor = Color.white },
                        fontSize = 16,
                        fontStyle = FontStyle.Bold
                    });
                
                    // Draw line to next waypoint
                    if (i < waypoints.Count - 1)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawLine(waypoints[i], waypoints[i + 1]);
                    }
                }
            }
        
            sceneView.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Debug.Log("Waypoint Debugger Window enabled - move mouse over Scene view");
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void CopyWaypointsAsArray()
        {
            string result = "Vector3[] waypoints = new Vector3[]\n{\n";
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 wp = waypoints[i];
                result += $" new Vector3({wp.x:F3}f, {wp. y:F3}f, {wp.z:F3}f)";
                if (i < waypoints.Count - 1) result += ",";
                result += "\n";
            }
            result += "};";
        
            EditorGUIUtility.systemCopyBuffer = result;
            Debug.Log($"Copied {waypoints.Count} waypoints as array:\n{result}");
        }
    }
}