using UnityEngine;

public class DontDestroyCode : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

}
