using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class BookShelf : MonoBehaviour
{
    public Button[] buttons;
    public List<TextMeshProUGUI> texts;
    public List<GameObject> prefabs;
    public List<TextMeshProUGUI> inShoptexts;

    private int selectedCategoryIndex = -1; // which button is currently selected
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

        // Default to Crime
        selectedCategoryIndex = 0;
        Debug.Log("Default category set to Crime.");
    }

    void OnCategorySelected(int index)
    {
        selectedCategoryIndex = index;
        Debug.Log($"Selected category index: {index}");
    }

    void Update()
    {
        // Reset hold mode when mouse released
            if (Input.GetMouseButtonUp(0))
            {
                currentHoldMode = HoldMode.None;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleShelfInteraction(true);
            }
            else if (Input.GetMouseButton(0)) // use else-if
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

                // Look for a child tagged "Book"
                GameObject existingBook = null;
                foreach (Transform child in shelf)
                {
                    if (child.CompareTag("Book"))
                    {
                        existingBook = child.gameObject;
                        break;
                    }
                }

                // Decide mode if this is the first click/hold
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
            // ✅ Determine category by color
            Renderer rend = book.GetComponent<Renderer>();
            if (rend != null)
            {
                Color c = rend.material.color;
                int categoryIndex = -1;

                if (c == Color.red) categoryIndex = 0;       // Crime
                else if (c == Color.green) categoryIndex = 1; // Drama
                else if (c == Color.gray) categoryIndex = 2;  // Fact
                else if (c == Color.magenta) categoryIndex = 3; // Fantasy
                else if (c == Color.yellow) categoryIndex = 4;  // Classic
                else if (c == new Color(1f, 0.204f, 0.584f)) categoryIndex = 5; // Kids
                else if (c == Color.blue) categoryIndex = 6;  // Travel

                if (categoryIndex >= 0)
                {
                    // Mirror place logic: decrease shop, increase stock
                    int shopValue;
                    if (int.TryParse(inShoptexts[categoryIndex].text, out shopValue))
                        inShoptexts[categoryIndex].text = (shopValue - 1).ToString();

                    int currentValue;
                    if (int.TryParse(texts[categoryIndex].text, out currentValue))
                        texts[categoryIndex].text = (currentValue + 1).ToString();
                }
            }

            Destroy(book.gameObject);
            SoundManager.Instance.PlayBookPlaced();
            Debug.Log("Removed book.");
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
    private void PlaceBook(Transform shelf)
    {
        if (selectedCategoryIndex >= 0 && prefabs.Count > 0)
        {
            int currentValue;
            if (int.TryParse(texts[selectedCategoryIndex].text, out currentValue) && currentValue > 0)
            {
                int randomIndex = Random.Range(0, prefabs.Count);

                Vector3 snapPos = shelf.position;
                snapPos.y += -0.2f;

                // Start slightly forward on Z
                Vector3 startPos = snapPos + shelf.forward * 0.5f;

                GameObject newBook = Instantiate(
                    prefabs[randomIndex],
                    startPos,
                    Quaternion.Euler(-90f, 180f, 0f)
                );

                newBook.transform.SetParent(shelf);
                newBook.tag = "Book";

                // Animate slide in
                StartCoroutine(SlideIn(newBook.transform, snapPos));

                // Apply category color
                Color bookColor = Color.white;
                switch (selectedCategoryIndex)
                {
                    case 0: bookColor = Color.red; break;
                    case 1: bookColor = Color.green; break;
                    case 2: bookColor = Color.gray; break;
                    case 3: bookColor = Color.magenta; break;
                    case 4: bookColor = Color.yellow; break;
                    case 5: bookColor = new Color(1f, 0.204f, 0.584f); break; // Kids
                    case 6: bookColor = Color.blue; break;
                }

                Renderer rend = newBook.GetComponent<Renderer>();
                if (rend != null) rend.material.color = bookColor;

                texts[selectedCategoryIndex].text = (currentValue - 1).ToString();

                int shopValue;
                if (int.TryParse(inShoptexts[selectedCategoryIndex].text, out shopValue))
                    inShoptexts[selectedCategoryIndex].text = (shopValue + 1).ToString();

                SoundManager.Instance.PlayBookPlaced();
                Debug.Log("Placed book.");
            }
        }
    }
}
