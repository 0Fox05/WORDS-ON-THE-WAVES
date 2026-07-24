using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class CartDeco : MonoBehaviour
{
    public static CartDeco Instance;

    [System.Serializable]
    public class BookBoostEntry
    {
        public BookCategory category;
        public float percent; // e.g. 15 means +15% chance
    }

    [System.Serializable]
    public class DecoItem
    {
        public string name;
        public List<Renderer> renderers; // multiple meshes supported
        [HideInInspector] public List<Material[]> originalMaterials = new List<Material[]>();
        public bool isActive = true;
        public Sprite sprite; // assign sprite in Inspector

        [Header("Boost Settings")]
        public List<BookBoostEntry> bookBoosts = new List<BookBoostEntry>();
        public float moneyBoostPercent = 0f; // e.g. 15 means +15% money
    }

    public Button ConfirmButton;
    public List<DecoItem> items;
    public Material shadowMaterial;

    [Header("UI References")]
    public List<Image> SpriteHave;        // assign Image slots in Inspector
    public List<Sprite> BoostedSprites;   // assign in order: Crime, Drama, Fact, Fantasy, Classic, Kids, Travel
    public List<Image> BoostedSpriteHave; // assign Image slots in Inspector, same order
    public List<TextMeshProUGUI> texts;   // assign in same order as BoostedSpriteHave

    [Header("Default Boost Sprites")]
    public List<Sprite> DefaultBoostedSprites; // assign in Inspector, same order as BoostedSprites


    public void Awake()
    {
        Instance = this;
        ConfirmButton.onClick.AddListener(() => GameManager.Instance.ChangeState(GameManager.GameState.Menu));
    }

    private void Start()
    {
        foreach (var item in items)
        {
            item.originalMaterials.Clear();
            foreach (var rend in item.renderers)
            {
                if (rend != null)
                {
                    item.originalMaterials.Add(rend.materials);
                }
            }
        }
        UpdateSpriteHaveUI();
        UpdateBoostedSpriteUI();
        RefreshItems();
    }

    public void UpdateItems()
    {
        foreach (var item in items)
        {
            int haveCount = GetPlayerItemCount(item.name);

            if (haveCount <= 0)
            {
                item.isActive = false;
                ApplyShadow(item);
                RemoveBoosts(item); // ghosted → no boost
            }
            else if (!item.isActive)
            {
                ApplyShadow(item);
                RemoveBoosts(item); // inactive → no boost
            }
            else
            {
                ApplyOriginal(item);
                ApplyBoosts(item); // active and not ghosted → boost applies
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Service)
            InactivateGhostItems();
        else
            ReactivateGhostItems();

        UpdateSpriteHaveUI();
        UpdateBoostedSpriteUI();
    }

    private void UpdateSpriteHaveUI()
    {
        // Do NOT clear slots anymore — keep whatever sprite was set in the Inspector

        int index = 0;
        foreach (var item in items)
        {
            if (GetPlayerItemCount(item.name) > 0)
            {
                if (index < SpriteHave.Count)
                {
                    // Overwrite only when player owns the item
                    SpriteHave[index].sprite = item.sprite;
                    index++;
                }
            }
            // If not owned → leave the slot’s original sprite untouched
        }
    }

    private void UpdateBoostedSpriteUI()
    {
        // Clear texts but keep sprites
        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] != null)
                texts[i].text = string.Empty;
        }

        // Calculate total boost per category
        Dictionary<BookCategory, float> totalBoosts = new Dictionary<BookCategory, float>();
        foreach (var item in items)
        {
            if (item.isActive && GetPlayerItemCount(item.name) > 0)
            {
                foreach (var boost in item.bookBoosts)
                {
                    if (!totalBoosts.ContainsKey(boost.category))
                        totalBoosts[boost.category] = 0f;
                    totalBoosts[boost.category] += boost.percent;
                }
            }
        }

        // Display boosts or restore defaults
        for (int i = 0; i < BoostedSprites.Count; i++)
        {
            BookCategory cat = (BookCategory)i;
            if (totalBoosts.TryGetValue(cat, out float totalPercent) && totalPercent > 0f)
            {
                // Show boosted sprite + text
                if (BoostedSpriteHave[i] != null)
                    BoostedSpriteHave[i].sprite = BoostedSprites[i];

                if (i < texts.Count && texts[i] != null)
                    texts[i].text = $"+{totalPercent:F0}%";
            }
            else
            {
                // No boost → restore default sprite
                if (BoostedSpriteHave[i] != null && i < DefaultBoostedSprites.Count)
                    BoostedSpriteHave[i].sprite = DefaultBoostedSprites[i];
            }
        }
    }


    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                foreach (var item in items)
                {
                    foreach (var rend in item.renderers)
                    {
                        if (rend != null && hit.collider.gameObject == rend.gameObject)
                        {
                            if (GetPlayerItemCount(item.name) > 0)
                                ToggleItem(item);
                            else
                                Debug.Log($"{item.name} not owned, stays shadow.");
                            return;
                        }
                    }
                }
            }
        }
    }

    public void ToggleItem(DecoItem item)
    {
        item.isActive = !item.isActive;

        // Use DataManager to persist the change
        DataManager.Instance.ChangeActive(item.name, item.isActive);

        if (item.isActive)
        {
            ApplyOriginal(item);
            ApplyBoosts(item);
        }
        else
        {
            ApplyShadow(item);
            RemoveBoosts(item);
        }

        UpdateSpriteHaveUI();
        UpdateBoostedSpriteUI();
    }

    private int GetPlayerItemCount(string itemName)
    {
        if (DataManager.Instance == null || DataManager.Instance.PlayerData == null)
            return 0; // skip if DataManager or PlayerData not ready

        var entry = DataManager.Instance.PlayerData.ItemsCart
            ?.Find(i => i.Name == itemName);

        if (entry != null)
        {
            // If Have = 0, force Active = false
            if (entry.Have <= 0) entry.Active = false;
            return entry.Have;
        }

        // No entry found → treat as 0
        return 0;
    }


    private void ApplyOriginal(DecoItem item)
    {
        for (int i = 0; i < item.renderers.Count; i++)
        {
            if (item.renderers[i] != null && i < item.originalMaterials.Count)
                item.renderers[i].materials = item.originalMaterials[i];
        }
    }

    private void ApplyShadow(DecoItem item)
    {
        for (int i = 0; i < item.renderers.Count; i++)
        {
            if (item.renderers[i] != null && i < item.originalMaterials.Count)
            {
                Material[] shadowArray = new Material[item.originalMaterials[i].Length];
                for (int j = 0; j < shadowArray.Length; j++)
                    shadowArray[j] = shadowMaterial;
                item.renderers[i].materials = shadowArray;
            }
        }
    }

    private void InactivateGhostItems()
    {
        foreach (var item in items)
        {
            if (!item.isActive)
            {
                foreach (var rend in item.renderers)
                {
                    if (rend != null) rend.gameObject.SetActive(false);
                }
                RemoveBoosts(item);
                Debug.Log($"{item.name} disabled in Service state (ghost material).");
            }
        }
        UpdateBoostedSpriteUI();
    }

    private void ReactivateGhostItems()
    {
        foreach (var item in items)
        {
            foreach (var rend in item.renderers)
            {
                if (rend != null && !rend.gameObject.activeSelf)
                {
                    rend.gameObject.SetActive(true);
                }
            }
            if (item.isActive) ApplyBoosts(item);
            Debug.Log($"{item.name} re-enabled after leaving Service state.");
        }
        UpdateBoostedSpriteUI();
    }

    private void ApplyBoosts(DecoItem item)
    {
        foreach (var boost in item.bookBoosts)
        {
            BookCalculate.Instance.BoostThis(boost.category, boost.percent);
            Debug.Log($"{item.name} applied book boost {boost.category} +{boost.percent}%");
        }

        if (item.moneyBoostPercent > 0f)
        {
            DataManager.Instance.ApplyMoneyBoost(item.moneyBoostPercent);
            Debug.Log($"{item.name} applied money boost +{item.moneyBoostPercent}%");
        }

        UpdateBoostedSpriteUI();
    }

    private void RemoveBoosts(DecoItem item)
    {
        foreach (var boost in item.bookBoosts)
        {
            BookCalculate.Instance.UnBoostThis(boost.category);
            Debug.Log($"{item.name} removed book boost {boost.category}");
        }

        if (item.moneyBoostPercent > 0f)
        {

        }

        UpdateBoostedSpriteUI();
    }
    public void RefreshItems()
    {
        if (DataManager.Instance == null || DataManager.Instance.PlayerData == null)
        {
            Debug.LogWarning("DataManager or PlayerData not ready yet — skipping RefreshItems.");
            return;
        }

        DataManager.Instance.ResetMoneyBoosts();

        foreach (var item in items)
        {
            var entry = DataManager.Instance.PlayerData.ItemsCart
                ?.Find(i => i.Name == item.name);

            int haveCount = entry != null ? entry.Have : 0;
            bool isActive = entry != null && entry.Active;

            item.isActive = isActive;

            if (haveCount <= 0)
            {
                item.isActive = false;
                ApplyShadow(item);
            }
            else if (!item.isActive)
            {
                ApplyShadow(item);
            }
            else
            {
                ApplyOriginal(item);

                foreach (var boost in item.bookBoosts)
                    BookCalculate.Instance.BoostThis(boost.category, boost.percent);

                if (item.moneyBoostPercent > 0f)
                    DataManager.Instance.ApplyMoneyBoost(item.moneyBoostPercent);
            }
        }

        UpdateSpriteHaveUI();
        UpdateBoostedSpriteUI();
    }
}
