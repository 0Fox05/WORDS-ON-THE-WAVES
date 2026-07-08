using UnityEngine;
using System.Collections.Generic;

public class CartDeco : MonoBehaviour
{
    public static CartDeco Instance;

    [System.Serializable]
    public class DecoItem
    {
        public string name;
        public Renderer renderer;          // drag the MeshRenderer here
        [HideInInspector] public Material[] originalMaterials; // saved at runtime
        public bool isActive = true;       // starts as normal
    }

    public List<DecoItem> items;
    public Material shadowMaterial;        // one global shadow material

    public void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        foreach (var item in items)
        {
            if (item.renderer != null)
            {
                item.originalMaterials = item.renderer.materials;          
            }
        }
    }
    public void UpdateItems() 
    {
        foreach (var item in items)
        {
            if (item.renderer != null)
            {
                // Check player ownership
                int haveCount = GetPlayerItemCount(item.name);

                if (haveCount <= 0)
                {
                    // Player doesn’t own → force shadow
                    item.isActive = false;
                    ApplyShadow(item);
                }
                else if (!item.isActive)
                {
                    ApplyShadow(item);
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                foreach (var item in items)
                {
                    if (item.renderer != null && hit.collider.gameObject == item.renderer.gameObject)
                    {
                        // Only toggle if player owns it
                        if (GetPlayerItemCount(item.name) > 0)
                        {
                            ToggleItem(item.name);
                        }
                        else
                        {
                            Debug.Log($"{item.name} not owned, stays shadow.");
                        }
                        break;
                    }
                }
            }
        }
    }

    public void ToggleItem(string itemName)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].name == itemName)
            {
                DecoItem item = items[i];
                item.isActive = !item.isActive;

                if (item.isActive)
                    ApplyOriginal(item);
                else
                    ApplyShadow(item);
                return;
            }
        }
        Debug.LogWarning("Item not found: " + itemName);
    }

    private void ApplyOriginal(DecoItem item)
    {
        item.renderer.materials = item.originalMaterials;
    }

    private void ApplyShadow(DecoItem item)
    {
        Material[] shadowArray = new Material[item.originalMaterials.Length];
        for (int i = 0; i < shadowArray.Length; i++)
            shadowArray[i] = shadowMaterial;
        item.renderer.materials = shadowArray;
    }

    // Helper: check player’s inventory
    private int GetPlayerItemCount(string itemName)
    {
        if (DataManager.Instance != null && DataManager.Instance.PlayerData != null)
        {
            var entry = DataManager.Instance.PlayerData.ItemsCart.Find(i => i.Name == itemName);
            if (entry != null) return entry.Have;
        }
        return 0;
    }
}
