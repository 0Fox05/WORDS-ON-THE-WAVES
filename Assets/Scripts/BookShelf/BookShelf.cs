using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BookShelf : MonoBehaviour
{
    public Button[] buttons;
    public List<TextMeshProUGUI> texts;
    public List<GameObject> prefabs;
    public List<TextMeshProUGUI> inShoptexts;

    private int selectedCategoryIndex = -1; // which button is currently selected

    void Start()
    {
        if (buttons.Length != texts.Count)
        {
            Debug.LogError("Mismatch! Buttons and texts must have the same length.");
            return;
        }

        buttons[0].onClick.AddListener(() => OnCategorySelected(0));
        buttons[1].onClick.AddListener(() => OnCategorySelected(1));
        buttons[2].onClick.AddListener(() => OnCategorySelected(2));
        buttons[3].onClick.AddListener(() => OnCategorySelected(3));
        buttons[4].onClick.AddListener(() => OnCategorySelected(4));
        buttons[5].onClick.AddListener(() => OnCategorySelected(5));
        buttons[6].onClick.AddListener(() => OnCategorySelected(6));
    }

    void OnCategorySelected(int index)
    {
        selectedCategoryIndex = index;
        Debug.Log($"Selected category index: {index}");
    }
    bool ColorsAreClose(Color a, Color b, float tolerance = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // left click
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

                    // --- Removing a book ---
                    if (existingBook != null)
                    {
                        Renderer rend = existingBook.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            Color c = rend.material.color;

                            int categoryIndex = -1;
                            if (c == Color.red) categoryIndex = 0;        // Crime
                            else if (c == Color.green) categoryIndex = 1; // Drama
                            else if (c == Color.gray) categoryIndex = 2;  // Fact
                            else if (c == Color.magenta) categoryIndex = 3; // Fantasy
                            else if (c == Color.yellow) categoryIndex = 4; // Classic
                            else if (ColorsAreClose(c, new Color(1f, 0.204f, 0.584f))) categoryIndex = 5; // Kids (pink)
                            else if (c == Color.blue) categoryIndex = 6;  // Travel

                            if (categoryIndex >= 0)
                            {
                                // Update player texts
                                int currentValue;
                                if (int.TryParse(texts[categoryIndex].text, out currentValue))
                                    texts[categoryIndex].text = (currentValue + 1).ToString();

                                // Update shop texts
                                int shopValue;
                                if (int.TryParse(inShoptexts[categoryIndex].text, out shopValue))
                                    inShoptexts[categoryIndex].text = (shopValue - 1).ToString();
                            }
                        }

                        Destroy(existingBook);
                        Debug.Log("Removed book from shelf.");
                    }
                    else
                    {
                        // --- Placing a book ---
                        if (selectedCategoryIndex >= 0 && prefabs.Count > 0)
                        {
                            int currentValue;
                            if (int.TryParse(texts[selectedCategoryIndex].text, out currentValue))
                            {
                                if (currentValue > 0)
                                {
                                    int randomIndex = Random.Range(0, prefabs.Count);

                                    Vector3 snapPos = shelf.position;
                                    snapPos.y += -0.2f;

                                    GameObject newBook = Instantiate(
                                        prefabs[randomIndex],
                                        snapPos,
                                        Quaternion.Euler(-90f, 180f, 0f)
                                    );

                                    newBook.transform.SetParent(shelf);
                                    newBook.tag = "Book";

                                    // Apply color based on category
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

                                    Debug.Log("Placed new book with category color.");

                                    // Update player texts
                                    texts[selectedCategoryIndex].text = (currentValue - 1).ToString();

                                    // Update shop texts
                                    int shopValue;
                                    if (int.TryParse(inShoptexts[selectedCategoryIndex].text, out shopValue))
                                        inShoptexts[selectedCategoryIndex].text = (shopValue + 1).ToString();
                                }
                                else
                                {
                                    Debug.Log("Cannot place book: category count is 0.");
                                }
                            }
                        }
                        else
                        {
                            Debug.Log("No category selected, cannot place book.");
                        }
                    }
                }
            }
        }
    }
}