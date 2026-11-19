using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using System.Text;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive Unit Test suite for US11 — Side-by-Side Room Comparison &amp; Share View (T11.2).
    /// Tests strictly enforce all Acceptance Criteria:
    ///   AC1: Users can add up to 3 rooms to the comparison table.
    ///   AC2: The table compares rent, deposit, area, distance to ICTU, amenities, and security.
    ///   AC3: The comparison table can be shared as an image.
    /// </summary>
    [CancelAfter(5000)]
    [TestFixture]
    public class RoomComparisonTests
    {
        private ComparisonBasketService _basket = null!;
        private IComparisonShareService _shareService = null!;

        [SetUp]
        public void SetUp()
        {
            _basket = new ComparisonBasketService();
            _shareService = new ComparisonShareService();
        }

        #region Helper Methods

        private static RoomListing CreateSampleRoom(
            int id,
            string title,
            decimal rent,
            decimal deposit,
            double area,
            double distanceKm,
            double securityScore,
            string amenitiesCsv,
            int motorbikeMins = 3,
            int walkMins = 10)
        {
            return new RoomListing
            {
                Id = id,
                Title = title,
                MonthlyRent = rent,
                Deposit = deposit,
                Area = area,
                DistanceToCampusKm = distanceKm,
                SecurityScore = securityScore,
                AmenitiesCsv = amenitiesCsv,
                MotorbikeMinutes = motorbikeMins,
                WalkMinutes = walkMins,
                Address = "Đường Z115, Xã Quyết Thắng",
                Ward = "Quyết Thắng",
                RoomType = "Phòng khép kín",
                QuietHours = "22:30 - 05:30",
                CookingRules = "Được nấu ăn bếp từ"
            };
        }

        #endregion

        #region AC1: Add up to 3 rooms to Comparison Table

        [Test]
        public void TryAddRoom_AllowsAddingUpToThreeRooms()
        {
            // Arrange
            var room1 = CreateSampleRoom(1, "Phòng A", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi,Điều hòa");
            var room2 = CreateSampleRoom(2, "Phòng B", 2500000m, 2500000m, 25, 1.2, 4.8, "WiFi,Điều hòa,Nóng lạnh");
            var room3 = CreateSampleRoom(3, "Phòng C", 1800000m, 1800000m, 18, 0.8, 4.2, "WiFi,Nóng lạnh");

            // Act
            bool added1 = _basket.TryAddRoom(room1, out string msg1);
            bool added2 = _basket.TryAddRoom(room2, out string msg2);
            bool added3 = _basket.TryAddRoom(room3, out string msg3);

            // Assert (AC1)
            Assert.That(added1, Is.True);
            Assert.That(added2, Is.True);
            Assert.That(added3, Is.True);
            Assert.That(_basket.SelectedRooms, Has.Count.EqualTo(3));
            Assert.That(msg3, Does.Contain("3/3"));
        }

        [Test]
        public void TryAddRoom_FourthRoom_IsRejectedWithErrorMessage()
        {
            // Arrange: Add 3 rooms first
            _basket.TryAddRoom(CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi"), out _);
            _basket.TryAddRoom(CreateSampleRoom(2, "Phòng 2", 2200000m, 2000000m, 22, 0.8, 4.6, "WiFi"), out _);
            _basket.TryAddRoom(CreateSampleRoom(3, "Phòng 3", 2400000m, 2000000m, 24, 1.0, 4.7, "WiFi"), out _);

            // Act: Attempt to add 4th room
            var room4 = CreateSampleRoom(4, "Phòng 4", 3000000m, 3000000m, 30, 2.0, 4.9, "WiFi");
            bool added4 = _basket.TryAddRoom(room4, out string errorMsg);

            // Assert (AC1)
            Assert.That(added4, Is.False);
            Assert.That(_basket.SelectedRooms, Has.Count.EqualTo(3));
            Assert.That(errorMsg, Does.Contain("tối đa 3 phòng"));
        }

        [Test]
        public void TryAddRoom_DuplicateRoomId_IsRejected()
        {
            // Arrange
            var room = CreateSampleRoom(101, "Phòng Trọ VIP", 2500000m, 2500000m, 25, 0.6, 4.8, "WiFi,Điều hòa");
            _basket.TryAddRoom(room, out _);

            // Act: Try adding same room again
            bool addedDuplicate = _basket.TryAddRoom(room, out string msg);

            // Assert
            Assert.That(addedDuplicate, Is.False);
            Assert.That(_basket.SelectedRooms, Has.Count.EqualTo(1));
            Assert.That(msg, Does.Contain("đã có trong danh sách so sánh"));
        }

        [Test]
        public void RemoveRoom_RemovesSpecificRoomFromBasket()
        {
            // Arrange
            var room1 = CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi");
            var room2 = CreateSampleRoom(2, "Phòng 2", 2200000m, 2000000m, 22, 0.8, 4.6, "WiFi");
            _basket.TryAddRoom(room1, out _);
            _basket.TryAddRoom(room2, out _);

            // Act
            _basket.RemoveRoom(1);

            // Assert
            Assert.That(_basket.SelectedRooms, Has.Count.EqualTo(1));
            Assert.That(_basket.SelectedRooms[0].Id, Is.EqualTo(2));
        }

        [Test]
        public void Clear_EmptiesAllRoomsInBasket()
        {
            // Arrange
            _basket.TryAddRoom(CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi"), out _);
            _basket.TryAddRoom(CreateSampleRoom(2, "Phòng 2", 2200000m, 2000000m, 22, 0.8, 4.6, "WiFi"), out _);

            // Act
            _basket.Clear();

            // Assert
            Assert.That(_basket.SelectedRooms, Is.Empty);
        }

        [Test]
        public void BuildComparisonMatrix_LimitsInputToMaximumThreeDistinctRooms()
        {
            // Arrange: 5 rooms input
            var rooms = new List<RoomListing>
            {
                CreateSampleRoom(1, "P1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi"),
                CreateSampleRoom(2, "P2", 2200000m, 2000000m, 22, 0.8, 4.6, "WiFi"),
                CreateSampleRoom(3, "P3", 2400000m, 2000000m, 24, 1.0, 4.7, "WiFi"),
                CreateSampleRoom(4, "P4", 2600000m, 2000000m, 26, 1.2, 4.8, "WiFi"),
                CreateSampleRoom(1, "P1 Trùng", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi")
            };

            // Act
            var matrix = _shareService.BuildComparisonMatrix(rooms);

            // Assert (AC1)
            Assert.That(matrix.RoomCount, Is.EqualTo(3));
            Assert.That(matrix.Columns, Has.Count.EqualTo(3));
            Assert.That(matrix.Columns.Select(c => c.RoomId), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        #endregion

        #region AC2: Side-by-Side Comparison of Rent, Deposit, Area, Distance, Amenities & Security

        [Test]
        public void BuildComparisonMatrix_IncludesAllSixRequiredMetrics()
        {
            // Arrange
            var rooms = new List<RoomListing>
            {
                CreateSampleRoom(1, "Phòng A", 2000000m, 1500000m, 18, 0.5, 4.5, "WiFi,Điều hòa"),
                CreateSampleRoom(2, "Phòng B", 2800000m, 2500000m, 28, 1.5, 4.9, "WiFi,Điều hòa,Nóng lạnh,Máy giặt")
            };

            // Act
            var matrix = _shareService.BuildComparisonMatrix(rooms);

            // Assert (AC2: Rent, Deposit, Area, Distance to ICTU, Amenities, Security)
            var metricNames = matrix.Rows.Select(r => r.MetricName).ToList();

            Assert.That(metricNames, Does.Contain("Giá thuê"));
            Assert.That(metricNames, Does.Contain("Tiền đặt cọc"));
            Assert.That(metricNames, Does.Contain("Diện tích"));
            Assert.That(metricNames, Does.Contain("Khoảng cách tới ICTU"));
            Assert.That(metricNames, Does.Contain("Điểm an ninh"));
            Assert.That(metricNames, Does.Contain("Số lượng tiện ích"));
        }

        [Test]
        public void BuildComparisonMatrix_HighlightsBestValueForRentDepositAreaAndDistance()
        {
            // Arrange: Room 1 cheapest & closest; Room 2 biggest & safest
            var room1 = CreateSampleRoom(1, "Phòng Rẻ Gần", 1800000m, 1500000m, 18, 0.4, 4.2, "WiFi");
            var room2 = CreateSampleRoom(2, "Phòng Rộng An Ninh", 3200000m, 3000000m, 32, 1.8, 4.9, "WiFi,Điều hòa,Nóng lạnh,Máy giặt,Tủ lạnh");

            // Act
            var matrix = _shareService.BuildComparisonMatrix(new[] { room1, room2 });

            // Assert
            var rentRow = matrix.Rows.First(r => r.MetricName == "Giá thuê");
            Assert.That(rentRow.BestColumnIndex, Is.EqualTo(0)); // Room 1
            Assert.That(rentRow.HighlightNote, Is.EqualTo("Giá tốt nhất"));

            var areaRow = matrix.Rows.First(r => r.MetricName == "Diện tích");
            Assert.That(areaRow.BestColumnIndex, Is.EqualTo(1)); // Room 2
            Assert.That(areaRow.HighlightNote, Is.EqualTo("Rộng rãi nhất"));

            var distRow = matrix.Rows.First(r => r.MetricName == "Khoảng cách tới ICTU");
            Assert.That(distRow.BestColumnIndex, Is.EqualTo(0)); // Room 1
            Assert.That(distRow.HighlightNote, Is.EqualTo("Gần trường nhất"));

            var secRow = matrix.Rows.First(r => r.MetricName == "Điểm an ninh");
            Assert.That(secRow.BestColumnIndex, Is.EqualTo(1)); // Room 2
            Assert.That(secRow.HighlightNote, Is.EqualTo("An ninh tốt nhất"));

            Assert.That(matrix.BestValueRoomId, Is.EqualTo(1));
            Assert.That(matrix.ClosestToCampusRoomId, Is.EqualTo(1));
        }

        [Test]
        public void BuildComparisonMatrix_GeneratesDetailedAmenitiesChecklist()
        {
            // Arrange
            var room1 = CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "Điều hòa,WiFi");
            var room2 = CreateSampleRoom(2, "Phòng 2", 2500000m, 2000000m, 25, 1.0, 4.6, "Nóng lạnh,WiFi,Máy giặt");

            // Act
            var matrix = _shareService.BuildComparisonMatrix(new[] { room1, room2 });

            // Assert (AC2: Amenities checklist)
            var acRow = matrix.Rows.FirstOrDefault(r => r.MetricName == "Tiện ích: Điều hòa");
            Assert.That(acRow, Is.Not.Null);
            Assert.That(acRow!.Values[0], Does.Contain("✓ Có"));
            Assert.That(acRow.Values[1], Does.Contain("Chưa có"));

            var washerRow = matrix.Rows.FirstOrDefault(r => r.MetricName == "Tiện ích: Máy giặt");
            Assert.That(washerRow, Is.Not.Null);
            Assert.That(washerRow!.Values[0], Does.Contain("Chưa có"));
            Assert.That(washerRow.Values[1], Does.Contain("✓ Có"));
        }

        [Test]
        public void BuildComparisonMatrix_WhenTied_DoesNotSingleOutBestColumn()
        {
            // Arrange: Both rooms have identical rent
            var room1 = CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi");
            var room2 = CreateSampleRoom(2, "Phòng 2", 2000000m, 2000000m, 22, 0.8, 4.6, "WiFi");

            // Act
            var matrix = _shareService.BuildComparisonMatrix(new[] { room1, room2 });

            // Assert: Tied rent has no single BestColumnIndex (-1)
            var rentRow = matrix.Rows.First(r => r.MetricName == "Giá thuê");
            Assert.That(rentRow.BestColumnIndex, Is.EqualTo(-1));
        }

        [Test]
        public void GetDifferencesOnly_FiltersOutMetricsWithIdenticalValues()
        {
            // Arrange: Both rooms have same rent and ward, but different area
            var room1 = CreateSampleRoom(1, "Phòng 1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi");
            var room2 = CreateSampleRoom(2, "Phòng 2", 2000000m, 2000000m, 30, 1.5, 4.5, "WiFi");

            // Act
            var matrix = _shareService.BuildComparisonMatrix(new[] { room1, room2 });
            var diffRows = matrix.GetDifferencesOnly();

            // Assert
            Assert.That(diffRows.Any(r => r.MetricName == "Diện tích"), Is.True);
            Assert.That(diffRows.Any(r => r.MetricName == "Khoảng cách tới ICTU"), Is.True);
            Assert.That(diffRows.Any(r => r.MetricName == "Giá thuê"), Is.False); // Equal -> filtered out
        }

        [Test]
        public void BuildComparisonMatrix_EmptyInput_ReturnsEmptyMatrixWithoutException()
        {
            // Act
            var matrix = _shareService.BuildComparisonMatrix(Array.Empty<RoomListing>());

            // Assert
            Assert.That(matrix.IsEmpty, Is.True);
            Assert.That(matrix.RoomCount, Is.EqualTo(0));
            Assert.That(matrix.Columns, Is.Empty);
            Assert.That(matrix.Rows, Is.Empty);
        }

        #endregion

        #region AC3: Share Comparison Table as Image

        [Test]
        public void GenerateComparisonImage_ProducesValidSvgImagePayload()
        {
            // Arrange
            var rooms = new List<RoomListing>
            {
                CreateSampleRoom(1, "Phòng A Cổng Phụ ICTU", 2100000m, 2000000m, 20, 0.4, 4.7, "WiFi,Điều hòa"),
                CreateSampleRoom(2, "Chung Cư Mini Tân Thịnh", 3200000m, 3000000m, 35, 1.5, 4.9, "WiFi,Điều hòa,Nóng lạnh,Máy giặt")
            };

            // Act (AC3)
            var payload = _shareService.GenerateComparisonImage(rooms);

            // Assert
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.ContentType, Is.EqualTo("image/svg+xml"));
            Assert.That(payload.FileName, Does.StartWith("so-sanh-phong-tro-ictu-"));
            Assert.That(payload.FileName, Does.EndWith(".svg"));
            Assert.That(payload.ImageBytes, Is.Not.Empty);
            Assert.That(payload.ImageBytes.Length, Is.GreaterThan(500));
            Assert.That(payload.Base64DataUri, Does.StartWith("data:image/svg+xml;base64,"));
        }

        [Test]
        public void GenerateComparisonImage_ContainsBrandingRoomTitlesAndMetrics()
        {
            // Arrange
            var room1 = CreateSampleRoom(1, "Nhà trọ Thảo Nguyên ICTU", 2200000m, 2000000m, 22, 0.5, 4.8, "Điều hòa,WiFi");
            var room2 = CreateSampleRoom(2, "Ký túc xá tư nhân Quyết Thắng", 1500000m, 1500000m, 16, 0.8, 4.2, "WiFi");

            // Act
            var payload = _shareService.GenerateComparisonImage(new[] { room1, room2 });
            var svgString = Encoding.UTF8.GetString(payload.ImageBytes);

            // Assert
            Assert.That(svgString, Does.StartWith("<svg"));
            Assert.That(svgString, Does.EndWith("</svg>" + Environment.NewLine).Or.EndsWith("</svg>"));
            Assert.That(svgString, Does.Contain("ICTU"));
            Assert.That(svgString, Does.Contain("2,200,000 đ/tháng"));
            Assert.That(svgString, Does.Contain("1,500,000 đ/tháng"));
            Assert.That(svgString, Does.Contain("RoomToRentRadar"));
        }

        [Test]
        public async Task GenerateComparisonImageAsync_ReturnsExpectedPayload()
        {
            // Arrange
            var rooms = new[] { CreateSampleRoom(1, "Phòng Test", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi") };

            // Act
            var payload = await _shareService.GenerateComparisonImageAsync(rooms);

            // Assert
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.ImageBytes.Length, Is.GreaterThan(100));
        }

        [Test]
        public void GenerateComparisonImageAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _shareService.GenerateComparisonImageAsync(
                    new[] { CreateSampleRoom(1, "P1", 2000000m, 2000000m, 20, 0.5, 4.5, "WiFi") },
                    cancellationToken: cts.Token);
            });
        }

        [Test]
        public void GenerateComparisonTextSummary_ProducesStructuredText()
        {
            // Arrange
            var room = CreateSampleRoom(1, "Phòng Trọ Tiện Nghi", 2300000m, 2000000m, 22, 0.6, 4.6, "WiFi,Điều hòa");

            // Act
            var summary = _shareService.GenerateComparisonTextSummary(new[] { room });

            // Assert
            Assert.That(summary, Does.Contain("SO SÁNH PHÒNG TRỌ (ICTU)"));
            Assert.That(summary, Does.Contain("Phòng Trọ Tiện Nghi"));
            Assert.That(summary, Does.Contain("2,300,000"));
            Assert.That(summary, Does.Contain("0.6 km"));
        }

        [Test]
        public void GenerateComparisonImage_EmptyRooms_GeneratesValidEmptyStateSvg()
        {
            // Act
            var payload = _shareService.GenerateComparisonImage(Array.Empty<RoomListing>());
            var svgString = Encoding.UTF8.GetString(payload.ImageBytes);

            // Assert
            Assert.That(payload.ImageBytes, Is.Not.Empty);
            Assert.That(svgString, Does.Contain("Chưa có phòng nào được chọn để so sánh"));
        }

        #endregion

        #region Dependency Injection & Architectural Verification

        [Test]
        public void DependencyInjection_ResolvesComparisonServicesFromInfrastructureModule()
        {
            // Arrange
            var tempDb = Path.Combine(Path.GetTempPath(), $"cmp_di_{Guid.NewGuid():N}.db3");
            var services = new ServiceCollection();
            services.AddRentalInfrastructure($"Data Source={tempDb}");

            using var provider = services.BuildServiceProvider();

            // Act
            var basket = provider.GetService<ComparisonBasketService>();
            var share = provider.GetService<IComparisonShareService>();

            // Assert
            Assert.That(basket, Is.Not.Null);
            Assert.That(share, Is.Not.Null);
            Assert.That(share, Is.InstanceOf<ComparisonShareService>());

            // Cleanup
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }

        #endregion
    }
}
