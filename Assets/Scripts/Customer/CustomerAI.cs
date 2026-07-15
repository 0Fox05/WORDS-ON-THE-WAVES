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
    [Header("Question UI (assign in Inspector)")]
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button yesButton;
    public Button noButton;

    private GameObject bookSelectionPanel;
    private List<Button> bookButtons;
    private List<Sprite> bookSprites;

    [Header("Other References (set by spawner)")]
    public Transform spawnLocation;
    public Transform cartLocation;
    public List<TextMeshProUGUI> texts; // Crime, Drama, Fact, Fantasy, Classic, Kids, Travel
    public CustomerSpawner spawner;

    private NavMeshAgent agent;
    private bool goingToCart = true;
    private bool isProcessing = false; // ✅ prevents double routines
    private int questionsAsked = 0;

    private DialogueLData dialogueData;
    private BookCategory correctCategory;
    private List<string> currentDialogueLines;

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
                           GameObject bookPanel, List<Button> buttons, List<Sprite> sprites,
                           CustomerSpawner spawnerRef)
    {
        spawnLocation = spawn;
        cartLocation = cart;
        texts = textList;

        bookSelectionPanel = bookPanel;
        bookButtons = buttons;
        bookSprites = sprites;

        spawner = spawnerRef;
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // ✅ Tightened arrival check
        if (!isProcessing && !agent.pathPending && agent.remainingDistance <= 0.05f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                if (goingToCart)
                {
                    isProcessing = true;
                    StartCoroutine(WaitForTurn());
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    IEnumerator WaitForTurn()
    {
        float waitTime = 120f;
        float elapsed = 0f;

        while (!spawner.TryLock(this))
        {
            elapsed += Time.deltaTime;
            if (elapsed >= waitTime)
            {
                agent.SetDestination(spawnLocation.position);
                goingToCart = false;
                yield break;
            }
            yield return null;
        }

        float chance = UnityEngine.Random.value;

        if (chance <= 0.7f && dialogueData != null && dialogueData.Question.Count > 0)
        {
            var shuffled = dialogueData.Question.OrderBy(x => UnityEngine.Random.value).ToList();
            foreach (var entry in shuffled)
            {
                if (Enum.TryParse(entry.CorrectBook, true, out BookCategory cat))
                {
                    int index = (int)cat;
                    if (index < texts.Count && int.TryParse(texts[index].text, out int value))
                    {
                        if (value > 0)
                        {
                            correctCategory = cat;
                            currentDialogueLines = entry.lines;
                            AskQuestion();
                            yield break;
                        }
                        else
                        {
                            spawner.ReleaseLock(this);
                            StartCoroutine(InstantPurchaseRoutine());
                            yield break;
                        }
                    }
                }
            }

            spawner.ReleaseLock(this);
            StartCoroutine(InstantPurchaseRoutine());
        }
        else
        {
            spawner.ReleaseLock(this);
            StartCoroutine(InstantPurchaseRoutine());
        }
    }

    void AskQuestion()
    {
        if (questionPanel != null && currentDialogueLines != null && questionsAsked < currentDialogueLines.Count)
        {
            questionPanel.SetActive(true);
            questionText.text = currentDialogueLines[questionsAsked];
            agent.isStopped = true;
        }
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

        var randomOthers = availableCategories.Where(c => c != correctCategory)
                                              .OrderBy(x => UnityEngine.Random.value)
                                              .Take(2);
        choices.AddRange(randomOthers);

        for (int i = 0; i < bookButtons.Count; i++)
        {
            if (i < choices.Count)
            {
                BookCategory cat = choices[i];
                int index = (int)cat;

                bookButtons[i].gameObject.SetActive(true);
                bookButtons[i].image.sprite = bookSprites[index];

                bookButtons[i].onClick.RemoveAllListeners();
                bookButtons[i].onClick.AddListener(() => OnBookChosenDialogue(cat));
            }
            else
            {
                bookButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // Dialogue purchase flow: buy correct book + 1 more
    void OnBookChosenDialogue(BookCategory chosenCategory)
    {
        HandleBookPurchase(chosenCategory);

        if (chosenCategory == correctCategory)
        {
            DataManager.Instance.ChangeMoney(10);
            Debug.Log("Correct book! +10 money");
        }
        else
        {
            DataManager.Instance.ChangeMoney(5);
            Debug.Log("Wrong book, normal sale +5 money");
        }

        // Buy one more book based on weighted probability
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

        spawner.ReleaseLock(this);
        agent.isStopped = false;
        agent.SetDestination(spawnLocation.position);
        goingToCart = false;
        isProcessing = false; // reset
        GameManager.Instance.ResetPoint();
    }

    // Instant purchase flow: buy 1 or 2 books
    void OnBookChosenInstant(BookCategory chosenCategory)
    {
        HandleBookPurchase(chosenCategory);
        DataManager.Instance.ChangeMoney(5);
        BookCalculate.Instance.UpdateUI();
    }

    void HandleBookPurchase(BookCategory chosenCategory)
    {
        Debug.Log($"Player chose {chosenCategory}");

        int index = (int)chosenCategory;
        if (index < texts.Count && int.TryParse(texts[index].text, out int value))
        {
            texts[index].text = Mathf.Max(0, value - 1).ToString();
        }

        DataManager.Instance.ChangeBookCount(chosenCategory, -1);
    }

    public void OnAnswer(bool playerChoiceCorrect)
    {
        if (playerChoiceCorrect)
        {
            GameManager.Instance.IncreasePoint();
        }

        questionsAsked++;
        questionPanel.SetActive(false);

        if (questionsAsked < currentDialogueLines.Count)
        {
            AskQuestion();
        }
        else
        {
            ShowBookSelection();
        }
    }

    IEnumerator InstantPurchaseRoutine()
    {
        yield return new WaitForSeconds(5f);

        int booksToBuy = UnityEngine.Random.Range(1, 3); // 1 or 2

        for (int i = 0; i < booksToBuy; i++)
        {
            BookCategory chosen = BookCalculate.Instance.GetWeightedRandomCategory();

            int index = (int)chosen;
            if (index < texts.Count && int.TryParse(texts[index].text, out int value) && value > 0)
            {
                OnBookChosenInstant(chosen);
            }
            else
            {
                break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        agent.isStopped = false;
        agent.SetDestination(spawnLocation.position);
        goingToCart = false;
        isProcessing = false; // reset
    }
}
