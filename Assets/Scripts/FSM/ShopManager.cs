using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

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

        // Give books based on drop rates
        for (int i = 0; i < crate.TotalBooks; i++)
        {
            string category = RollCategory(crate);
            if (System.Enum.TryParse(category, out BookCategory bookCategory))
            {
                // Ensure category exists in PlayerData before adding
                var entry = DataManager.Instance.PlayerData.Books.Find(b => b.Category == bookCategory);
                if (entry == null)
                {
                    entry = new PlayerBookEntry { Category = bookCategory, Have = 0 };
                    DataManager.Instance.PlayerData.Books.Add(entry);
                }

                DataManager.Instance.ChangeBookCount(bookCategory, 1);
            }
            else
            {
                Debug.LogWarning($"Unknown category rolled: {category}");
            }
        }

        // Show crate UI only after successful purchase
        UIManager.Instance.BoxGetUI.SetActive(true);
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

        // Fallback in case of rounding errors
        return crate.DropRates[crate.DropRates.Count - 1].Category;
    }
}
