using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using System;

public class CustomerAI : MonoBehaviour
{
    [Header("Question UI")]
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button yesButton;
    public Button noButton;

    [Header("Book Selection (assign in Inspector)")]
    public GameObject bookSelectionPanel;
    public List<Button> bookButtons;   // assign 3 buttons directly in Inspector
    private List<Sprite> bookSprites;  // comes from spawner

    [Header("Other References")]
    public Transform spawnLocation;
    public Transform cartLocation;
    public List<TextMeshProUGUI> texts;
    public CustomerSpawner spawner;

    private NavMeshAgent agent;
    private bool goingToCart = true;
    private bool isProcessing = false;
    private int questionsAsked = 0;

    private DialogueLData dialogueData;
    private BookCategory correctCategory;
    private List<string> currentDialogueLines;
    private Dictionary<BookCategory, Sprite> spriteMap;
    private DialogueEntry currentEntry;

    private bool useDialogueRoute;
    private GameObject originPrefab; // ✅ prefab gốc để trả về pool

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (questionPanel != null) questionPanel.SetActive(false);
        if (bookSelectionPanel != null) bookSelectionPanel.SetActive(false);

        if (yesButton != null)
        {
            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() => OnAnswer(true));
        }
        if (noButton != null)
        {
            noButton.onClick.RemoveAllListeners();
            noButton.onClick.AddListener(() => OnAnswer(false));
        }

        string locationName = UIManager.Instance.ChosenLocation;
        DataManager.Instance.LoadDialogue(locationName);
        dialogueData = DataManager.Instance.dialogueData;

        if (agent != null && cartLocation != null)
        {
            agent.SetDestination(cartLocation.position);
            goingToCart = true;
        }
    }

    public void Initialize(Transform spawn, Transform cart, List<TextMeshProUGUI> textList,
                           Dictionary<BookCategory, Sprite> spriteDictionary, CustomerSpawner spawnerRef,
                           GameObject prefabRef)
    {
        spawnLocation = spawn;
        cartLocation = cart;
        texts = textList;
        spawner = spawnerRef;
        spriteMap = spriteDictionary;
        originPrefab = prefabRef; // ✅ lưu prefab gốc

        float chance = UnityEngine.Random.value;
        useDialogueRoute = (chance <= 0.7f);

        // reset trạng thái mỗi lần tái sử dụng
        isProcessing = false;
        goingToCart = true;
        questionsAsked = 0;
        currentEntry = null;

        if (agent != null && cartLocation != null)
        {
            agent.isStopped = false;
            agent.SetDestination(cartLocation.position);
        }
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (!isProcessing && !agent.pathPending && agent.remainingDistance <= 0.05f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                if (goingToCart)
                {
                    isProcessing = true;
                    if (useDialogueRoute)
                        StartCoroutine(WaitForTurn());
                    else
                        StartCoroutine(InstantPurchaseRoutine());
                }
                else
                {
                    // ❌ Destroy(gameObject);
                    // ✅ trả về pool
                    ResetAndReturnToPool();
                }
            }
        }
    }

    IEnumerator WaitForTurn()
    {
        AskQuestion();
        yield break;
    }

    void AskQuestion()
    {
        if (currentEntry == null)
        {
            var validEntries = dialogueData.Question.Where(entry =>
            {
                if (Enum.TryParse(entry.CorrectBook, true, out BookCategory cat))
                {
                    int idx = (int)cat;
                    return idx < texts.Count &&
                           int.TryParse(texts[idx].text, out int val) &&
                           val > 0;
                }
                return false;
            }).ToList();

            if (validEntries.Count == 0)
            {
                StartCoroutine(InstantPurchaseRoutine());
                return;
            }

            currentEntry = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
            Enum.TryParse(currentEntry.CorrectBook, true, out correctCategory);
            currentDialogueLines = currentEntry.lines;
        }

        questionPanel.SetActive(true);
        questionText.text = currentDialogueLines[questionsAsked];
        agent.isStopped = true;
    }

    public void OnAnswer(bool playerChoiceCorrect)
    {
        Debug.Log($"+{correctCategory}%");
        questionsAsked++;
        questionPanel.SetActive(false);

        if (questionsAsked < currentDialogueLines.Count)
            AskQuestion();
        else
            ShowBookSelection();
    }

    void ShowBookSelection()
    {
        if (bookSelectionPanel == null) return;

        bookSelectionPanel.SetActive(true);

        List<BookCategory> choices = new List<BookCategory> { correctCategory };
        var allCategories = Enum.GetValues(typeof(BookCategory)).Cast<BookCategory>().ToList();
        var availableCategories = allCategories.Where(c =>
        {
            int idx = (int)c;
            return idx < texts.Count && int.TryParse(texts[idx].text, out int val) && val > 0;
        }).ToList();

        choices.AddRange(availableCategories.Where(c => c != correctCategory)
                                            .OrderBy(x => UnityEngine.Random.value)
                                            .Take(2));

        for (int i = 0; i < bookButtons.Count; i++)
        {
            if (i < choices.Count)
            {
                BookCategory cat = choices[i];

                bookButtons[i].gameObject.SetActive(true);
                bookButtons[i].image.sprite = spriteMap[cat];
                bookButtons[i].onClick.RemoveAllListeners();
                bookButtons[i].onClick.AddListener(() => OnBookChosenDialogue(cat));
            }
            else
            {
                bookButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void OnBookChosenDialogue(BookCategory chosenCategory)
    {
        HandleBookPurchase(chosenCategory);

        if (chosenCategory == correctCategory)
        {
            DataManager.Instance.ChangeMoney(10);
            GameManager.Instance.IncreasePoint();
        }
        else
        {
            DataManager.Instance.ChangeMoney(5);
            GameManager.Instance.ResetPoint();
        }

        BookCategory extra = BookCalculate.Instance.GetWeightedRandomCategory();
        int idx = (int)extra;
        if (idx < texts.Count && int.TryParse(texts[idx].text, out int val) && val > 0)
        {
            HandleBookPurchase(extra);
            DataManager.Instance.ChangeMoney(5);
        }

        BookCalculate.Instance.UpdateUI();
        bookSelectionPanel.SetActive(false);
        questionPanel.SetActive(false);

        foreach (var btn in bookButtons)
        {
            btn.onClick.RemoveAllListeners();
            btn.gameObject.SetActive(false);
        }

        agent.isStopped = false;
        agent.SetDestination(spawnLocation.position);
        goingToCart = false;
        isProcessing = false;
        currentEntry = null;
    }

    void OnBookChosenInstant(BookCategory chosenCategory)
    {
        HandleBookPurchase(chosenCategory);
        DataManager.Instance.ChangeMoney(5);
        BookCalculate.Instance.UpdateUI();
    }

    void HandleBookPurchase(BookCategory chosenCategory)
    {
        int index = (int)chosenCategory;
        if (index < texts.Count && int.TryParse(texts[index].text, out int value))
        {
            texts[index].text = Mathf.Max(0, value - 1).ToString();
        }
        DataManager.Instance.ChangeBookCount(chosenCategory, -1);
    }

    IEnumerator InstantPurchaseRoutine()
    {
        yield return new WaitForSeconds(5f);

        int booksToBuy = UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < booksToBuy; i++)
        {
            BookCategory chosen = BookCalculate.Instance.GetWeightedRandomCategory();
            int index = (int)chosen;
            if (index < texts.Count && int.TryParse(texts[index].text, out int value) && value > 0)
            {
                OnBookChosenInstant(chosen);
            }
            else break;

            yield return new WaitForSeconds(0.5f);
        }

        agent.isStopped = false;
        agent.SetDestination(spawnLocation.position);
        goingToCart = false;
        isProcessing = false;
    }

    void ResetAndReturnToPool()
    {
        if (questionPanel != null) questionPanel.SetActive(false);
        if (bookSelectionPanel != null) bookSelectionPanel.SetActive(false);

        foreach (var btn in bookButtons)
        {
            btn.onClick.RemoveAllListeners();
            btn.gameObject.SetActive(false);
        }

        isProcessing = false;
        goingToCart = true;
        currentEntry = null;
        questionsAsked = 0;

        if (agent != null) agent.isStopped = true;

        spawner.ReturnToPool(originPrefab, this.gameObject); // ✅ trả về pool
    }
}
