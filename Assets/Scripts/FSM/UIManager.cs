using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [Header("Cameras")]
    public CinemachineCamera[] cameras;
    [Header("Home Panel")]
    public GameObject HomePanel;
    public Button PlayButton;
    [Header("Menu Panels")]
    public GameObject MenuPanel;
    public GameObject StorePanel;
    [Header("Store Panels")]
    public GameObject BoxGetUI;
    public GameObject ItemGetUI;
    public Button[] BuyBox;
    public List<TextMeshProUGUI> BNameTexts;
    public Button[] BuyItem;
    public List<TextMeshProUGUI> INameTexts;
    public Button[] ConfirmButtons;
    [Header("Crate Sold Filters")]
    public List<GameObject> CrateSoldFilters;   // assign in Inspector, same order as BuyBox
    public List<GameObject> ItemHave;

    [Header("Menu Maps Panels")]
    public Button[] Maps;
    public List<GameObject> MapCanNot;

    public GameObject BuyBoxPanel;
    public GameObject BuyItemPanel;
    public GameObject SettingPanel;

    [Header("Cargo Panels")]
    public GameObject CargoPanel;

    [Header("Preparation Panels")]
    public string ChosenLocation;
    public GameObject PreparationPanel;
    [Header("for Ingame and prepare Panels")]
    [Header("Number Crime")]
    public List<TextMeshProUGUI> SCrimeTexts;
    [Header("Number Drama")]
    public List<TextMeshProUGUI> SDramaTexts;
    [Header("Number Fact")]
    public List<TextMeshProUGUI> SFactTexts;
    [Header("Number Fantasy")]
    public List<TextMeshProUGUI> SFantasyTexts;
    [Header("Number Classic")]
    public List<TextMeshProUGUI> SClassicTexts;
    [Header("Number Kids")]
    public List<TextMeshProUGUI> SKidsTexts;
    [Header("Number Travel")]
    public List<TextMeshProUGUI> STravelTexts;
    public Button LetsGoButton;
    public Button BackButton;
    public GameObject LoadingScreen;
    public CanvasGroup LoadingCanvasGroup;

    [Header("Service Panels")]
    public GameObject ServicePanel;
    [Header("Special")]
    public Button[] SettingButtons;
    public Button[] DecorButtons;
    public Button[] StoreButtons;
    [Header("PlayerData")]
    public Button ResetToMenu;
    [Header("Money")]
    public List<TextMeshProUGUI> MoneyTexts;
    [Header("Number Crime have")]
    public List<TextMeshProUGUI> CrimeTexts;
    [Header("Number Drama have")]
    public List<TextMeshProUGUI> DramaTexts;
    [Header("Number Fact have")]
    public List<TextMeshProUGUI> FactTexts;
    [Header("Number Fantasy have")]
    public List<TextMeshProUGUI> FantasyTexts;
    [Header("Number Classic have")]
    public List<TextMeshProUGUI> ClassicTexts;
    [Header("Number Kids have")]
    public List<TextMeshProUGUI> KidsTexts;
    [Header("Number Travel have")]
    public List<TextMeshProUGUI> TravelTexts;

    private void Awake()
    {
        Instance = this;

        // Keep functional listeners
        PlayButton.onClick.AddListener(() => GameManager.Instance.ChangeState(GameManager.GameState.Menu));
        LetsGoButton.onClick.AddListener(() => GameManager.Instance.ChangeState(GameManager.GameState.Service));
        BackButton.onClick.AddListener(() => GameManager.Instance.ChangeState(GameManager.GameState.Menu));
        LoadingScreen.SetActive(false);

        foreach (Button btn in StoreButtons)
        {
            btn.onClick.AddListener(ShowStore);
        }
        foreach (Button btn in SettingButtons)
        {
            btn.onClick.AddListener(ShowSetting);
            btn.onClick.AddListener(Paused);
        }
        foreach (Button btn in DecorButtons)
        {
            btn.onClick.AddListener(() => GameManager.Instance.ChangeState(GameManager.GameState.Cargo));
        }
        foreach (Button btn in ConfirmButtons)
        {
            btn.onClick.AddListener(CleanUIShop);
        }
    }

    private void Start()
    {
        ShowHome();
        DataManager.Instance.LoadPlayer();   // load from JSON
        UpdatePlayerDataUI();                // now update UI from loaded data
        UpdateMoneyUI(DataManager.Instance.PlayerData.Money);
        for (int i = 0; i < Maps.Length; i++)
        {
            int index = i;
            string locationName = "";

            switch (index)
            {
                case 0: locationName = "Far Horizons"; break;
                case 1: locationName = "Morning Cafe"; break;
                case 2: locationName = "Grad Station"; break;
                case 3: locationName = "Central Square"; break;
            }

            Maps[index].onClick.AddListener(() => SavingLocation(locationName));
        }

        for (int i = 0; i < CrateSoldFilters.Count; i++)
        {
            if (CrateSoldFilters[i] != null)
                CrateSoldFilters[i].SetActive(false);
        }

        var crates = DataManager.Instance.CrateData.Crates;

        // Create a temporary list that duplicates the first crate to fill 4 buttons
        List<Crate> displayCrates = new List<Crate>(crates);

        if (displayCrates.Count < BuyBox.Length)
        {
            // Duplicate the first crate until we have enough
            while (displayCrates.Count < BuyBox.Length)
            {
                displayCrates.Add(crates[0]);
            }
        }

        // Wire buttons and names
        for (int i = 0; i < BuyBox.Length; i++)
        {
            // Loop back if there are fewer crates than buttons
            int realIndex = i % crates.Count;
            var crate = crates[realIndex];

            // Update name text
            if (i < BNameTexts.Count)
            {
                BNameTexts[i].text = $"{crate.Name} - ${crate.Price}";
            }

            // Add listener using the button index (not realIndex)
            int buttonIndex = i;
            BuyBox[buttonIndex].onClick.AddListener(() => OnBuyBoxClicked(buttonIndex, realIndex));
        }

        // Wire shop items
        var shopItems = DataManager.Instance.ShopData.ItemsShop;

        List<ShopItem> randomItems = new List<ShopItem>();
        if (shopItems.Count > 0)
        {
            System.Random rng = new System.Random();
            while (randomItems.Count < 2 && randomItems.Count < shopItems.Count)
            {
                var candidate = shopItems[rng.Next(shopItems.Count)];
                if (!randomItems.Contains(candidate))
                    randomItems.Add(candidate);
            }
        }

        for (int i = 0; i < BuyItem.Length && i < randomItems.Count; i++)
        {
            int index = i;
            var shopItem = randomItems[index];

            // Update name text
            if (index < INameTexts.Count)
            {
                INameTexts[index].text = $"{shopItem.Name} - ${shopItem.Money}";
            }

            // Add listener
            BuyItem[index].onClick.AddListener(() => OnBuyItemClicked(shopItem.Name));
        }
        ResetToMenu.onClick.AddListener(() => ResetCall());
        for (int i = 0; i < randomItems.Count && i < ItemHave.Count && i < BuyItem.Length; i++)
        {
            var shopItem = randomItems[i];
            var cartItem = DataManager.Instance.PlayerData.ItemsCart.Find(c => c.Name == shopItem.Name);

            bool owned = cartItem != null && cartItem.Have > 0;

            ItemHave[i].SetActive(owned);          // show filter if owned
            BuyItem[i].interactable = !owned;      // disable button if owned
        }
    }
    public void ResetCall()
    {
        GameManager.Instance.BackResetMenu();
    }
    public void ShowHome()
    {
        HideAll();
        HomePanel.SetActive(true);
        Debug.Log("Showing Home UI");
    }
    public void ShowMenu()
    {
        HideAll();
        MenuPanel.SetActive(true);
        ShowMapCan();
        Debug.Log("Showing Menu UI");
    }

    public void ShowStore()
    {
        if (StorePanel.activeSelf)
        {
            BuyBoxPanel.SetActive(false);
            BuyItemPanel.SetActive(false);
            StorePanel.SetActive(false);
            Debug.Log("Hiding Store UI");
        }
        else
        {
            BuyBoxPanel.SetActive(false);
            BuyItemPanel.SetActive(false);
            StorePanel.SetActive(true);
            Debug.Log("Showing Store UI");
        }
    }


    public void ShowSetting()
    {
        if (!SettingPanel.activeSelf)
        {
            SettingPanel.SetActive(true);
        }
        else
        {
            SettingPanel.SetActive(false);
        }
        Debug.Log("Showing Setting UI");
    }

    public void ShowDecor()
    {      
        HideAll();
        CargoPanel.SetActive(true);
        Debug.Log("Showing decor UI");
        // Switch camera without showing ServicePanel
        CartDeco.Instance.UpdateItems();
        int camIndex = DataManager.Instance.GetCameraIndexForLocation("Cart");
        ActivateCamera(camIndex);
    }
    public void SavingLocation(string location)
    {
        // Find the location entry in DataManager
        var locEntry = DataManager.Instance.LocationData.Locations
            .Find(l => l.Name == location);

        if (locEntry != null)
        {
            int currentMoney = DataManager.Instance.GetMoney();

            // Special case: Central Square is always allowed if money == 0
            if (location == "Central Square" && currentMoney == 0)
            {
                ChosenLocation = location;
                GameManager.Instance.ChangeState(GameManager.GameState.Preparation);
                Debug.Log($"Special case: Player allowed to go to {location} with 0 money.");
                return;
            }

            // Normal case: check travel fee
            if (currentMoney >= locEntry.TravelFee)
            {
                ChosenLocation = location;
                GameManager.Instance.ChangeState(GameManager.GameState.Preparation);
                Debug.Log($"Location {location} saved. Travel fee: {locEntry.TravelFee}");
            }
            else
            {
                Debug.LogWarning($"Not enough money to choose {location}. Fee: {locEntry.TravelFee}, Current: {currentMoney}");
            }
        }
        else
        {
            Debug.LogWarning($"Location {location} not found in LocationData!");
        }
    }
    public void ShowMapCan()
    {
        int currentMoney = DataManager.Instance.GetMoney();

        // Step 1: deactivate all overlays first
        for (int i = 0; i < MapCanNot.Count; i++)
        {
            if (MapCanNot[i] != null)
                MapCanNot[i].SetActive(false);
        }

        // Step 2: check each location and activate overlay if not affordable
        for (int i = 0; i < Maps.Length; i++)
        {
            string locName = "";
            switch (i)
            {
                case 0: locName = "Far Horizons"; break;
                case 1: locName = "Morning Cafe"; break;
                case 2: locName = "Grad Station"; break;
                case 3: locName = "Central Square"; break;
            }

            var locEntry = DataManager.Instance.LocationData.Locations
                .Find(l => l.Name == locName);

            if (locEntry != null && currentMoney < locEntry.TravelFee)
            {
                if (i < MapCanNot.Count && MapCanNot[i] != null)
                    MapCanNot[i].SetActive(true);
            }
        }
        Debug.LogWarning($"Map Scan Ran");
    }
    public void ShowPreparation()
    {
        HideAll();
        PreparationPanel.SetActive(true);
        Debug.Log("Showing Preparation UI");
        // Switch camera without showing ServicePanel
        int camIndex = DataManager.Instance.GetCameraIndexForLocation("Shelf");
        ActivateCamera(camIndex);
    }

    public void ShowService(string locationName)
    {       
        SyncShopTexts();
        HideAll();
        ServicePanel.SetActive(true);
        Debug.Log("Showing Service UI");

        // Switch camera based on location
        int camIndex = DataManager.Instance.GetCameraIndexForLocation(locationName);
        ActivateCamera(camIndex);

        // Find the location entry in DataManager
        var locEntry = DataManager.Instance.LocationData.Locations
            .Find(l => l.Name == ChosenLocation);

        if (locEntry != null)
        {
            // Deduct the travel fee
            DataManager.Instance.ChangeMoney(-locEntry.TravelFee);
            Debug.Log($"Saved location {ChosenLocation}, deducted fee: {locEntry.TravelFee}");
        }
        else
        {
            Debug.LogWarning($"Location {ChosenLocation} not found in LocationData!");
        }
    }

    private void HideAll()
    {
        HomePanel.SetActive(false);
        MenuPanel.SetActive(false);
        SettingPanel.SetActive(false);
        CargoPanel.SetActive(false);
        StorePanel.SetActive(false);
        PreparationPanel.SetActive(false);
        ServicePanel.SetActive(false);
    }

    private void ActivateCamera(int index)
    {
        // Call the coroutine for loading screen
        StartCoroutine(ShowLoadingScreen());

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
                cameras[i].Priority = (i == index) ? 10 : 1;
        }
        Debug.Log("Activated camera #" + (index + 1));
        CartPlaceManage.Instance.NowMoveTo(index);
    }

    private void OnBuyBoxClicked(int buttonIndex, int realIndex)
    {
        if (ShopManager.Instance == null)
        {
            Debug.LogError("ShopManager instance is missing!");
            return;
        }

        // Check if player has enough money before buying
        var crate = DataManager.Instance.CrateData.Crates[realIndex];
        if (DataManager.Instance.PlayerData.Money >= crate.Price)
        {
            // Buy the actual crate
            ShopManager.Instance.BuyCrate(realIndex);
            UpdatePlayerDataUI();

            // Show "Sold" filter for this specific button
            if (buttonIndex < CrateSoldFilters.Count && CrateSoldFilters[buttonIndex] != null)
            {
                CrateSoldFilters[buttonIndex].SetActive(true);
            }

            // Disable this specific button
            if (buttonIndex < BuyBox.Length && BuyBox[buttonIndex] != null)
            {
                BuyBox[buttonIndex].interactable = false;
            }
        }
        else
        {
            Debug.Log("Not enough money to buy crate " + crate.Name);
            // Do nothing: no filter, no disable
        }
    }



    private void OnBuyItemClicked(string itemName)
    {
        if (DataManager.Instance == null || DataManager.Instance.ShopData == null)
        {
            Debug.LogError("Shop data not loaded!");
            return;
        }

        var shopItem = DataManager.Instance.ShopData.ItemsShop.Find(i => i.Name == itemName);
        if (shopItem == null)
        {
            Debug.LogWarning("Item not found in shop: " + itemName);
            return;
        }

        // Try to buy
        if (DataManager.Instance.PlayerData.Money >= shopItem.Money)
        {
            DataManager.Instance.ChangeMoney(-shopItem.Money);

            var cartItem = DataManager.Instance.PlayerData.ItemsCart.Find(i => i.Name == itemName);
            if (cartItem != null)
                cartItem.Have++;
            else
                DataManager.Instance.PlayerData.ItemsCart.Add(new PlayerCartEntry { Name = itemName, Have = 1 });

            DataManager.Instance.SavePlayer();
            UpdatePlayerDataUI();
            CartDeco.Instance.UpdateItems();
            Debug.Log($"Bought {itemName} for {shopItem.Money}");

            // ✅ Refresh filters and disable buttons
            for (int i = 0; i < BuyItem.Length && i < ItemHave.Count; i++)
            {
                var candidate = DataManager.Instance.ShopData.ItemsShop.Find(s => INameTexts[i].text.StartsWith(s.Name));
                if (candidate != null)
                {
                    var owned = DataManager.Instance.PlayerData.ItemsCart.Exists(c => c.Name == candidate.Name && c.Have > 0);
                    ItemHave[i].SetActive(owned);
                    BuyItem[i].interactable = !owned;
                }
            }
        }
        else
        {
            Debug.Log("Not enough money to buy " + itemName);
        }
    }


    public void UpdateMoneyUI(int money)
    {
        foreach (var text in MoneyTexts)
        {
            if (text != null)
                text.text = $"{money}";
        }
    }
    public void UpdatePlayerDataUI()
    {
        var playerData = DataManager.Instance.PlayerData;

        // Update book counts by category
        foreach (var entry in playerData.Books)
        {
            int count = entry.Have;

            switch (entry.Category)
            {
                case BookCategory.Crime:
                    foreach (var t in CrimeTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Drama:
                    foreach (var t in DramaTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Fact:
                    foreach (var t in FactTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Fantasy:
                    foreach (var t in FantasyTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Classic:
                    foreach (var t in ClassicTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Kids:
                    foreach (var t in KidsTexts) if (t != null) t.text = count.ToString();
                    break;
                case BookCategory.Travel:
                    foreach (var t in TravelTexts) if (t != null) t.text = count.ToString();
                    break;
            }
        }
    }
    public void UpdateShopTexts(BookCategory category, int newValue)
    {
        switch (category)
        {
            case BookCategory.Crime:
                foreach (var t in SCrimeTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Drama:
                foreach (var t in SDramaTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Fact:
                foreach (var t in SFactTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Fantasy:
                foreach (var t in SFantasyTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Classic:
                foreach (var t in SClassicTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Kids:
                foreach (var t in SKidsTexts) if (t != null) t.text = newValue.ToString();
                break;
            case BookCategory.Travel:
                foreach (var t in STravelTexts) if (t != null) t.text = newValue.ToString();
                break;
        }
    }
    public void SyncShopTexts()
    {
        SyncCategoryTexts(SCrimeTexts);
        SyncCategoryTexts(SDramaTexts);
        SyncCategoryTexts(SFactTexts);
        SyncCategoryTexts(SFantasyTexts);
        SyncCategoryTexts(SClassicTexts);
        SyncCategoryTexts(SKidsTexts);
        SyncCategoryTexts(STravelTexts);
    }

    private void SyncCategoryTexts(List<TextMeshProUGUI> texts)
    {
        if (texts == null || texts.Count == 0) return;

        int maxValue = 0;
        foreach (var t in texts)
        {
            if (t != null && int.TryParse(t.text, out int val))
            {
                if (val > maxValue) maxValue = val;
            }
        }

        // Update all texts in the list to the max value
        foreach (var t in texts)
        {
            if (t != null) t.text = maxValue.ToString();
        }
    }
    public void Paused()
    {
        if (Time.timeScale == 0f)
        {
            GameManager.Instance.ResumeGame();
        }
        else
        {
            GameManager.Instance.PauseGame();
        }
    }
    private IEnumerator ShowLoadingScreen()
    {
        if (LoadingScreen != null)
        {
            LoadingScreen.SetActive(true);
            LoadingCanvasGroup.alpha = 1f; // fully visible
        }

        // Wait 3 seconds
        yield return new WaitForSeconds(3f);

        // Fade out over 1 second
        float duration = 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            LoadingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }

        // Hide completely
        LoadingScreen.SetActive(false);
    }
    public void CleanUIShop()
    {
        BoxGetUI.SetActive(false);
        ItemGetUI.SetActive(false);
    }
    public void PlaySoundButton()
    {
        SoundManager.Instance.PlayButtonClick();
    }
}