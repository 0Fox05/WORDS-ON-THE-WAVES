using UnityEngine;
using UnityEditor;

public class GameStateTester : EditorWindow
{
    private GameManager.GameState selectedState;
    private string chosenLocation = "";

    [MenuItem("Tools/Game State Tester")]
    public static void ShowWindow()
    {
        GetWindow<GameStateTester>("Game State Tester");
    }

    void OnGUI()
    {
        GUILayout.Label("Game State Tester", EditorStyles.boldLabel);

        // Dropdown for selecting a state
        selectedState = (GameManager.GameState)EditorGUILayout.EnumPopup("Select State", selectedState);

        // Text field for location (only used if Service state is chosen)
        if (selectedState == GameManager.GameState.Service)
        {
            chosenLocation = EditorGUILayout.TextField("Chosen Location", chosenLocation);
        }

        // Button to trigger ChangeState
        if (GUILayout.Button("Apply State"))
        {
            if (Application.isPlaying)
            {
                if (GameManager.Instance != null)
                {
                    // Set location if Service
                    if (selectedState == GameManager.GameState.Service)
                        GameManager.Instance.TheChosenLocation = chosenLocation;

                    GameManager.Instance.ChangeState(selectedState);
                    Debug.Log($"Triggered ChangeState → {selectedState} (Location: {chosenLocation})");
                }
                else
                {
                    Debug.LogError("GameManager.Instance is null — make sure GameManager is in the scene.");
                }
            }
            else
            {
                Debug.LogWarning("Enter Play Mode to test state changes.");
            }
        }
    }
}
