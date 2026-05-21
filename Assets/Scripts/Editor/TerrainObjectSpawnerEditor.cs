using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainObjectSpawner))]
public class TerrainObjectSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        
        TerrainObjectSpawner spawner = (TerrainObjectSpawner)target;
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Spawn Objects", GUILayout.Height(30)))
        {
            spawner.SpawnObjects();
        }
        
        if (GUILayout.Button("Clear Objects", GUILayout.Height(30)))
        {
            spawner.ClearObjects();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("The yellow rectangle shows the spawn area. Green spheres show spawn positions with raycasts pointing down.", MessageType.Info);
    }
}
