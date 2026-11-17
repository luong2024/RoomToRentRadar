using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Mocks.SampleData;

/// <summary>
/// Fixed copy of the eight campuses in <c>CampusSeedData</c> (BR-CMP-01, docs/erd.md §4.1),
/// ICTU (the default) first. MockSeedDriftTests breaks on purpose if the seed changes (PLAN Q6/Q11).
/// </summary>
public static class SampleCampuses
{
    public static IReadOnlyList<CampusDto> All { get; } = new List<CampusDto>
    {
        new("ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, true),
        new("TNUS", "Trường Đại học Khoa học", "TNUS", 21.58870, 105.80979, false),
        new("SFL", "Trường Ngoại ngữ", "SFL", 21.59120, 105.80744, false),
        new("TUEBA", "Trường Đại học Kinh tế và Quản trị Kinh doanh", "TUEBA", 21.59007, 105.81299, false),
        new("TUAF", "Trường Đại học Nông Lâm", "TUAF", 21.59462, 105.80550, false),
        new("TNUE", "Trường Đại học Sư phạm", "TNUE", 21.59861, 105.82163, false),
        new("TUMP", "Trường Đại học Y Dược", "TUMP", 21.58827, 105.82712, false),
        new("TNUT", "Trường Đại học Kỹ thuật Công nghiệp", "TNUT", 21.54954, 105.84350, false)
    };

    public static CampusDto Default => All.Single(campus => campus.IsDefault);

    public static CampusDto? Find(string id) => All.FirstOrDefault(campus => campus.Id == id);
}