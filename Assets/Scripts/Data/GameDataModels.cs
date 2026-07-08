using System;
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

[Serializable]
public class MapLocation
{
    public string Name;
    public int TravelFee;
    public List<string> TargetCustomers;
}

[Serializable]
public class DropRate
{
    public string Category;   
    public float Rate;
}

[Serializable]
public class Crate
{
    public string Name;
    public float Price;
    public int TotalBooks;
    public List<DropRate> DropRates;
}

[Serializable]
public class CrateConfig
{
    public List<Crate> Crates;
}

[Serializable]
public class LocationConfig
{
    public List<MapLocation> Locations;
}

[Serializable]
public class PlayerBookEntry
{
    public BookCategory Category;
    public int Have;
}

[Serializable]
public class PlayerCartEntry
{
    public string Name;
    public int Have;
}

[Serializable]
public class PlayerData
{
    public List<PlayerBookEntry> Books;
    public List<PlayerCartEntry> ItemsCart;  
    public int Money;
}

[Serializable]
public class DialogueEntry
{
    public string CorrectBook;
    public List<string> lines;
}

[Serializable]
public class DialogueLData
{
    public List<DialogueEntry> Question;
}

[Serializable]
public class ShopItem
{
    public string Name;
    public int Money;
}

[Serializable]
public class ShopConfig
{
    public List<ShopItem> ItemsShop;
}

