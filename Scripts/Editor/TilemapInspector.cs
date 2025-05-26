using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapInspector : EditorWindow
{
    private Tilemap tilemap;
    private bool isInspectorEnabled = false; // Toggle for enabling/disabling the inspector

    [MenuItem("Tools/Tilemap Inspector")]
    public static void ShowWindow()
    {
        GetWindow<TilemapInspector>("Tilemap Inspector");
    }

    private void OnGUI()
    {
        // Select the tilemap
        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", tilemap, typeof(Tilemap), true);

        // Enable/disable toggle
        isInspectorEnabled = EditorGUILayout.Toggle("Enable Inspector", isInspectorEnabled);

        if (tilemap == null)
        {
            EditorGUILayout.HelpBox("Please assign a Tilemap to inspect.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("Click on a tile in the Scene view to get the tile name and position.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        // Only run if the inspector is enabled and a tilemap is assigned
        if (!isInspectorEnabled || tilemap == null) return;

        Event e = Event.current;

        // Check for left-click and that it's focused on the Scene view
        if (e.type == EventType.MouseDown && e.button == 0 && SceneView.currentDrawingSceneView == sceneView)
        {
            Vector2 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            Vector3Int cellPos = tilemap.WorldToCell(mousePos);

            TileBase clickedTile = tilemap.GetTile(cellPos);

            if (clickedTile != null)
            {
                Debug.Log($"Tile Name: {clickedTile.name}, Position: {cellPos}");
                ShowNotification(new GUIContent($"Tile: {clickedTile.name} at {cellPos}"));
            }
            else
            {
                Debug.Log("No tile at this position.");
            }

            e.Use();
        }
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
}