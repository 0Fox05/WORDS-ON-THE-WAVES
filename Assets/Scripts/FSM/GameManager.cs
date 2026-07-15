using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameState CurrentState;
    public static GameManager Instance;
    public string TheChosenLocation;
    public int point;

    private void Awake()
    {
        Instance = this;
    }
    public void start()
    {

    }

    public void IncreasePoint()
    {
        point += 1;
        Debug.Log("Point increased by 1");
    }
    public void ResetPoint()
    {
        point = 0;
    }
    public enum GameState
    {
        Home,
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
            case GameState.Home:
                UIManager.Instance.ShowHome();
                break;
            case GameState.Menu:
                UIManager.Instance.ShowMenu();
                CartDeco.Instance.UpdateItems();
                break;
            case GameState.Cargo:
                UIManager.Instance.ShowDecor();
                break;
            case GameState.Preparation:
                UIManager.Instance.ShowPreparation();
                CartDeco.Instance.UpdateItems();
                break;
            case GameState.Service:
                TheChosenLocation = UIManager.Instance.ChosenLocation;
                UIManager.Instance.ShowService(TheChosenLocation);
                CartDeco.Instance.UpdateItems();
                BookCalculate.Instance.UpdateUI();
                CartDeco.Instance.RefreshItems();
                break;
        }
    }
}
