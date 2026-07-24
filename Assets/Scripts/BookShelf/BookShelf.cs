using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class BookShelf : MonoBehaviour
{
    public Button[] buttons;
    public List<TextMeshProUGUI> texts;
    public List<TextMeshProUGUI> inShoptexts;

    // Each category has its own prefab list
    [System.Serializable]
    public class CategoryPrefabs
    {
        public List<GameObject> prefabs;
    }
    public List<CategoryPrefabs> categoryPrefabs;

    private int selectedCategoryIndex = -1;
    private enum HoldMode { None, Place, Remove }
    private HoldMode currentHoldMode = HoldMode.None;

    void Start()
    {
        if (buttons.Length != texts.Count)
        {
            Debug.LogError("Mismatch! Buttons and texts must have the same length.");
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => OnCategorySelected(index));
        }

        selectedCategoryIndex = 0; // Default to Crime
        Debug.Log("Default category set to Crime.");
    }

    void OnCategorySelected(int index)
    {
        selectedCategoryIndex = index;
        Debug.Log($"Selected category index: {index}");
    }

    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            currentHoldMode = HoldMode.None;
        }
        if (Input.GetMouseButtonDown(0))
        {
            HandleShelfInteraction(true);
        }
        else if (Input.GetMouseButton(0))
        {
            HandleShelfInteraction(false);
        }
    }

    private void HandleShelfInteraction(bool decideMode)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.collider.CompareTag("Shelf"))
            {
                Transform shelf = hit.collider.transform;

                GameObject existingBook = null;
                foreach (Transform child in shelf)
                {
                    if (child.CompareTag("Book"))
                    {
                        existingBook = child.gameObject;
                        break;
                    }
                }

                if (existingBook != null)
                {
                    BookLock lockComp = existingBook.GetComponent<BookLock>();
                    if (lockComp != null && lockComp.IsProcessing)
                        return;
                }

                if (decideMode && currentHoldMode == HoldMode.None)
                {
                    currentHoldMode = (existingBook != null) ? HoldMode.Remove : HoldMode.Place;
                }

                if (currentHoldMode == HoldMode.Remove && existingBook != null)
                {
                    StartCoroutine(SlideOutAndDestroy(existingBook.transform));
                }
                else if (currentHoldMode == HoldMode.Place && existingBook == null)
                {
                    PlaceBook(shelf);
                }
            }
        }
    }

    private IEnumerator SlideOutAndDestroy(Transform book)
    {
        if (book == null) yield break;

        BookLock lockComp = book.GetComponent<BookLock>();
        if (lockComp != null) lockComp.Lock();

        float t = 0f;
        float duration = 0.3f;
        Vector3 startPos = book.position;
        Vector3 targetPos = startPos + book.parent.forward * 0.5f;

        while (t < duration)
        {
            if (book == null) yield break;
            t += Time.deltaTime;
            book.position = Vector3.Lerp(startPos, targetPos, t / duration);
            yield return null;
        }

        if (book != null)
        {
            // Get category from BookCategoryForInshop component
            BookCategoryForInshop cat = book.GetComponent<BookCategoryForInshop>();
            if (cat != null)
            {
                int categoryIndex = cat.categoryIndex;

                int shopValue;
                if (int.TryParse(inShoptexts[categoryIndex].text, out shopValue))
                    inShoptexts[categoryIndex].text = (shopValue - 1).ToString();

                int currentValue;
                if (int.TryParse(texts[categoryIndex].text, out currentValue))
                    texts[categoryIndex].text = (currentValue + 1).ToString();
            }

            Destroy(book.gameObject);
            SoundManager.Instance.PlayBookPlaced();
            Debug.Log("Removed book.");
        }
    }

    private void PlaceBook(Transform shelf)
    {
        if (selectedCategoryIndex >= 0 && categoryPrefabs.Count > selectedCategoryIndex)
        {
            int currentValue;
            if (int.TryParse(texts[selectedCategoryIndex].text, out currentValue) && currentValue > 0)
            {
                texts[selectedCategoryIndex].text = (currentValue - 1).ToString();

                // Random prefab from the selected category
                var prefabs = categoryPrefabs[selectedCategoryIndex].prefabs;
                int randomIndex = Random.Range(0, prefabs.Count);

                Vector3 snapPos = shelf.position;
                snapPos.y += -0.2f;
                Vector3 startPos = snapPos + shelf.forward * 0.5f;

                GameObject newBook = Instantiate(
                    prefabs[randomIndex],
                    startPos,
                    Quaternion.Euler(-90f, 180f, 0f)
                );

                newBook.transform.SetParent(shelf);
                newBook.tag = "Book";

                // Add lock
                BookLock lockComp = newBook.AddComponent<BookLock>();
                lockComp.Lock();

                // Add category info
                BookCategoryForInshop cat = newBook.AddComponent<BookCategoryForInshop>();
                cat.categoryIndex = selectedCategoryIndex;

                StartCoroutine(SlideInAndUnlock(newBook.transform, snapPos, lockComp));

                int shopValue;
                if (int.TryParse(inShoptexts[selectedCategoryIndex].text, out shopValue))
                    inShoptexts[selectedCategoryIndex].text = (shopValue + 1).ToString();

                SoundManager.Instance.PlayBookPlaced();
                Debug.Log("Placed book.");
            }
        }
    }

    private IEnumerator SlideIn(Transform book, Vector3 targetPos)
    {
        float t = 0f;
        float duration = 0.3f;
        Vector3 startPos = book.position;

        while (t < duration)
        {
            t += Time.deltaTime;
            book.position = Vector3.Lerp(startPos, targetPos, t / duration);
            yield return null;
        }

        book.position = targetPos;
    }

    private IEnumerator SlideInAndUnlock(Transform book, Vector3 targetPos, BookLock lockComp)
    {
        yield return SlideIn(book, targetPos);
        if (lockComp != null) lockComp.Unlock();
    }
}

// Helper component to store category info for in-shop tracking
public class BookCategoryForInshop : MonoBehaviour
{
    public int categoryIndex;
}
