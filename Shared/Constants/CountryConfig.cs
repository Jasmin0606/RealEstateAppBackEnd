namespace Shared.Constants;

public class CountryField
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Type { get; set; }  // text, number, select
    public List<string>? Options { get; set; }

    public CountryField(string key, string label, string type, List<string>? options = null)
    {
        Key = key;
        Label = label;
        Type = type;
        Options = options;
    }
}

public class CountryConfig
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
    public string DateFormat { get; set; } = string.Empty;
    public string PhonePrefix { get; set; } = string.Empty;
    public string LocationProvider { get; set; } = string.Empty; // postcode | zipcode | emirate
    public string SizeUnit { get; set; } = string.Empty;         // sqft | sqm
    public List<string> PropertyTypes { get; set; } = [];
    public List<string> Regions { get; set; } = [];              // Boroughs / States / Emirates
    public List<CountryField> ExtraFields { get; set; } = [];
}

public static class SupportedCountries
{
    public static readonly CountryConfig UK = new()
    {
        CountryCode    = "GB",
        CountryName    = "United Kingdom",
        CurrencyCode   = "GBP",
        CurrencySymbol = "£",
        DateFormat     = "dd/MM/yyyy",
        PhonePrefix    = "+44",
        LocationProvider = "postcode",
        SizeUnit       = "sqft",
        PropertyTypes  = ["Flat", "Terraced", "Semi-Detached", "Detached", "Bungalow", "Studio", "Maisonette"],
        Regions =
        [
            "Barking and Dagenham", "Barnet", "Bexley", "Brent", "Bromley",
            "Camden", "City of London", "Croydon", "Ealing", "Enfield",
            "Greenwich", "Hackney", "Hammersmith and Fulham", "Haringey", "Harrow",
            "Havering", "Hillingdon", "Hounslow", "Islington", "Kensington and Chelsea",
            "Kingston upon Thames", "Lambeth", "Lewisham", "Merton", "Newham",
            "Redbridge", "Richmond upon Thames", "Southwark", "Sutton", "Tower Hamlets",
            "Waltham Forest", "Wandsworth", "Westminster"
        ],
        ExtraFields =
        [
            new("Tenure",          "Tenure",             "select", ["Freehold", "Leasehold", "Shared Ownership", "Share of Freehold"]),
            new("EPCRating",       "EPC Rating",          "select", ["A", "B", "C", "D", "E", "F", "G"]),
            new("CouncilTaxBand",  "Council Tax Band",    "select", ["A", "B", "C", "D", "E", "F", "G", "H"]),
            new("NearestTube",     "Nearest Tube Station","text"),
            new("TubeZone",        "Tube Zone",           "select", ["Zone 1", "Zone 2", "Zone 3", "Zone 4", "Zone 5", "Zone 6"]),
            new("ParkingType",     "Parking",             "select", ["None", "On-Street", "Off-Street", "Garage", "Allocated"])
        ]
    };

    public static readonly CountryConfig USA = new()
    {
        CountryCode    = "US",
        CountryName    = "United States",
        CurrencyCode   = "USD",
        CurrencySymbol = "$",
        DateFormat     = "MM/dd/yyyy",
        PhonePrefix    = "+1",
        LocationProvider = "zipcode",
        SizeUnit       = "sqft",
        PropertyTypes  = ["Single Family", "Condo", "Townhouse", "Multi-Family", "Land", "Commercial"],
        Regions        = ["California", "New York", "Texas", "Florida", "Illinois"],
        ExtraFields    =
        [
            new("HOAFee",       "HOA Fee/month ($)",    "number"),
            new("PropertyTax",  "Annual Property Tax ($)", "number"),
            new("YearBuilt",    "Year Built",            "number"),
            new("GarageSpaces", "Garage Spaces",         "select", ["0", "1", "2", "3+"])
        ]
    };

    public static readonly CountryConfig UAE = new()
    {
        CountryCode    = "AE",
        CountryName    = "United Arab Emirates",
        CurrencyCode   = "AED",
        CurrencySymbol = "د.إ",
        DateFormat     = "dd/MM/yyyy",
        PhonePrefix    = "+971",
        LocationProvider = "emirate",
        SizeUnit       = "sqft",
        PropertyTypes  = ["Apartment", "Villa", "Townhouse", "Penthouse", "Studio", "Office", "Plot"],
        Regions        = ["Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Ras Al Khaimah", "Fujairah", "Umm Al Quwain"],
        ExtraFields    =
        [
            new("ServiceCharge", "Service Charge (AED/sqft/yr)", "number"),
            new("Furnished",     "Furnished",                    "select", ["Unfurnished", "Semi-Furnished", "Fully Furnished"]),
            new("Completion",    "Completion Status",            "select", ["Ready", "Off-Plan"]),
            new("Views",         "Views",                        "select", ["Sea View", "City View", "Garden View", "Pool View", "No View"])
        ]
    };

    public static readonly Dictionary<string, CountryConfig> All = new()
    {
        { "GB", UK },
        { "US", USA },
        { "AE", UAE }
    };

    public static CountryConfig? GetByCode(string countryCode) =>
        All.TryGetValue(countryCode.ToUpperInvariant(), out var config) ? config : null;
}


/// <summary>
/// Cache key factory — keeps keys consistent across the app.
/// Always includes countryCode to prevent data leaking between countries.
/// </summary>
public static class CacheKeys
{
    public static string PropertyDetail(string countryCode, Guid id) =>
        $"property:{countryCode}:detail:{id}";

    public static string PropertySearch(string countryCode, string filterHash) =>
        $"properties:{countryCode}:search:{filterHash}";

    public static string SellerProperties(string countryCode, Guid sellerId) =>
        $"properties:{countryCode}:seller:{sellerId}";

    public static string UserFavourites(Guid userId) =>
        $"favourites:{userId}";
}