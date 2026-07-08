using UnityEngine;
using System.Collections.Generic;

public class CartPlaceManage : MonoBehaviour
{
    public static CartPlaceManage Instance { get; private set; }

    [System.Serializable]
    public class Place
    {
        public string name;        // e.g. "Square", "Beach"
        public Transform location; // assign marker in Inspector
    }

    public List<Place> places;
    public GameObject objectToMove; // assign your cart in Inspector

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // Move by index
    public void NowMoveTo(int index)
    {
        if (index >= 0 && index < places.Count)
        {
            if (objectToMove != null)
            {
                // Make the cart a child of the target location
                objectToMove.transform.SetParent(places[index].location);

                // Move to the specific offset relative to the parent
                objectToMove.transform.localPosition = Vector3.zero;

                // Optional: align rotation with parent
                objectToMove.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning("No object assigned to move!");
            }
        }
        else
        {
            Debug.LogWarning("Place index out of range: " + index);
        }
    }

    // Move by name
    public void NowMoveTo(string placeName)
    {
        for (int i = 0; i < places.Count; i++)
        {
            if (places[i].name == placeName)
            {
                NowMoveTo(i);
                return;
            }
        }
        Debug.LogWarning("Place not found: " + placeName);
    }
}
