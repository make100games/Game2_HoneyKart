using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TreeCrownSpawner))]
public class TreeCrownSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        
        TreeCrownSpawner spawner = (TreeCrownSpawner)target;
        
        if (GUILayout.Button("Spawn Balls"))
        {
            spawner.SpawnBalls();
        }
    }
}
