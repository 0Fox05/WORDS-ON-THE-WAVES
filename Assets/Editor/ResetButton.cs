using UnityEngine;
using UnityEditor;

public class ResetPlayerWindow : EditorWindow
{
    [MenuItem("Tools/Reset Player Data")]
    public static void ShowWindow()
    {
        // Create and show the window
        GetWindow<ResetPlayerWindow>("Reset Player");
    }

    void OnGUI()
    {
        GUILayout.Label("Player Data Reset Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Reset Player Data"))
        {
            // Try to find DataManager in the scene
            DataManager manager = FindObjectOfType<DataManager>();
            if (manager != null)
            {
                manager.ResetPlayer();
                Debug.Log("Player data reset via EditorWindow.");
            }
            else
            {
                Debug.LogError("No DataManager found in the scene!");
            }
        }
    }
}
