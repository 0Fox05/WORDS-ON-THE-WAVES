using UnityEngine;
using System.Collections.Generic;

public class FloatTextPool : MonoBehaviour
{
    public static FloatTextPool Instance;
    public GameObject floatTextPrefab;
    public int poolSize = 10;
    private List<GameObject> pool = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(floatTextPrefab, transform);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }

    public void SpawnText(string message, Transform canvasTransform)
    {
        foreach (GameObject obj in pool)
        {
            if (!obj.activeInHierarchy)
            {
                obj.GetComponent<FloatUpText>().Show(message, canvasTransform);
                return;
            }
        }

        // Expand pool if all are in use
        GameObject newObj = Instantiate(floatTextPrefab, transform);
        newObj.GetComponent<FloatUpText>().Show(message, canvasTransform);
        pool.Add(newObj);
    }
}
