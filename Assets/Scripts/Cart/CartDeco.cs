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
        foreach (var img in SpriteHave)
        {
            if (img != null) img.sprite = null;
        }

        int index = 0;
        foreach (var item in items)
        {
            if (GetPlayerItemCount(item.name) > 0)
            {
                if (index < SpriteHave.Count)
                {
                    SpriteHave[index].sprite = item.sprite;
                    index++;
                }
            }
        }
    }

    private void UpdateBoostedSpriteUI()
    {
        // Clear all boosted slots and texts first
        for (int i = 0; i < BoostedSpriteHave.Count; i++)
        {
            if (BoostedSpriteHave[i] != null)
                BoostedSpriteHave[i].sprite = null;

            if (i < texts.Count && texts[i] != null)
                texts[i].text = string.Empty;
        }

        // Show boosts and their percentages
        for (int i = 0; i < BoostedSprites.Count; i++)
        {
            BookCategory cat = (BookCategory)i;
            float multiplier = BookCalculate.Instance.GetMultiplier(cat);

            if (multiplier > 1f && i < BoostedSpriteHave.Count)
            {
                BoostedSpriteHave[i].sprite = BoostedSprites[i];

                if (i < texts.Count && texts[i] != null)
                {
                    float boostPercent = (multiplier - 1f) * 100f;
                    texts[i].text = $"+{boostPercent:F0}%";
                }
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

    private int GetPlayerItemCount(string itemName)
    {
        if (DataManager.Instance != null && DataManager.Instance.PlayerData != null)
        {
            var entry = DataManager.Instance.PlayerData.ItemsCart.Find(i => i.Name == itemName);
            if (entry != null) return entry.Have;
        }
        return 0;
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
        // ✅ Reset money boost multiplier before scanning
        DataManager.Instance.ResetMoneyBoosts();

        foreach (var item in items)
        {
            int haveCount = GetPlayerItemCount(item.name);

            if (haveCount <= 0)
            {
                item.isActive = false;
                ApplyShadow(item);
                // no boost
            }
            else if (!item.isActive)
            {
                ApplyShadow(item);
                // no boost
            }
            else
            {
                ApplyOriginal(item);

                // ✅ Apply book boosts directly
                foreach (var boost in item.bookBoosts)
                {
                    BookCalculate.Instance.BoostThis(boost.category, boost.percent);
                }

                // ✅ Apply money boost directly
                if (item.moneyBoostPercent > 0f)
                {
                    DataManager.Instance.ApplyMoneyBoost(item.moneyBoostPercent);
                }
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Service)
            InactivateGhostItems();
        else
            ReactivateGhostItems();

        UpdateSpriteHaveUI();
        UpdateBoostedSpriteUI();
    }

}
