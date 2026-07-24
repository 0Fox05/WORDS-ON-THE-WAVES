using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
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

    // ✅ Pool dictionary
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

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

        foreach (var prefab in customerPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning("CustomerSpawner: Found a null prefab in customerPrefabs list — skipping.");
                continue;
            }

            pools[prefab] = new Queue<GameObject>();

            for (int i = 0; i < 5; i++) // preload 5 NPCs each
            {
                // pick a point near your spawnPoint
                Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                Vector3 candidatePos = spawnPoint.position + randomOffset;

                // snap to nearest NavMesh position
                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidatePos, out hit, 5f, NavMesh.AllAreas))
                {
                    GameObject npc = Instantiate(prefab, hit.position, Quaternion.identity);
                    npc.SetActive(false);
                    pools[prefab].Enqueue(npc);
                }
                else
                {
                    Debug.LogWarning($"CustomerSpawner: Could not find NavMesh near {candidatePos}, skipping preload.");
                }
            }
        }
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
        GameObject npc = GetFromPool(prefab);

        npc.transform.position = spawnPoint.position;
        npc.transform.rotation = Quaternion.identity;
        npc.SetActive(true);

        CustomerAI ai = npc.GetComponent<CustomerAI>() ?? npc.AddComponent<CustomerAI>();
        ai.Initialize(spawnPoint, bookCartArea, texts, spriteMap, this, prefab);

        Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            // ✅ Only set destination if position is on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(npc.transform.position, out hit, 1.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position); // snap to valid NavMesh position
                agent.SetDestination(bookCartArea.position + offset);
            }
            else
            {
                // Optional: silently skip or log once in editor only
#if UNITY_EDITOR
                Debug.Log($"Skipped NavMeshAgent creation for {npc.name} (not near NavMesh)");
#endif
            }
        }
    }

    GameObject GetFromPool(GameObject prefab)
    {
        if (pools[prefab].Count > 0)
        {
            return pools[prefab].Dequeue();
        }
        else
        {
            GameObject npc = Instantiate(prefab);
            npc.SetActive(false);
            return npc;
        }
    }

    public void ReturnToPool(GameObject prefab, GameObject npc)
    {
        npc.SetActive(false);
        pools[prefab].Enqueue(npc);
    }
}
