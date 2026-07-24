using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("UI References")]
    public List<TextMeshProUGUI> books; // Assign in Inspector in the order: Crime, Drama, Fact, Fantasy, Classic, Kids, Travel

    private void Awake()
    {
        Instance = this;
    }

    public void BuyCrate(int crateIndex)
    {
        var crates = DataManager.Instance.CrateData.Crates;
        if (crateIndex < 0 || crateIndex >= crates.Count)
        {
            Debug.LogError("Invalid crate index!");
            return;
        }

        var crate = crates[crateIndex];

        // Check money
        if (DataManager.Instance.GetMoney() < crate.Price)
        {
            Debug.Log("Not enough money to buy " + crate.Name);
            return;
        }

        // Deduct money
        DataManager.Instance.ChangeMoney(-(int)crate.Price);
        Debug.Log($"Bought crate: {crate.Name} for {crate.Price}");

        // Track results per category
        Dictionary<BookCategory, int> results = new Dictionary<BookCategory, int>();

        // Roll books
        for (int i = 0; i < crate.TotalBooks; i++)
        {
            string category = RollCategory(crate);
            if (System.Enum.TryParse(category, out BookCategory bookCategory))
            {
                var entry = DataManager.Instance.PlayerData.Books.Find(b => b.Category == bookCategory);
                if (entry == null)
                {
                    entry = new PlayerBookEntry { Category = bookCategory, Have = 0 };
                    DataManager.Instance.PlayerData.Books.Add(entry);
                }

                DataManager.Instance.ChangeBookCount(bookCategory, 1);

                if (!results.ContainsKey(bookCategory))
                    results[bookCategory] = 0;
                results[bookCategory]++;
            }
            else
            {
                Debug.LogWarning($"Unknown category rolled: {category}");
            }
        }

        // Show crate UI
        UIManager.Instance.BoxGetUI.SetActive(true);

        // Clear all slots
        foreach (var t in books)
            t.text = string.Empty;

        // Update UI in the order you assigned in Inspector
        SetCategoryText(BookCategory.Crime, results, 0);
        SetCategoryText(BookCategory.Drama, results, 1);
        SetCategoryText(BookCategory.Fact, results, 2);
        SetCategoryText(BookCategory.Fantasy, results, 3);
        SetCategoryText(BookCategory.Classic, results, 4);
        SetCategoryText(BookCategory.Kids, results, 5);
        SetCategoryText(BookCategory.Travel, results, 6);
    }

    private void SetCategoryText(BookCategory category, Dictionary<BookCategory, int> results, int index)
    {
        if (index >= books.Count) return;

        if (results.TryGetValue(category, out int count))
        {
            books[index].text = $"{count}";
        }
        else
        {
            books[index].text = $"0";
        }
    }
    private string RollCategory(Crate crate)
    {
        float roll = Random.value;
        float cumulative = 0f;

        foreach (var rate in crate.DropRates)
        {
            cumulative += rate.Rate;
            if (roll <= cumulative)
                return rate.Category;
        }

        return crate.DropRates[crate.DropRates.Count - 1].Category;
    }
    public void StupidPlayer()
    {
        // Condition 1: money <= 20
        if (DataManager.Instance.GetMoney() <= 20)
        {
            bool allZero = true;

            // Condition 2: check if all categories have 0
            foreach (BookCategory category in System.Enum.GetValues(typeof(BookCategory)))
            {
                var entry = DataManager.Instance.PlayerData.Books.Find(b => b.Category == category);
                if (entry != null && entry.Have > 0)
                {
                    allZero = false;
                    break;
                }
            }

            // If both conditions are true
            if (allZero)
            {
                foreach (BookCategory category in System.Enum.GetValues(typeof(BookCategory)))
                {
                    var entry = DataManager.Instance.PlayerData.Books.Find(b => b.Category == category);
                    if (entry == null)
                    {
                        entry = new PlayerBookEntry { Category = category, Have = 0 };
                        DataManager.Instance.PlayerData.Books.Add(entry);
                    }

                    DataManager.Instance.ChangeBookCount(category, 1);
                }

                DataManager.Instance.ChangeMoney(7);

                Debug.Log("StupidPlayer triggered: +1 book in each category and +7 money");
            }
        }
    }

}
