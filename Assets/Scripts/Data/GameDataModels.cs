using System.Collections.Generic;

public enum BookCategory
{
    Crime,
    Drama,
    Fact,
    Fantasy,
    Classic,
    Kids,
    Travel
}

[System.Serializable]
public class MapLocation
{
    public string Name;
    public int TravelFee;
    public List<string> TargetCustomers;
}

[System.Serializable]
public class DropRateEntry
{
    public BookCategory Category;
    public float Rate;
}

[System.Serializable]
public class BookCrate
{
    public string Name;
    public float Price;
    public int TotalBooks;
    public List<DropRateEntry> DropRates;
}

[System.Serializable]
public class LocationConfig
{
    public List<MapLocation> Locations;
}

[System.Serializable]
public class CrateConfig
{
    public List<BookCrate> Crates;
}
