using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 7 (AC-3): the not-yet-implemented gateway operations are Http stubs returning a
/// faulted task with NotImplementedException("Txx.y: operationId ..."); the operations listed in
/// <c>RealOperationIds</c> are real. Task-id families are cross-checked against docs/use-cases.md §4 (PLAN §5.4).
/// </summary>
[TestFixture]
public class HttpGatewayStubsTests
{
    private static readonly Type[] GatewayInterfaces = typeof(IRoomGateway).Assembly
        .GetTypes()
        .Where(t => t.IsInterface
            && t.Namespace == "RoomToRentRadar.Client.Adapters.Abstractions"
            && t.Name.EndsWith("Gateway", StringComparison.Ordinal)
            && t != typeof(IAuthGateway))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    private static readonly MethodInfo[] GatewayMethods =
        GatewayInterfaces.SelectMany(t => t.GetMethods()).ToArray();

    private static readonly string[] RealOperationIds =
        ["searchRooms", "listCampuses", "getRoomDetail", "getSimilarRooms", "getRoomTravelTimes", "updateMyProfile"];

    private static IEnumerable<TestCaseData> StubCases() =>
        GatewayMethods
            .Where(m => !RealOperationIds.Contains(OperationId(m)))
            .Select(m => new TestCaseData(m).SetName($"Stub({m.DeclaringType!.Name}.{m.Name})"));

    [TestCaseSource(nameof(StubCases))]
    public void Stub_ThrowsNotImplementedWithTaskId(MethodInfo method)
    {
        var operationId = OperationId(method);
        var task = InvokeWithDefaultArgs(method);

        var ex = Assert.ThrowsAsync<NotImplementedException>(async () => await task);

        Assert.That(ex!.Message, Does.Match($@"^T\d{{2}}\.\d+: {Regex.Escape(operationId)} "),
            $"{method.DeclaringType!.Name}.{method.Name} must throw NotImplementedException with its task id and operationId.");
    }

    [TestCaseSource(nameof(StubCases))]
    public void Stub_ReturnsFaultedTask_NotSynchronousThrow(MethodInfo method)
    {
        // Invoke must return normally: a synchronous throw would surface as TargetInvocationException here.
        var task = InvokeWithDefaultArgs(method);

        Assert.Multiple(() =>
        {
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception!.InnerException, Is.InstanceOf<NotImplementedException>());
        });
    }

    [Test]
    public async Task RealOperations_CompleteHttpRoundTrip()
    {
        var stubMethods = GatewayMethods.Where(m => !RealOperationIds.Contains(OperationId(m))).ToArray();
        Assert.That(GatewayMethods, Has.Length.EqualTo(50));
        Assert.That(stubMethods, Has.Length.EqualTo(44));
        foreach (var method in stubMethods)
        {
            var task = InvokeWithDefaultArgs(method);
            Assert.That(task.IsFaulted, Is.True, $"{method.DeclaringType!.Name}.{method.Name} must be a stub.");
            Assert.That(task.Exception!.InnerException, Is.InstanceOf<NotImplementedException>());
        }

        // The real operations complete an actual HTTP round-trip instead of throwing NotImplementedException.
        var stub = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/rooms" => Json(new PagedResult<RoomCardDto>([], 0, 1, 20)),
            "/api/campuses" => Json(Array.Empty<CampusDto>()),
            "/api/rooms/room-1" => Json(SampleRoomDetail()),
            "/api/rooms/room-1/similar" => Json(Array.Empty<RoomCardDto>()),
            "/api/rooms/room-1/travel-times" => Json(new TravelTimesDto("ICTU", 0.5, false, [])),
            "/api/me/profile" => Json(SampleProfile()),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        using var client = new HttpClient(stub) { BaseAddress = new Uri("http://localhost") };

        var page = await new HttpRoomGateway(client).SearchRoomsAsync(new RoomSearchRequest());
        var campuses = await new HttpCampusGateway(client).ListCampusesAsync();
        var detail = await new HttpRoomGateway(client).GetRoomDetailAsync("room-1");
        var similar = await new HttpRoomGateway(client).GetSimilarRoomsAsync("room-1");
        var travelTimes = await new HttpRoomGateway(client).GetRoomTravelTimesAsync("room-1");
        var profile = await new HttpProfileGateway(client)
            .UpdateMyProfileAsync(new UpdateMyProfileRequest(null, null, "ICTU", null));

        Assert.That(page.TotalCount, Is.EqualTo(0));
        Assert.That(campuses, Is.Empty);
        Assert.That(detail.Id, Is.EqualTo("room-1"));
        Assert.That(similar, Is.Empty);
        Assert.That(travelTimes.CampusId, Is.EqualTo("ICTU"));
        Assert.That(profile.Id, Is.EqualTo("user-1"));
        Assert.That(stub.CountForPath("/api/me/profile"), Is.EqualTo(1));
        Assert.That(stub.CountForPath("/api/rooms"), Is.EqualTo(1));
        Assert.That(stub.CountForPath("/api/campuses"), Is.EqualTo(1));
        Assert.That(stub.CountForPath("/api/rooms/room-1"), Is.EqualTo(1));
        Assert.That(stub.CountForPath("/api/rooms/room-1/similar"), Is.EqualTo(1));
        Assert.That(stub.CountForPath("/api/rooms/room-1/travel-times"), Is.EqualTo(1));
    }

    [Test]
    public void StubTaskIds_FamilyMatchesUseCaseTraceability()
    {
        var familiesByOperation = ReadUseCaseTraceability();
        foreach (var method in GatewayMethods.Where(m => !RealOperationIds.Contains(OperationId(m))))
        {
            var operationId = OperationId(method);
            var message = InvokeWithDefaultArgs(method).Exception!.InnerException!.Message;
            var family = Regex.Match(message, @"^T(\d{2})\.\d+: ").Groups[1].Value;

            // erd §6.1 assigns the photo endpoints to T03.1 although use-cases §4 lists them under US08.
            if (operationId is "uploadPhoto" or "deletePhoto")
            {
                Assert.That(family, Is.EqualTo("03"), $"{operationId} must stay on the T03 family (erd §6.1).");
                continue;
            }

            Assert.That(familiesByOperation, Does.ContainKey(operationId),
                $"operationId '{operationId}' has no US row in docs/use-cases.md §4.");
            Assert.That(familiesByOperation[operationId], Does.Contain(family),
                $"Task family T{family} of '{operationId}' matches no US row containing it in docs/use-cases.md §4.");
        }
    }

    private static string OperationId(MethodInfo method) =>
        method.GetCustomAttribute<ApiOperationAttribute>()!.OperationId;

    private static Task InvokeWithDefaultArgs(MethodInfo method)
    {
        var httpType = typeof(IRoomGateway).Assembly.GetType(
            "RoomToRentRadar.Client.Adapters.Http.Http" + method.DeclaringType!.Name[1..], throwOnError: true)!;
        var gateway = Activator.CreateInstance(httpType, new HttpClient(), null)!;
        var args = method.GetParameters().Select(DefaultArg).ToArray();
        return (Task)method.Invoke(gateway, args)!;
    }

    private static object? DefaultArg(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
        {
            return null;
        }

        return Activator.CreateInstance(parameter.ParameterType);
    }

    private static IReadOnlyDictionary<string, HashSet<string>> ReadUseCaseTraceability()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        string? path = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "use-cases.md");
            if (File.Exists(candidate))
            {
                path = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.That(path, Is.Not.Null, "docs/use-cases.md was not found by walking up from AppContext.BaseDirectory.");
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path!))
        {
            var row = Regex.Match(line, @"^\| US(\d{2}) \|");
            if (!row.Success)
            {
                continue;
            }

            var family = row.Groups[1].Value;
            foreach (Match token in Regex.Matches(line, @"`([^`]+)`"))
            {
                var operationId = token.Groups[1].Value;
                if (!map.TryGetValue(operationId, out var families))
                {
                    map[operationId] = families = new HashSet<string>(StringComparer.Ordinal);
                }

                families.Add(family);
            }
        }

        return map;
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, ApiResponseReader.JsonOptions), Encoding.UTF8, "application/json")
        };

    private static UserProfileDto SampleProfile() => new(
        "user-1", "student@ictu.edu.vn", "Student", "Student", "Local", null, null, true,
        new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "ICTU", "en");

    private static RoomDetailDto SampleRoomDetail() => new(
        "room-1", "Phòng trọ test", "Mô tả", RoomType.BoardingRoom, ListingStatus.Available, true,
        2_500_000, 2_500_000, 25.0, 2, Furnishing.Full, new DateOnly(2026, 12, 1),
        new AddressDto("12 Z115", "Quyết Thắng", "Thái Nguyên"),
        new GeoPointDto(21.5851, 105.8066),
        1.2,
        new UtilitiesView(3500, 100000, WaterPriceUnit.PerPerson, null, 50000, null, 2950000),
        [Amenity.Wifi, Amenity.AirConditioner],
        new SafetyDto(4, true, [SafetyFeature.Cctv, SafetyFeature.SecureEntrance]),
        null,
        Array.Empty<PhotoDto>(),
        new LandlordContactDto("landlord-1", "Nguyễn Thị Hoa", true, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), null, null),
        false,
        new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
}