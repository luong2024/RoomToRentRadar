using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Unit test suite for US10 — Build distance &amp; estimated travel time calculation service (T10.1).
    /// Tests strictly enforce Acceptance Criteria:
    ///   AC1: Detail screen shows distance and estimated travel time by motorbike, bus, and walk.
    ///   AC2: The estimate uses ICTU campus as destination.
    ///   AC3: If routing service is unavailable, straight-line distance is shown as fallback.
    /// </summary>
    [CancelAfter(5000)]
    [TestFixture]
    public class TravelTimeServiceTests
    {
        private ITravelTimeService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _service = new TravelTimeService();
        }

        #region AC2: Default Destination Uses ICTU Campus

        [Test]
        public void EstimateTravelTime_WithoutCustomDestination_DefaultsToIctuCampus()
        {
            // Arrange: Room in Quyet Thang ward near ICTU
            double roomLat = 21.5890;
            double roomLon = 105.8100;

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon);

            // Assert (AC2)
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DestinationLatitude, Is.EqualTo(Constants.IctuLatitude).Within(0.0001));
            Assert.That(result.DestinationLongitude, Is.EqualTo(Constants.IctuLongitude).Within(0.0001));
            Assert.That(result.DestinationName, Is.EqualTo(Constants.IctuCampusName));
        }

        [Test]
        public void EstimateTravelTime_WithCustomDestination_UsesProvidedDestination()
        {
            // Arrange: Room and a custom destination (e.g., Song Cong center)
            double roomLat = 21.5850;
            double roomLon = 105.8064;
            double destLat = 21.4850;
            double destLon = 105.8150;
            string destName = "Trung tâm TP Sông Công";

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon, destLat, destLon, destName);

            // Assert
            Assert.That(result.DestinationLatitude, Is.EqualTo(destLat).Within(0.0001));
            Assert.That(result.DestinationLongitude, Is.EqualTo(destLon).Within(0.0001));
            Assert.That(result.DestinationName, Is.EqualTo(destName));
        }

        [Test]
        public void EstimateTravelTime_WithWhitespaceDestination_FallsBackToIctuName()
        {
            // Act
            var result = _service.EstimateTravelTime(21.5870, 105.8080, destinationName: "   ");

            // Assert
            Assert.That(result.DestinationName, Is.EqualTo(Constants.IctuCampusName));
        }

        #endregion

        #region AC1: Motorbike, Bus, and Walk Travel Time Estimates

        [Test]
        public void EstimateTravelTime_ProducesPositiveEstimatesForMotorbikeBusAndWalk()
        {
            // Arrange: Room located 1.5 km from ICTU
            double roomLat = 21.5950;
            double roomLon = 105.8150;

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon);

            // Assert (AC1)
            Assert.That(result.DistanceKm, Is.GreaterThan(0.0));
            Assert.That(result.MotorbikeMinutes, Is.GreaterThan(0));
            Assert.That(result.WalkMinutes, Is.GreaterThan(0));
            Assert.That(result.BusMinutes, Is.GreaterThan(0));

            // Realistic hierarchy: Motorbike should be faster than walking
            Assert.That(result.MotorbikeMinutes, Is.LessThan(result.WalkMinutes));
        }

        [Test]
        public void EstimateTravelTime_ShortDistance_BusMinutesEqualsWalkMinutes()
        {
            // Arrange: Very short distance (< 600m), walking is faster than waiting 8 mins for bus
            double roomLat = 21.5860;
            double roomLon = 105.8070;

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon);

            // Assert: For < 600m, bus time adapts to walk time
            Assert.That(result.DistanceKm, Is.LessThan(0.6));
            Assert.That(result.BusMinutes, Is.EqualTo(result.WalkMinutes));
        }

        [Test]
        public void EstimateTravelTime_MediumDistance_BusIncludesWaitTime()
        {
            // Arrange: Distance ~ 3 km from ICTU
            double roomLat = 21.6100;
            double roomLon = 105.8300;

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon);

            // Assert: Bus time should be at least wait time (8 mins) and faster than walking
            Assert.That(result.DistanceKm, Is.GreaterThan(2.0));
            Assert.That(result.BusMinutes, Is.GreaterThanOrEqualTo(8));
            Assert.That(result.BusMinutes, Is.LessThan(result.WalkMinutes));
        }

        [Test]
        public void PopulateListingEstimates_DirectlyUpdatesAllFourFieldsOnListing()
        {
            // Arrange
            var listing = new RoomListing
            {
                Id = 1,
                Title = "Phòng trọ gần cổng phụ ICTU",
                Latitude = 21.5870,
                Longitude = 105.8080,
                DistanceToCampusKm = 0,
                MotorbikeMinutes = 0,
                BusMinutes = 0,
                WalkMinutes = 0
            };

            // Act
            _service.PopulateListingEstimates(listing);

            // Assert (AC1 & AC2)
            Assert.That(listing.DistanceToCampusKm, Is.GreaterThan(0.0));
            Assert.That(listing.MotorbikeMinutes, Is.GreaterThan(0));
            Assert.That(listing.WalkMinutes, Is.GreaterThan(0));
            Assert.That(listing.BusMinutes, Is.GreaterThan(0));
        }

        [Test]
        public void PopulateListingEstimates_NullListing_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.PopulateListingEstimates(null!));
        }

        [Test]
        public void PopulateListingsEstimates_BatchUpdatesEntireCollection()
        {
            // Arrange
            var listings = new List<RoomListing>
            {
                new() { Id = 1, Latitude = 21.5870, Longitude = 105.8080 },
                new() { Id = 2, Latitude = 21.5950, Longitude = 105.8150 },
                new() { Id = 3, Latitude = 21.6050, Longitude = 105.8250 }
            };

            // Act
            _service.PopulateListingsEstimates(listings);

            // Assert: All listings received calculated estimates
            foreach (var item in listings)
            {
                Assert.That(item.DistanceToCampusKm, Is.GreaterThan(0.0));
                Assert.That(item.MotorbikeMinutes, Is.GreaterThan(0));
                Assert.That(item.WalkMinutes, Is.GreaterThan(0));
                Assert.That(item.BusMinutes, Is.GreaterThan(0));
            }

            // Distant listing should have larger distance and travel time
            Assert.That(listings[2].DistanceToCampusKm, Is.GreaterThan(listings[0].DistanceToCampusKm));
            Assert.That(listings[2].WalkMinutes, Is.GreaterThan(listings[0].WalkMinutes));
        }

        [Test]
        public void PopulateListingsEstimates_NullCollection_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.PopulateListingsEstimates(null!));
        }

        #endregion

        #region AC3: Straight-Line Fallback Distance & Haversine Formula

        [Test]
        public void EstimateTravelTime_SetsIsFallbackToTrue_AndProvidesRouteSummary()
        {
            // Act
            var result = _service.EstimateTravelTime(21.5890, 105.8100);

            // Assert (AC3)
            Assert.That(result.IsFallback, Is.True);
            Assert.That(result.RouteSummary, Does.Contain(Constants.IctuCampusName));
            Assert.That(result.RouteSummary, Does.Contain("Ước lượng dự phòng"));
        }

        [Test]
        public void CalculateStraightLineDistanceKm_IdenticalCoordinates_ReturnsZero()
        {
            // Act
            var dist = _service.CalculateStraightLineDistanceKm(
                Constants.IctuLatitude, Constants.IctuLongitude,
                Constants.IctuLatitude, Constants.IctuLongitude);

            // Assert
            Assert.That(dist, Is.EqualTo(0.0));
        }

        [Test]
        public void CalculateStraightLineDistanceKm_KnownDistance_MatchesHaversineBenchmark()
        {
            // Hanoi Hoan Kiem (21.0285, 105.8542) to ICTU Thai Nguyen (21.5850, 105.8064)
            // Real straight-line distance is ~ 62 km
            double hanoiLat = 21.0285;
            double hanoiLon = 105.8542;

            // Act
            var dist = _service.CalculateStraightLineDistanceKm(
                hanoiLat, hanoiLon,
                Constants.IctuLatitude, Constants.IctuLongitude);

            // Assert: Between 60 km and 65 km
            Assert.That(dist, Is.InRange(60.0, 65.0));
        }

        [Test]
        public void CalculateStraightLineDistanceKm_IsSymmetric()
        {
            double lat1 = 21.5850, lon1 = 105.8064;
            double lat2 = 21.6000, lon2 = 105.8200;

            var distAB = _service.CalculateStraightLineDistanceKm(lat1, lon1, lat2, lon2);
            var distBA = _service.CalculateStraightLineDistanceKm(lat2, lon2, lat1, lon1);

            Assert.That(distAB, Is.EqualTo(distBA));
        }

        [Test]
        public void CalculateStraightLineDistanceKm_SatisfiesTriangleInequality()
        {
            // Point A (ICTU), Point B (Thai Nguyen Center), Point C (Song Cong)
            double aLat = 21.5850, aLon = 105.8064;
            double bLat = 21.5950, bLon = 105.8450;
            double cLat = 21.4850, cLon = 105.8150;

            var dAC = _service.CalculateStraightLineDistanceKm(aLat, aLon, cLat, cLon);
            var dAB = _service.CalculateStraightLineDistanceKm(aLat, aLon, bLat, bLon);
            var dBC = _service.CalculateStraightLineDistanceKm(bLat, bLon, cLat, cLon);

            // Triangle inequality: d(A, C) <= d(A, B) + d(B, C)
            Assert.That(dAC, Is.LessThanOrEqualTo(dAB + dBC + 0.05));
        }

        #endregion

        #region Edge Cases & Asynchronous API

        [Test]
        public void EstimateTravelTime_ExactDestinationCoordinates_ReturnsZeroMinutes()
        {
            // Arrange: Room coordinate exactly matches ICTU Campus
            double roomLat = Constants.IctuLatitude;
            double roomLon = Constants.IctuLongitude;

            // Act
            var result = _service.EstimateTravelTime(roomLat, roomLon);

            // Assert
            Assert.That(result.DistanceKm, Is.EqualTo(0.0));
            Assert.That(result.MotorbikeMinutes, Is.EqualTo(0));
            Assert.That(result.BusMinutes, Is.EqualTo(0));
            Assert.That(result.WalkMinutes, Is.EqualTo(0));
            Assert.That(result.RouteSummary, Does.Contain("Tại điểm đến"));
        }

        [Test]
        public void EstimateTravelTime_ClampsExtremeCoordinates_DoesNotThrow()
        {
            // Arrange: Latitude > 90, Longitude < -180
            double extremeLat = 150.0;
            double extremeLon = -200.0;

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = _service.EstimateTravelTime(extremeLat, extremeLon);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public async Task EstimateTravelTimeAsync_ReturnsExpectedEstimate()
        {
            // Arrange
            double roomLat = 21.5900;
            double roomLon = 105.8100;

            // Act
            var result = await _service.EstimateTravelTimeAsync(roomLat, roomLon);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DistanceKm, Is.GreaterThan(0.0));
            Assert.That(result.MotorbikeMinutes, Is.GreaterThan(0));
        }

        [Test]
        public void EstimateTravelTimeAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _service.EstimateTravelTimeAsync(21.5850, 105.8064, cancellationToken: cts.Token);
            });
        }

        #endregion

        #region DI Registration & Architectural Compliance

        [Test]
        public void DependencyInjection_ResolvesITravelTimeServiceFromInfrastructureModule()
        {
            // Arrange
            var tempDb = Path.Combine(Path.GetTempPath(), $"travel_di_{Guid.NewGuid():N}.db3");
            var services = new ServiceCollection();
            services.AddRentalInfrastructure($"Data Source={tempDb}");

            using var provider = services.BuildServiceProvider();

            // Act
            var resolvedService = provider.GetService<ITravelTimeService>();

            // Assert
            Assert.That(resolvedService, Is.Not.Null);
            Assert.That(resolvedService, Is.InstanceOf<TravelTimeService>());

            // Clean up
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }

        #endregion
    }
}
