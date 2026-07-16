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
        public Transform spawnLocation;
        public Transform customerPlace;
    }

    public List<Place> places;
    public GameObject objectToMove; // assign your cart in Inspector
    public GameObject spawnerObject;
    public GameObject customerGoal;

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
            Place target = places[index];

            // Move cart
            if (objectToMove != null && target.location != null)
            {
                objectToMove.transform.SetParent(target.location, false);
                objectToMove.transform.localPosition = Vector3.zero;
                objectToMove.transform.localRotation = Quaternion.identity;
            }

            // Move spawner
            if (spawnerObject != null && target.spawnLocation != null)
            {
                spawnerObject.transform.SetParent(target.spawnLocation, false);
                spawnerObject.transform.localPosition = Vector3.zero;
                spawnerObject.transform.localRotation = Quaternion.identity;
            }

            // Move customer
            if (customerGoal != null && target.customerPlace != null)
            {
                customerGoal.transform.SetParent(target.customerPlace, false);
                customerGoal.transform.localPosition = Vector3.zero;
                customerGoal.transform.localRotation = Quaternion.identity;
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
