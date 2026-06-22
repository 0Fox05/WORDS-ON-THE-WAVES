using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameState CurrentState;
    public static GameManager Instance;
    private void Awake()
    {
        Instance = this;
    }
    public enum GameState
    {
        Menu,
        Cargo,
        Preparation,
        Service
    }
    public void ChangeState(GameState newState)
    {
        CurrentState = newState;

        switch (newState)
        {
            case GameState.Menu:
                UIManager.Instance.ShowMenu();
                break;
            case GameState.Cargo:
                UIManager.Instance.ShowCargo();
                break;
            case GameState.Preparation:
                UIManager.Instance.ShowPreparation();
                break;
            case GameState.Service:
                UIManager.Instance.ShowService();
                break;
        }
    }
}
