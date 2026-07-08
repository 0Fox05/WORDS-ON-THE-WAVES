using UnityEngine;

public class GhostHover : MonoBehaviour
{
    public GameObject ghostPrefab;   // assign a prefab in Inspector
    private GameObject ghostInstance;

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.collider.CompareTag("Shelf"))
            {
                // If ghost not yet created, instantiate it
                if (ghostInstance == null && ghostPrefab != null)
                {
                    ghostInstance = Instantiate(
                        ghostPrefab,
                        hit.collider.transform.position + Vector3.up * -0.2f,
                        Quaternion.Euler(-90f, 180f, 0f)
                    );

                    ghostInstance.transform.SetParent(hit.collider.transform);

                    // Make ghost semi-transparent
                    Renderer rend = ghostInstance.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        Color c = rend.material.color;
                        c.a = 0.3f;
                        rend.material.color = c;
                    }
                }
                else if (ghostInstance != null)
                {
                    // Move ghost to follow shelf under mouse
                    ghostInstance.transform.position = hit.collider.transform.position + Vector3.up * -0.2f;
                    ghostInstance.transform.SetParent(hit.collider.transform);
                }
            }
            else
            {
                // Not hovering a shelf → remove ghost
                if (ghostInstance != null)
                {
                    Destroy(ghostInstance);
                    ghostInstance = null;
                }
            }
        }
        else
        {
            // No raycast hit → remove ghost
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }
        }
    }
}
