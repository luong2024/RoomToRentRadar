namespace RoomToRentRadar.Shared.Dtos;

/// <summary>Giá trị <c>sortBy</c> hợp lệ cho tìm kiếm (T04.1) và tìm theo vùng vẽ (T05.2).</summary>
public static class ListingSortKeys
{
    public const string Distance = "distance";
    public const string Rent = "rent";
    public const string RentAsc = "rent_asc";
    public const string RentDesc = "rent_desc";
    public const string Price = "price";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string Area = "area";
    public const string AreaDesc = "area_desc";

    public const string Default = Distance;
}
