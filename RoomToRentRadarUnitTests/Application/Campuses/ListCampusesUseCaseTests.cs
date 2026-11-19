using NSubstitute;
using RoomToRentRadar.Application.UseCases.Campuses;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Campuses;

[TestFixture]
public class ListCampusesUseCaseTests
{
    private ICampusRepository _campusRepository = null!;
    private ListCampusesUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _campusRepository = Substitute.For<ICampusRepository>();
        _sut = new ListCampusesUseCase(_campusRepository);
    }

    [Test]
    public async Task ExecuteAsync_WhenCampusesExist_ReturnsMappedDtosWithDefaultFirstThenBySortOrder()
    {
        // Arrange: unordered list with default campus in the middle
        var campuses = new List<Campus>
        {
            new("TNUS", "Trường Đại học Khoa học", "TNUS", 21.58870, 105.80979, isDefault: false, sortOrder: 2),
            new("ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1),
            new("SFL", "Trường Ngoại ngữ", "SFL", 21.59120, 105.80744, isDefault: false, sortOrder: 3),
            new("TUEBA", "Trường Đại học Kinh tế và Quản trị Kinh doanh", "TUEBA", 21.59007, 105.81299, isDefault: false, sortOrder: 4)
        };

        _campusRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(campuses);

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Count, Is.EqualTo(4));
            // Default campus first (BR-CMP-01)
            Assert.That(result[0].Id, Is.EqualTo("ICTU"));
            Assert.That(result[0].IsDefault, Is.True);
            Assert.That(result[0].Name, Is.EqualTo("Trường Đại học Công nghệ Thông tin và Truyền thông"));
            Assert.That(result[0].ShortName, Is.EqualTo("ICTU"));
            Assert.That(result[0].Latitude, Is.EqualTo(21.5851));
            Assert.That(result[0].Longitude, Is.EqualTo(105.8066));

            // Others sorted by SortOrder ascending (BR-CMP-01, openapi.yaml:219)
            Assert.That(result[1].Id, Is.EqualTo("TNUS"));
            Assert.That(result[2].Id, Is.EqualTo("SFL"));
            Assert.That(result[3].Id, Is.EqualTo("TUEBA"));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenRepositoryReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _campusRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Campus>());

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _campusRepository.GetAllAsync(token).Returns(Array.Empty<Campus>());

        // Act
        await _sut.ExecuteAsync(token);

        // Assert
        await _campusRepository.Received(1).GetAllAsync(token);
    }
}
