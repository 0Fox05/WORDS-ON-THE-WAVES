using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Panels")]
    public GameObject MenuPanel;
    public GameObject CargoPanel;
    public GameObject PreparationPanel;
    public GameObject ServicePanel;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowMenu()
    {
        HideAll();
        MenuPanel.SetActive(true);
        Debug.Log("Showing Menu UI");
    }

    public void ShowCargo()
    {
        HideAll();
        CargoPanel.SetActive(true);
        Debug.Log("Showing Cargo UI");
    }

    public void ShowPreparation()
    {
        HideAll();
        PreparationPanel.SetActive(true);
        Debug.Log("Showing Preparation UI");
    }

    public void ShowService()
    {
        HideAll();
        ServicePanel.SetActive(true);
        Debug.Log("Showing Service UI");
    }

    private void HideAll()
    {
        MenuPanel.SetActive(false);
        CargoPanel.SetActive(false);
        PreparationPanel.SetActive(false);
        ServicePanel.SetActive(false);
    }
}