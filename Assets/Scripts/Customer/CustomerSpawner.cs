using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefabs (assign multiple)")]
    public List<GameObject> customerPrefabs;

    public Transform spawnPoint;
    public Transform bookCartArea;
    public float spawnInterval = 20f;

    public List<TextMeshProUGUI> texts;
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button yesButton;
    public Button noButton;
    public List<Sprite> bookSprites;

    private Coroutine spawnRoutine;
    private Dictionary<BookCategory, Sprite> spriteMap;

    void Awake()
    {
        spriteMap = new Dictionary<BookCategory, Sprite>
        {
            { BookCategory.Crime,   bookSprites[0] },
            { BookCategory.Drama,   bookSprites[1] },
            { BookCategory.Fact,    bookSprites[2] },
            { BookCategory.Fantasy, bookSprites[3] },
            { BookCategory.Classic, bookSprites[4] },
            { BookCategory.Kids,    bookSprites[5] },
            { BookCategory.Travel,  bookSprites[6] }
        };
    }

    void Update()
    {
        if (GameManager.Instance.CurrentState == GameManager.GameState.Service)
        {
            if (spawnRoutine == null)
                spawnRoutine = StartCoroutine(SpawnLoop());
        }
        else
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }
    }

    IEnumerator SpawnLoop()
    {
        while (GameManager.Instance.CurrentState == GameManager.GameState.Service)
        {
            SpawnCustomer();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnCustomer()
    {
        if (customerPrefabs == null || customerPrefabs.Count == 0) return;

        GameObject prefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];
        GameObject npc = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        CustomerAI ai = npc.GetComponent<CustomerAI>();
        if (ai == null)
            ai = npc.AddComponent<CustomerAI>();

        ai.Initialize(spawnPoint, bookCartArea, texts, spriteMap, this);

        // ✅ Give each customer a unique offset near the cart
        Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
        UnityEngine.AI.NavMeshAgent agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.SetDestination(bookCartArea.position + offset);
        }
    }
}
