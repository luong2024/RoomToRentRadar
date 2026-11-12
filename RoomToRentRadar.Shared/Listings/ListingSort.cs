using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum ListingSort
{
    [JsonStringEnumMemberName("newest")]
    Newest,

    [JsonStringEnumMemberName("price_asc")]
    PriceAsc,

    [JsonStringEnumMemberName("price_desc")]
    PriceDesc,

    [JsonStringEnumMemberName("area_desc")]
    AreaDesc,

    [JsonStringEnumMemberName("distance_asc")]
    DistanceAsc
}

public static class ListingSortWire
{
    public const string Newest = "newest";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string AreaDesc = "area_desc";
    public const string DistanceAsc = "distance_asc";

    public static bool TryParse(string? value, out ListingSort sort)
    {
        switch (value)
        {
            case Newest:
                sort = ListingSort.Newest;
                return true;
            case PriceAsc:
                sort = ListingSort.PriceAsc;
                return true;
            case PriceDesc:
                sort = ListingSort.PriceDesc;
                return true;
            case AreaDesc:
                sort = ListingSort.AreaDesc;
                return true;
            case DistanceAsc:
                sort = ListingSort.DistanceAsc;
                return true;
            default:
                sort = default;
                return false;
        }
    }

    public static string ToWireValue(ListingSort sort) => sort switch
    {
        ListingSort.Newest => Newest,
        ListingSort.PriceAsc => PriceAsc,
        ListingSort.PriceDesc => PriceDesc,
        ListingSort.AreaDesc => AreaDesc,
        ListingSort.DistanceAsc => DistanceAsc,
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
    };
}
