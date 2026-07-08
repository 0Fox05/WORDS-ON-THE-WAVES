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

    // These still come from the spawner
    private GameObject bookSelectionPanel;
    private List<Button> bookButtons;
    private List<Sprite> bookSprites;

    [Header("Other References (set by spawner)")]
    public Transform spawnLocation;
    public Transform cartLocation;
    public List<TextMeshProUGUI> texts;
    public CustomerSpawner spawner;

    private NavMeshAgent agent;
    private bool goingToCart = true;
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

        // Hook up buttons (Inspector-assigned)
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

        // Dialogue setup
        string locationName = UIManager.Instance.ChosenLocation;
        DataManager.Instance.LoadDialogue(locationName);
        dialogueData = DataManager.Instance.dialogueData;

        if (dialogueData != null && dialogueData.Question.Count > 0)
        {
            foreach (var entry in dialogueData.Question)
            {
                if (Enum.TryParse(entry.CorrectBook, true, out BookCategory cat))
                {
                    var playerEntry = DataManager.Instance.PlayerData.Books.Find(b => b.Category == cat);
                    if (playerEntry != null && playerEntry.Have > 0)
                    {
                        correctCategory = cat;
                        currentDialogueLines = entry.lines;
                        break;
                    }
                }
            }
        }

        // Start moving to cart
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

        // These are still passed from spawner
        bookSelectionPanel = bookPanel;
        bookButtons = buttons;
        bookSprites = sprites;

        spawner = spawnerRef;
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                if (goingToCart)
                {
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

        // Decide behavior: 70% dialogue, 30% instant purchase
        float chance = UnityEngine.Random.value;
        bool canDoDialogue = (currentDialogueLines != null && currentDialogueLines.Count > 0);

        if (chance <= 0.7f && canDoDialogue)
        {
            // Dialogue path
            AskQuestion();
        }
        else
        {
            // Release lock right away since no dialogue
            spawner.ReleaseLock(this);

            // Instant purchase path
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

        var allCategories = System.Enum.GetValues(typeof(BookCategory)).Cast<BookCategory>().ToList();
        var randomOthers = allCategories.Where(c => c != correctCategory)
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
                bookButtons[i].onClick.AddListener(() => OnBookChosen(cat));
            }
            else
            {
                bookButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void OnBookChosen(BookCategory chosenCategory)
    {
        Debug.Log($"Player chose {chosenCategory}");

        DataManager.Instance.ChangeBookCount(chosenCategory, -1);

        int index = (int)chosenCategory;
        if (index < texts.Count && texts[index] != null)
        {
            if (int.TryParse(texts[index].text, out int value))
            {
                texts[index].text = Mathf.Max(0, value - 1).ToString();
            }
        }

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

        bookSelectionPanel.SetActive(false);
        questionPanel.SetActive(false);

        spawner.ReleaseLock(this);
        agent.isStopped = false;
        agent.SetDestination(spawnLocation.position);
        goingToCart = false;
        GameManager.Instance.ResetPoint();
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
        yield return new WaitForSeconds(10f);

        var available = DataManager.Instance.PlayerData.Books
            .Where(b => b.Have > 0)
            .Select(b => b.Category)
            .ToList();

        if (available.Count > 0)
        {
            BookCategory chosen = available[UnityEngine.Random.Range(0, available.Count)];
            OnBookChosen(chosen);
        }
        else
        {
            // No books available, just leave
            agent.isStopped = false;
            agent.SetDestination(spawnLocation.position);
            goingToCart = false;
        }
    }
}
