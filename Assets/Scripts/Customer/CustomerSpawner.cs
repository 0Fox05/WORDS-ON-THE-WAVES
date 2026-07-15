using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefabs (assign multiple)")]
    public List<GameObject> customerPrefabs;   // instead of single prefab

    public Transform spawnPoint;
    public Transform bookCartArea;
    public float spawnInterval = 20f;

    // Ordered list: Crime, Drama, Fact, Fantasy, Classic, Kids, Travel
    public List<TextMeshProUGUI> texts;

    // Popup UI references (assign in Inspector)
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button yesButton;
    public Button noButton;

    // Book selection UI references
    public GameObject bookSelectionPanel;
    public List<Button> bookButtons; 
    public List<Sprite> bookSprites; 

    private Coroutine spawnRoutine;

    // Global lock system
    private CustomerAI activeCustomer;

    public bool TryLock(CustomerAI ai)
    {
        if (activeCustomer == null)
        {
            activeCustomer = ai;
            return true;
        }
        return false;
    }

    public void ReleaseLock(CustomerAI ai)
    {
        if (activeCustomer == ai)
        {
            activeCustomer = null;
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

        // Pick random prefab from list
        GameObject prefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];

        GameObject npc = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        CustomerAI ai = npc.GetComponent<CustomerAI>();
        if (ai == null)
            ai = npc.AddComponent<CustomerAI>();

        ai.Initialize(spawnPoint, bookCartArea, texts,
              bookSelectionPanel, bookButtons, bookSprites, this);
    }
}
