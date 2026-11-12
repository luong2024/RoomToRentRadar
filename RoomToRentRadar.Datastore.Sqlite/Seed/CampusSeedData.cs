using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// BR-CMP-01..03 / <c>docs/erd.md</c> §4.1: the eight campuses of Thái Nguyên University.
/// This is reference data — it is seeded on every database initialisation, in every environment,
/// and is never edited by the application. Exactly one row has <c>IsDefault = true</c> (ICTU).
/// </summary>
public static class CampusSeedData
{
    public const string DefaultCampusId = "ICTU";

    public static IReadOnlyList<Campus> CreateCampuses() => new List<Campus>(8)
    {
        new("ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1),
        new("TNUS", "Trường Đại học Khoa học", "TNUS", 21.58870, 105.80979, isDefault: false, sortOrder: 2),
        new("SFL", "Trường Ngoại ngữ", "SFL", 21.59120, 105.80744, isDefault: false, sortOrder: 3),
        new("TUEBA", "Trường Đại học Kinh tế và Quản trị Kinh doanh", "TUEBA", 21.59007, 105.81299, isDefault: false, sortOrder: 4),
        new("TUAF", "Trường Đại học Nông Lâm", "TUAF", 21.59462, 105.80550, isDefault: false, sortOrder: 5),
        new("TNUE", "Trường Đại học Sư phạm", "TNUE", 21.59861, 105.82163, isDefault: false, sortOrder: 6),
        new("TUMP", "Trường Đại học Y Dược", "TUMP", 21.58827, 105.82712, isDefault: false, sortOrder: 7),
        new("TNUT", "Trường Đại học Kỹ thuật Công nghiệp", "TNUT", 21.54954, 105.84350, isDefault: false, sortOrder: 8)
    };
}
