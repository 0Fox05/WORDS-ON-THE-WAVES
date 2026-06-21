using UnityEngine;

public class DataManager : MonoBehaviour
{
    public LocationConfig LocationData;
    public CrateConfig CrateData;

    void Awake()
    {
        LoadLocations("JsonData/locations");
        LoadCrates("JsonData/crates");
    }

    void LoadLocations(string resourcePath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            LocationData = JsonUtility.FromJson<LocationConfig>(jsonFile.text);

            Debug.Log("=== Loaded Locations ===");
            foreach (var loc in LocationData.Locations)
            {
                Debug.Log($"Location: {loc.Name}, Fee: {loc.TravelFee}, Targets: {string.Join(", ", loc.TargetCustomers)}");
            }
        }
        else
        {
            Debug.LogError($"Locations file not found at Resources/{resourcePath}");
        }
    }

    void LoadCrates(string resourcePath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);
        if (jsonFile != null)
        {
            CrateData = JsonUtility.FromJson<CrateConfig>(jsonFile.text);

            Debug.Log("=== Loaded Crates ===");
            foreach (var crate in CrateData.Crates)
            {
                Debug.Log($"Crate: {crate.Name}, Price: {crate.Price}, TotalBooks: {crate.TotalBooks}");
                foreach (var rate in crate.DropRates)
                {
                    Debug.Log($"   {rate.Category}: {rate.Rate * 100}%");
                }
            }
        }
        else
        {
            Debug.LogError($"Crates file not found at Resources/{resourcePath}");
        }
    }
}
