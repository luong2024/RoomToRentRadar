using System.Text;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Configuration options for generating comparison matrix and image export.
    /// </summary>
    public class ComparisonExportOptions
    {
        public string Title { get; set; } = "Bảng So Sánh Phòng Trọ — RoomToRentRadar";
        public bool HighlightBestValues { get; set; } = true;
        public bool IncludeHouseRules { get; set; } = true;
        public bool IncludeTravelTimes { get; set; } = true;
        public string PreferredImageFormat { get; set; } = "svg"; // svg or png
    }

    /// <summary>
    /// Represents formatted column data for a single room in the comparison table (AC1 &amp; AC2).
    /// </summary>
    public class ComparisonItemColumn
    {
        public int RoomId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public string FormattedRent { get; set; } = string.Empty;
        public decimal Deposit { get; set; }
        public string FormattedDeposit { get; set; } = string.Empty;
        public double Area { get; set; }
        public string FormattedArea { get; set; } = string.Empty;
        public double DistanceToCampusKm { get; set; }
        public string FormattedDistance { get; set; } = string.Empty;
        public int MotorbikeMinutes { get; set; }
        public int BusMinutes { get; set; }
        public int WalkMinutes { get; set; }
        public string FormattedTravelTime { get; set; } = string.Empty;
        public double SecurityScore { get; set; }
        public string FormattedSecurityScore { get; set; } = string.Empty;
        public IReadOnlyList<string> Amenities { get; set; } = Array.Empty<string>();
        public string Address { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string QuietHours { get; set; } = string.Empty;
        public string CookingRules { get; set; } = string.Empty;
        public string GuestPolicy { get; set; } = string.Empty;
        public string ParkingRules { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a single metric comparison row comparing up to 3 rooms side by side (AC2).
    /// </summary>
    public class ComparisonMetricRow
    {
        public string MetricName { get; set; } = string.Empty;
        public string Category { get; set; } = "Thông tin chung";
        public IReadOnlyList<string> Values { get; set; } = Array.Empty<string>();
        public int BestColumnIndex { get; set; } = -1; // Index of the best room for this metric
        public string HighlightNote { get; set; } = string.Empty;
    }

    /// <summary>
    /// Matrix model representing the side-by-side comparison of up to 3 rooms (AC1 &amp; AC2).
    /// Ready for direct data binding in MAUI UI or image generation.
    /// </summary>
    public class ComparisonMatrixModel
    {
        public string Title { get; set; } = "So sánh phòng trọ";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int RoomCount => Columns.Count;
        public bool IsEmpty => Columns.Count == 0;
        public IReadOnlyList<ComparisonItemColumn> Columns { get; set; } = Array.Empty<ComparisonItemColumn>();
        public IReadOnlyList<ComparisonMetricRow> Rows { get; set; } = Array.Empty<ComparisonMetricRow>();
    }

    /// <summary>
    /// Payload containing exported comparison table image and share metadata (AC3).
    /// </summary>
    public class ComparisonSharePayload
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/svg+xml";
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string Base64DataUri { get; set; } = string.Empty;
        public string ShareTextSummary { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Contract for US11 side-by-side room comparison matrix and share-as-image capability (T11.2).
    /// Acceptance Criteria:
    ///   AC1: Users can add up to 3 rooms to the comparison table.
    ///   AC2: The table compares rent, deposit, area, distance to ICTU, amenities, and security.
    ///   AC3: The comparison table can be shared as an image.
    /// </summary>
    public interface IComparisonShareService
    {
        ComparisonMatrixModel BuildComparisonMatrix(IReadOnlyList<RoomListing> rooms);

        ComparisonSharePayload GenerateComparisonImage(
            IReadOnlyList<RoomListing> rooms,
            ComparisonExportOptions? options = null);

        Task<ComparisonSharePayload> GenerateComparisonImageAsync(
            IReadOnlyList<RoomListing> rooms,
            ComparisonExportOptions? options = null,
            CancellationToken cancellationToken = default);

        string GenerateComparisonTextSummary(IReadOnlyList<RoomListing> rooms);
    }

    /// <summary>
    /// TASK OWNER: T11.2 (US11 — Test comparison table and share comparison view).
    /// Provides comparison matrix calculation and share-as-image generation in pure .NET 10.
    /// </summary>
    public class ComparisonShareService : IComparisonShareService
    {
        public const int MaxComparisonRooms = ComparisonBasketService.MaxComparisonRooms; // 3 rooms

        /// <summary>
        /// AC1 &amp; AC2: Builds side-by-side comparison matrix for up to 3 rooms.
        /// </summary>
        public virtual ComparisonMatrixModel BuildComparisonMatrix(IReadOnlyList<RoomListing> rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return new ComparisonMatrixModel();
            }

            // AC1: Limit to maximum 3 distinct rooms
            var selectedRooms = rooms
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .Take(MaxComparisonRooms)
                .ToList();

            var columns = selectedRooms.Select(MapToColumn).ToList();
            var rows = new List<ComparisonMetricRow>();

            // Baseline rows (to be fully fleshed out in Phase 2)
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Giá thuê",
                Category = "Chi phí",
                Values = columns.Select(c => c.FormattedRent).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Tiền cọc",
                Category = "Chi phí",
                Values = columns.Select(c => c.FormattedDeposit).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Diện tích",
                Category = "Quy mô",
                Values = columns.Select(c => c.FormattedArea).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Khoảng cách tới ICTU",
                Category = "Vị trí",
                Values = columns.Select(c => c.FormattedDistance).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Điểm an ninh",
                Category = "An ninh",
                Values = columns.Select(c => c.FormattedSecurityScore).ToList()
            });

            return new ComparisonMatrixModel
            {
                Title = "Bảng so sánh phòng trọ",
                GeneratedAt = DateTime.UtcNow,
                Columns = columns,
                Rows = rows
            };
        }

        /// <summary>
        /// AC3: Generates image payload for sharing comparison table.
        /// </summary>
        public virtual ComparisonSharePayload GenerateComparisonImage(
            IReadOnlyList<RoomListing> rooms,
            ComparisonExportOptions? options = null)
        {
            var matrix = BuildComparisonMatrix(rooms);
            var opt = options ?? new ComparisonExportOptions();

            var summaryText = GenerateComparisonTextSummary(rooms);

            // Baseline SVG image for Phase 1
            var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""800"" height=""600"" viewBox=""0 0 800 600"">
  <rect width=""100%"" height=""100%"" fill=""#F8FAFC""/>
  <text x=""40"" y=""50"" font-family=""sans-serif"" font-size=""22"" font-weight=""bold"" fill=""#1E293B"">{opt.Title}</text>
  <text x=""40"" y=""80"" font-family=""sans-serif"" font-size=""14"" fill=""#64748B"">Số phòng so sánh: {matrix.RoomCount} | Ngày tạo: {DateTime.UtcNow:dd/MM/yyyy HH:mm}</text>
</svg>";

            var bytes = Encoding.UTF8.GetBytes(svg);
            var base64 = Convert.ToBase64String(bytes);

            return new ComparisonSharePayload
            {
                FileName = $"so-sanh-phong-tro-ictu-{DateTime.UtcNow:yyyyMMdd-HHmmss}.svg",
                ContentType = "image/svg+xml",
                ImageBytes = bytes,
                Base64DataUri = $"data:image/svg+xml;base64,{base64}",
                ShareTextSummary = summaryText,
                GeneratedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Asynchronous image generation overload.
        /// </summary>
        public virtual Task<ComparisonSharePayload> GenerateComparisonImageAsync(
            IReadOnlyList<RoomListing> rooms,
            ComparisonExportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = GenerateComparisonImage(rooms, options);
            return Task.FromResult(payload);
        }

        /// <summary>
        /// Generates human-readable text summary of comparison for clipboard or messaging.
        /// </summary>
        public virtual string GenerateComparisonTextSummary(IReadOnlyList<RoomListing> rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return "Chưa có phòng nào được chọn để so sánh.";
            }

            var selected = rooms.Take(MaxComparisonRooms).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("=== SO SÁNH PHÒNG TRỌ (ICTU) ===");

            for (int i = 0; i < selected.Count; i++)
            {
                var r = selected[i];
                sb.AppendLine($"[{i + 1}] {r.Title}");
                sb.AppendLine($"   - Giá thuê: {r.MonthlyRent:N0} đ/tháng | Cọc: {r.Deposit:N0} đ");
                sb.AppendLine($"   - Diện tích: {r.Area} m² | Cách ICTU: {r.DistanceToCampusKm:F1} km");
                sb.AppendLine($"   - An ninh: {r.SecurityScore:F1}/5.0 ★ | Tiện ích: {r.AmenitiesCsv}");
            }

            return sb.ToString();
        }

        protected virtual ComparisonItemColumn MapToColumn(RoomListing listing)
        {
            var amenities = (listing.AmenitiesCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            return new ComparisonItemColumn
            {
                RoomId = listing.Id,
                Title = listing.Title,
                MonthlyRent = listing.MonthlyRent,
                FormattedRent = $"{listing.MonthlyRent:N0} đ/tháng",
                Deposit = listing.Deposit,
                FormattedDeposit = $"{listing.Deposit:N0} đ",
                Area = listing.Area,
                FormattedArea = $"{listing.Area} m²",
                DistanceToCampusKm = listing.DistanceToCampusKm,
                FormattedDistance = $"{listing.DistanceToCampusKm:F1} km tới ICTU",
                MotorbikeMinutes = listing.MotorbikeMinutes,
                BusMinutes = listing.BusMinutes,
                WalkMinutes = listing.WalkMinutes,
                FormattedTravelTime = $"{listing.MotorbikeMinutes} ph xe máy / {listing.WalkMinutes} ph đi bộ",
                SecurityScore = listing.SecurityScore,
                FormattedSecurityScore = $"{listing.SecurityScore:F1} / 5.0 ★",
                Amenities = amenities,
                Address = listing.Address,
                Ward = listing.Ward,
                RoomType = listing.RoomType,
                QuietHours = listing.QuietHours,
                CookingRules = listing.CookingRules,
                GuestPolicy = listing.GuestPolicy,
                ParkingRules = listing.ParkingRules,
                ThumbnailUrl = listing.ThumbnailUrl
            };
        }
    }
}
