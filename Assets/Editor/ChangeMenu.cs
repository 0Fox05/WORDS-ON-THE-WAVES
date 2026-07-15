using UnityEngine;
using UnityEditor;

public class ChangeMenu : EditorWindow
{
    [MenuItem("Window/Change Menu")]
    public static void ShowWindow()
    {
        GetWindow<ChangeMenu>("Change Menu");
    }

    private void OnGUI()
    {
        GUILayout.Label("Game State Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Switch to Menu State"))
        {
            ChangeMenuState();
        }
    }

    private void ChangeMenuState()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Menu);
            Debug.Log("GameManager state changed to Menu");
        }
        else
        {
            Debug.LogError("GameManager instance not found in scene!");
        }
    }
}
