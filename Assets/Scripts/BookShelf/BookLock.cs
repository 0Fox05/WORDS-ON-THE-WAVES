using UnityEngine;

public class BookLock : MonoBehaviour
{
    public bool IsProcessing { get; private set; } = false;

    public void Lock() => IsProcessing = true;
    public void Unlock() => IsProcessing = false;
}
