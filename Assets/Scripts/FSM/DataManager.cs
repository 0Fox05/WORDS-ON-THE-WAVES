using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;
    public LocationConfig LocationData;
    public CrateConfig CrateData;
    public PlayerData PlayerData;
    public ShopConfig ShopData;
    public DialogueLData dialogueData;

    private float moneyBoostMultiplier = 1f; // default no boost

    private Dictionary<string, int> locationCameraMap = new Dictionary<string, int>()
    {
        { "Central Square", 0 },
        { "Morning Cafe", 1 },
        { "Grad Station", 2 },
        { "Far Horizons", 3 },
        { "Cart", 4 },
        { "Shelf", 5 },
    };

    public int GetCameraIndexForLocation(string locationName)
    {
        if (locationCameraMap.TryGetValue(locationName, out int index))
            return index;
        Debug.LogWarning("No camera mapped for location: " + locationName);
        return 0; // default to first camera
    }

    void Awake()
    {
        Instance = this;
        LoadLocations("JsonData/locations");
        LoadCrates("JsonData/crates");
        LoadPlayer("JsonData/PlayerData");
        LoadShop("JsonData/Items");
    }

    // --- LOCATION ---
    void LoadLocations(string resourcePath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            LocationData = JsonUtility.FromJson<LocationConfig>(jsonFile.text);
            Debug.Log("=== Loaded Locations ===");
            foreach (var loc in LocationData.Locations)
                Debug.Log($"Location: {loc.Name}, Fee: {loc.TravelFee}, Targets: {string.Join(", ", loc.TargetCustomers)}");
        }
        else Debug.LogError($"Locations file not found at Resources/{resourcePath}");
    }

    // --- CRATES ---
    void LoadCrates(string resourcePath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            CrateData = JsonUtility.FromJson<CrateConfig>(jsonFile.text);
            Debug.Log("=== Loaded Crates ===");
            foreach (var crate in CrateData.Crates)
            {
                Debug.Log($"Crate: {crate.Name}, Price: {crate.Price}, TotalBooks: {crate.TotalBooks}");
                foreach (var rate in crate.DropRates)
                    Debug.Log($"   {rate.Category}: {rate.Rate * 100}%");
            }
        }
        else Debug.LogError($"Crates file not found at Resources/{resourcePath}");
    }

    // --- PLAYER ---
    public void LoadPlayer(string fileName = "player.json")
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            PlayerData = JsonUtility.FromJson<PlayerData>(json);

            // normalize categories + merge duplicates
            var merged = new List<PlayerBookEntry>();
            foreach (BookCategory cat in Enum.GetValues(typeof(BookCategory)))
            {
                int total = 0;
                foreach (var b in PlayerData.Books)
                    if (b.Category == cat) total += b.Have;
                merged.Add(new PlayerBookEntry { Category = cat, Have = total });
            }
            PlayerData.Books = merged;

            Debug.Log("Player data loaded and normalized from: " + path);
        }
        else
        {
            Debug.LogWarning("No player data file found, using defaults.");
            PlayerData = new PlayerData();
        }
    }

    // --- MONEY ---
    public void ChangeMoney(int delta)
    {
        int boostedDelta;

        // Apply buff only if the game is in Service state
        if (GameManager.Instance.CurrentState == GameManager.GameState.Service)
        {
            boostedDelta = Mathf.RoundToInt(delta * moneyBoostMultiplier);
        }
        else
        {
            boostedDelta = delta;
        }

        PlayerData.Money += boostedDelta;
        if (PlayerData.Money < 0) PlayerData.Money = 0;

        SavePlayer();
        UIManager.Instance.UpdateMoneyUI(PlayerData.Money);
        UIManager.Instance.UpdatePlayerDataUI();

        Debug.Log($"Money changed: base {delta}, boosted {boostedDelta}, total {PlayerData.Money}");
    }

    public void ResetMoneyBoosts()
    {
        moneyBoostMultiplier = 1f;
    }

    public void ApplyMoneyBoost(float percent)
    {
        float multiplier = 1f + (percent / 100f);
        moneyBoostMultiplier *= multiplier; // ✅ stack boosts
        Debug.Log($"Money boost added: +{percent}% (total multiplier {moneyBoostMultiplier:F2})");
    }

    public int GetMoney() => PlayerData.Money;

    // --- SAVE ---
    public void SavePlayer(string fileName = "player.json")
    {
        NormalizeBookData();
        string json = JsonUtility.ToJson(PlayerData, true);
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, json);
        Debug.Log("Player data saved to: " + path);
    }

    public void ResetPlayer(string resourcePath = "JsonData/PlayerData", string fileName = "player.json")
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            PlayerData = JsonUtility.FromJson<PlayerData>(jsonFile.text);
            string json = JsonUtility.ToJson(PlayerData, true);
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, json);
            Debug.Log("Player data reset to defaults and saved at: " + path);
        }
        else Debug.LogError($"Default player file not found at Resources/{resourcePath}");
    }

    // --- BOOKS ---
    public void ChangeBookCount(BookCategory category, int delta)
    {
        var entry = PlayerData.Books.Find(b => b.Category == category);
        if (entry != null)
        {
            entry.Have += delta;
            if (entry.Have < 0) entry.Have = 0;
            SavePlayer();
        }
        else Debug.LogWarning("Category not found in PlayerData: " + category);

        UIManager.Instance.UpdateMoneyUI(PlayerData.Money);
        UIManager.Instance.UpdatePlayerDataUI();
    }

    private void NormalizeBookData()
    {
        var merged = new Dictionary<BookCategory, int>();
        foreach (var book in PlayerData.Books)
        {
            if (!merged.ContainsKey(book.Category)) merged[book.Category] = 0;
            merged[book.Category] += book.Have;
        }
        PlayerData.Books = new List<PlayerBookEntry>();
        foreach (var kvp in merged)
            PlayerData.Books.Add(new PlayerBookEntry { Category = kvp.Key, Have = kvp.Value });
    }

    // --- SHOP ---
    void LoadShop(string resourcePath = "JsonData/shop")
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            ShopData = JsonUtility.FromJson<ShopConfig>(jsonFile.text);
            Debug.Log("=== Loaded Shop Items ===");
            foreach (var item in ShopData.ItemsShop)
                Debug.Log($"{item.Name} costs {item.Money}");
        }
        else Debug.LogError($"Shop file not found at Resources/{resourcePath}");
    }

    public bool BuyItem(string itemName)
    {
        var shopItem = ShopData.ItemsShop.Find(i => i.Name == itemName);
        if (shopItem == null)
        {
            Debug.LogWarning("Item not found in shop: " + itemName);
            return false;
        }

        if (PlayerData.Money >= shopItem.Money)
        {
            ChangeMoney(-shopItem.Money);
            var cartItem = PlayerData.ItemsCart.Find(i => i.Name == itemName);
            if (cartItem != null) cartItem.Have++;
            else PlayerData.ItemsCart.Add(new PlayerCartEntry { Name = itemName, Have = 1 });

            SavePlayer();
            UIManager.Instance.UpdatePlayerDataUI();
            Debug.Log($"Bought {itemName} for {shopItem.Money}");
            return true;
        }
        else
        {
            Debug.Log("Not enough money to buy " + itemName);
            return false;
        }
    }
    // --- ACTIVE STATE ---
    public void ChangeActive(string itemName, bool active)
    {
        var entry = PlayerData.ItemsCart.Find(i => i.Name == itemName);
        if (entry != null)
        {
            // Only allow active if the player owns it
            entry.Active = (entry.Have > 0) && active;
            SavePlayer();
            Debug.Log($"Item {itemName} active state set to {entry.Active}");
        }
        else
        {
            Debug.LogWarning($"Item {itemName} not found in PlayerData.ItemsCart");
        }
    }

    // --- DIALOGUE ---
    public void LoadDialogue(string locationName)
    {
        string path = $"JsonData/dialogues_{locationName.ToLower().Replace(" ", "")}";
        TextAsset jsonFile = Resources.Load<TextAsset>(path);
        if (jsonFile != null)
        {
            dialogueData = JsonUtility.FromJson<DialogueLData>(jsonFile.text);
            Debug.Log($"Loaded dialogue for {locationName}");
        }
        else
        {
            dialogueData = null;
            Debug.LogWarning($"Dialogue file not found at Resources/{path}");
        }
    }
}
