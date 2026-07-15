using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class BookCalculate : MonoBehaviour
{
    public static BookCalculate Instance;   // Singleton reference

    [Header("Assign in order: Crime, Drama, Fact, Fantasy, Classic, Kids, Travel")]
    public List<TextMeshProUGUI> BooksText;

    [Header("Assign in same order for percentages")]
    public List<TextMeshProUGUI> Percentages;

    [Header("Hover targets (images/buttons)")]
    public List<GameObject> HoverTargets;

    [Header("Sale chance panels to show")]
    public List<GameObject> SaleChancePanels;

    // ✅ Boost multipliers (percentage additions)
    public Dictionary<BookCategory, float> boosts = new Dictionary<BookCategory, float>();

    private void Awake()
    {
        Instance = this;

        foreach (var panel in SaleChancePanels)
        {
            if (panel != null) panel.SetActive(false);
        }

        // Initialize boosts to 1 (no boost)
        foreach (BookCategory cat in System.Enum.GetValues(typeof(BookCategory)))
        {
            boosts[cat] = 1f;
        }

        // Hook up hover events
        for (int i = 0; i < HoverTargets.Count; i++)
        {
            int index = i;
            var trigger = HoverTargets[i].AddComponent<EventTrigger>();

            // Pointer Enter
            EventTrigger.Entry enter = new EventTrigger.Entry();
            enter.eventID = EventTriggerType.PointerEnter;
            enter.callback.AddListener((data) => OnHoverEnter(index));
            trigger.triggers.Add(enter);

            // Pointer Exit
            EventTrigger.Entry exit = new EventTrigger.Entry();
            exit.eventID = EventTriggerType.PointerExit;
            exit.callback.AddListener((data) => OnHoverExit(index));
            trigger.triggers.Add(exit);
        }
    }

    // ✅ Boost methods
    public void BoostThis(BookCategory category, float percent)
    {
        // percent = 15 means +15% boost
        boosts[category] = 1f + (percent / 100f);
        UpdateUI();
    }

    public void UnBoostThis(BookCategory category)
    {
        boosts[category] = 1f;
        UpdateUI();
    }

    public float GetMultiplier(BookCategory category)
    {
        if (boosts.ContainsKey(category))
            return boosts[category];
        return 1f;
    }

    // ✅ Weighted random with boost
    public BookCategory GetWeightedRandomCategory()
    {
        List<int> values = new List<int>();
        int total = 0;

        for (int i = 0; i < BooksText.Count; i++)
        {
            if (BooksText[i] != null && int.TryParse(BooksText[i].text, out int number))
            {
                // ✅ Apply boost only if stock > 0
                float boosted = number > 0 ? number * GetMultiplier((BookCategory)i) : 0;
                values.Add((int)boosted);
                total += (int)boosted;
            }
            else
            {
                values.Add(0);
            }
        }

        if (total <= 0) return BookCategory.Crime;

        int roll = UnityEngine.Random.Range(0, total);
        int cumulative = 0;

        for (int i = 0; i < values.Count; i++)
        {
            cumulative += values[i];
            if (roll < cumulative)
                return (BookCategory)i;
        }

        return BookCategory.Crime;
    }

    // ✅ Update percentages with boost
    public void UpdateUI()
    {
        List<int> values = new List<int>();
        int total = 0;

        for (int i = 0; i < BooksText.Count; i++)
        {
            if (BooksText[i] != null && int.TryParse(BooksText[i].text, out int number))
            {
                float boosted = number > 0 ? number * GetMultiplier((BookCategory)i) : 0;
                values.Add((int)boosted);
                total += (int)boosted;
            }
            else
            {
                values.Add(0);
            }
        }

        for (int i = 0; i < values.Count; i++)
        {
            float percent = 0f;
            if (total > 0 && values[i] > 0)
                percent = (values[i] / (float)total) * 100f;

            if (i < Percentages.Count && Percentages[i] != null)
                Percentages[i].text = $"{percent:F1}%";
        }
    }

    // ✅ Hover logic
    private void OnHoverEnter(int index)
    {
        if (index < SaleChancePanels.Count && SaleChancePanels[index] != null)
        {
            SaleChancePanels[index].SetActive(true);
        }
    }

    private void OnHoverExit(int index)
    {
        if (index < SaleChancePanels.Count && SaleChancePanels[index] != null)
        {
            SaleChancePanels[index].SetActive(false);
        }
    }
}
