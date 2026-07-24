using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameState CurrentState;
    public static GameManager Instance;
    public string TheChosenLocation;
    public int point;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void start()
    {
        SoundManager.Instance.MuteLocationMusic(true);
    }

    public void IncreasePoint()
    {
        point += 1;
        Debug.Log("Point increased by 1");
        CheckPoints();
    }
    public void ResetPoint()
    {
        point = 0;
    }
    public void CheckPoints()
    {
        if (point > 1)
        {
            // Do something here
            Debug.Log("Player has more than 1 point!");

            UIManager.Instance.ShowTextCombo(point);
        }
        else
        {
            if (point == 1)
            {
                Debug.Log("Player + 1 point!");
            }
            else
            {
                ResetPoint();
                Debug.Log("Player has 0 point.");
            }
        }
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
                SoundManager.Instance.MuteLocationMusic(true);
                break;
            case GameState.Menu:
                UIManager.Instance.ShowMenu();
                CartDeco.Instance.UpdateItems();
                ClearAllShelves();
                UIManager.Instance.UpdatePlayerDataUI();
                SoundManager.Instance.MuteLocationMusic(true);
                ShopManager.Instance.StupidPlayer();
                break;
            case GameState.Cargo:
                UIManager.Instance.ShowDecor();
                CartDeco.Instance.RefreshItems();
                break;
            case GameState.Preparation:
                UIManager.Instance.ShowPreparation();
                CartDeco.Instance.RefreshItems();
                CartDeco.Instance.UpdateItems();
                TheChosenLocation = UIManager.Instance.ChosenLocation;
                SoundManager.Instance.MuteLocationMusic(true);
                break;
            case GameState.Service:
                UIManager.Instance.ShowService(TheChosenLocation);
                CartDeco.Instance.UpdateItems();
                BookCalculate.Instance.UpdateUI();
                CartDeco.Instance.RefreshItems();
                PlayLocationSong();
                break;
        }
    }

    public void PlayLocationSong()
    {
        SoundManager.Instance.MuteLocationMusic(false);
        if (string.IsNullOrEmpty(TheChosenLocation))
        {
            Debug.LogWarning("No chosen location set!");
            return;
        }

        int index = -1;
        switch (TheChosenLocation)
        {
            case "Far Horizons": index = 3; break;
            case "Morning Cafe": index = 1; break;
            case "Grad Station": index = 2; break;
            case "Central Square": index = 0; break;
            default:
                Debug.LogWarning($"No audio mapped for location: {TheChosenLocation}");
                return;
        }

        // ✅ Use the dedicated locationMusicSource so default background keeps playing
        if (index >= 0 && index < SoundManager.Instance.locationClips.Count)
        {
            SoundManager.Instance.locationMusicSource.clip = SoundManager.Instance.locationClips[index];
            SoundManager.Instance.locationMusicSource.loop = true;
            SoundManager.Instance.locationMusicSource.volume = SoundManager.Instance.musicSource.volume; // tie to same slider
            SoundManager.Instance.locationMusicSource.Play();
            Debug.Log($"Playing location song for {TheChosenLocation}");
        }
    }



    private void ClearAllShelves()
    {
        GameObject[] shelves = GameObject.FindGameObjectsWithTag("Shelf");
        foreach (GameObject shelf in shelves)
        {
            // Destroy every child object inside the shelf
            foreach (Transform child in shelf.transform)
            {
                if (child.CompareTag("Book"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        Debug.Log("All shelves cleared of child objects.");
        UIManager.Instance.UpdateShopTexts(BookCategory.Crime, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Drama, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Fact, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Fantasy, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Classic, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Kids, 0);
        UIManager.Instance.UpdateShopTexts(BookCategory.Travel, 0);
    }
    public void BackResetMenu()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("Game");
    }
    public void PauseGame()
    {
        Time.timeScale = 0f; // stops physics and animations
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f; // resumes normal speed
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Game")
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // unsubscribe so it only runs once
            ResetPoint();
        }
    }
}
