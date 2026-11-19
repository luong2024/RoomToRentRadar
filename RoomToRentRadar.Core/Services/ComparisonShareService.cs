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
        public string Title { get; set; } = "Bảng so sánh phòng trọ";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int RoomCount => Columns.Count;
        public bool IsEmpty => Columns.Count == 0;
        public int BestValueRoomId { get; set; }
        public int ClosestToCampusRoomId { get; set; }
        public string OverallVerdict { get; set; } = string.Empty;
        public IReadOnlyList<ComparisonItemColumn> Columns { get; set; } = Array.Empty<ComparisonItemColumn>();
        public IReadOnlyList<ComparisonMetricRow> Rows { get; set; } = Array.Empty<ComparisonMetricRow>();

        /// <summary>
        /// Filters rows to show only metrics where values differ among the compared rooms.
        /// </summary>
        public IReadOnlyList<ComparisonMetricRow> GetDifferencesOnly()
        {
            if (Columns.Count <= 1) return Rows;
            return Rows.Where(r => r.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1).ToList();
        }
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
        /// Compares rent, deposit, area, distance to ICTU, amenities, security, and house rules.
        /// </summary>
        public virtual ComparisonMatrixModel BuildComparisonMatrix(IReadOnlyList<RoomListing> rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return new ComparisonMatrixModel();
            }

            // AC1: Limit to maximum 3 distinct rooms
            var selectedRooms = rooms
                .Where(r => r != null)
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .Take(MaxComparisonRooms)
                .ToList();

            if (selectedRooms.Count == 0)
            {
                return new ComparisonMatrixModel();
            }

            var columns = selectedRooms.Select(MapToColumn).ToList();
            var rows = new List<ComparisonMetricRow>();

            // AC2: 1. Chi phí (Rent & Deposit)
            var minRent = columns.Min(c => c.MonthlyRent);
            var bestRentIndices = columns.Select((c, idx) => c.MonthlyRent == minRent ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Giá thuê",
                Category = "Chi phí",
                Values = columns.Select(c => c.FormattedRent).ToList(),
                BestColumnIndex = bestRentIndices.Count == 1 ? bestRentIndices[0] : -1,
                HighlightNote = bestRentIndices.Count == 1 ? "Giá tốt nhất" : string.Empty
            });

            var minDeposit = columns.Min(c => c.Deposit);
            var bestDepositIndices = columns.Select((c, idx) => c.Deposit == minDeposit ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Tiền đặt cọc",
                Category = "Chi phí",
                Values = columns.Select(c => c.FormattedDeposit).ToList(),
                BestColumnIndex = bestDepositIndices.Count == 1 ? bestDepositIndices[0] : -1,
                HighlightNote = bestDepositIndices.Count == 1 ? "Cọc thấp nhất" : string.Empty
            });

            // AC2: 2. Quy mô & Vị trí tới ICTU (Area, Distance & Travel Times)
            var maxArea = columns.Max(c => c.Area);
            var bestAreaIndices = columns.Select((c, idx) => c.Area == maxArea ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Diện tích",
                Category = "Không gian",
                Values = columns.Select(c => c.FormattedArea).ToList(),
                BestColumnIndex = bestAreaIndices.Count == 1 ? bestAreaIndices[0] : -1,
                HighlightNote = bestAreaIndices.Count == 1 ? "Rộng rãi nhất" : string.Empty
            });

            var minDist = columns.Min(c => c.DistanceToCampusKm);
            var bestDistIndices = columns.Select((c, idx) => c.DistanceToCampusKm == minDist ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Khoảng cách tới ICTU",
                Category = "Vị trí",
                Values = columns.Select(c => c.FormattedDistance).ToList(),
                BestColumnIndex = bestDistIndices.Count == 1 ? bestDistIndices[0] : -1,
                HighlightNote = bestDistIndices.Count == 1 ? "Gần trường nhất" : string.Empty
            });

            var minMoto = columns.Min(c => c.MotorbikeMinutes);
            var bestMotoIndices = columns.Select((c, idx) => c.MotorbikeMinutes == minMoto ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Đi xe máy tới ICTU",
                Category = "Đi lại",
                Values = columns.Select(c => $"{c.MotorbikeMinutes} phút").ToList(),
                BestColumnIndex = bestMotoIndices.Count == 1 ? bestMotoIndices[0] : -1,
                HighlightNote = bestMotoIndices.Count == 1 ? "Nhanh nhất" : string.Empty
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Đi bộ tới ICTU",
                Category = "Đi lại",
                Values = columns.Select(c => $"{c.WalkMinutes} phút").ToList()
            });

            // AC2: 3. Điểm An Ninh & Tiện Ích
            var maxSecurity = columns.Max(c => c.SecurityScore);
            var bestSecIndices = columns.Select((c, idx) => c.SecurityScore == maxSecurity ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Điểm an ninh",
                Category = "An ninh & An toàn",
                Values = columns.Select(c => c.FormattedSecurityScore).ToList(),
                BestColumnIndex = bestSecIndices.Count == 1 ? bestSecIndices[0] : -1,
                HighlightNote = bestSecIndices.Count == 1 ? "An ninh tốt nhất" : string.Empty
            });

            var maxAmenitiesCount = columns.Max(c => c.Amenities.Count);
            var bestAmenitiesIndices = columns.Select((c, idx) => c.Amenities.Count == maxAmenitiesCount ? idx : -1).Where(i => i >= 0).ToList();
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Số lượng tiện ích",
                Category = "Tiện nghi",
                Values = columns.Select(c => $"{c.Amenities.Count} tiện ích").ToList(),
                BestColumnIndex = bestAmenitiesIndices.Count == 1 ? bestAmenitiesIndices[0] : -1,
                HighlightNote = bestAmenitiesIndices.Count == 1 ? "Đầy đủ tiện nghi nhất" : string.Empty
            });

            // Detailed checklist for individual amenities
            var standardAmenities = new[] { "Điều hòa", "Nóng lạnh", "Máy giặt", "WiFi", "Tủ lạnh", "Gác xép", "Ban công", "WC khép kín" };
            var allAmenities = standardAmenities
                .Union(columns.SelectMany(c => c.Amenities), StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var amenity in allAmenities)
            {
                var rowValues = columns.Select(c =>
                    c.Amenities.Any(a => a.Equals(amenity, StringComparison.OrdinalIgnoreCase))
                        ? "✓ Có"
                        : "— Chưa có").ToList();

                rows.Add(new ComparisonMetricRow
                {
                    MetricName = $"Tiện ích: {amenity}",
                    Category = "Chi tiết tiện nghi",
                    Values = rowValues
                });
            }

            // AC2: 4. Nội quy & Thông tin chung
            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Giờ giới nghiêm",
                Category = "Nội quy",
                Values = columns.Select(c => string.IsNullOrWhiteSpace(c.QuietHours) ? "Tự do" : c.QuietHours).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Nấu ăn trong phòng",
                Category = "Nội quy",
                Values = columns.Select(c => string.IsNullOrWhiteSpace(c.CookingRules) ? "Theo thỏa thuận" : c.CookingRules).ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Khu vực / Phường",
                Category = "Địa chỉ",
                Values = columns.Select(c => string.IsNullOrWhiteSpace(c.Ward) ? c.Address : $"Phường {c.Ward}").ToList()
            });

            rows.Add(new ComparisonMetricRow
            {
                MetricName = "Loại phòng",
                Category = "Thông tin chung",
                Values = columns.Select(c => c.RoomType).ToList()
            });

            // Calculate overall recommendations
            var bestValueColumn = columns.OrderBy(c => c.MonthlyRent).ThenByDescending(c => c.SecurityScore).First();
            var closestColumn = columns.OrderBy(c => c.DistanceToCampusKm).First();

            var verdict = columns.Count switch
            {
                1 => $"Phòng [{columns[0].Title}]: {columns[0].FormattedRent}, cách ICTU {columns[0].FormattedDistance}.",
                _ => $"Phòng '{bestValueColumn.Title}' có giá thuê tốt nhất ({bestValueColumn.FormattedRent}). Phòng '{closestColumn.Title}' gần ICTU nhất ({closestColumn.FormattedDistance})."
            };

            return new ComparisonMatrixModel
            {
                Title = "Bảng so sánh phòng trọ",
                GeneratedAt = DateTime.UtcNow,
                BestValueRoomId = bestValueColumn.RoomId,
                ClosestToCampusRoomId = closestColumn.RoomId,
                OverallVerdict = verdict,
                Columns = columns,
                Rows = rows
            };
        }

        /// <summary>
        /// AC3: Generates image payload for sharing comparison table.
        /// Produces a beautifully styled SVG vector image with full room details, badges, and comparison metrics.
        /// </summary>
        public virtual ComparisonSharePayload GenerateComparisonImage(
            IReadOnlyList<RoomListing> rooms,
            ComparisonExportOptions? options = null)
        {
            var matrix = BuildComparisonMatrix(rooms);
            var opt = options ?? new ComparisonExportOptions();
            var summaryText = GenerateComparisonTextSummary(rooms);

            const int canvasWidth = 960;
            const int marginX = 24;
            const int labelColWidth = 220;
            int roomColCount = Math.Max(1, matrix.Columns.Count);
            int roomColWidth = (canvasWidth - (marginX * 2) - labelColWidth) / roomColCount;

            const int headerHeight = 120;
            const int roomCardsHeight = 130;
            const int rowHeight = 38;
            const int footerHeight = 70;
            int totalHeight = headerHeight + roomCardsHeight + (Math.Max(1, matrix.Rows.Count) * rowHeight) + footerHeight;

            var sb = new StringBuilder();
            sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{canvasWidth}"" height=""{totalHeight}"" viewBox=""0 0 {canvasWidth} {totalHeight}"">");
            sb.AppendLine("  <defs>");
            sb.AppendLine(@"    <linearGradient id=""headerGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
            sb.AppendLine(@"      <stop offset=""0%"" stop-color=""#0284C7""/>");
            sb.AppendLine(@"      <stop offset=""100%"" stop-color=""#0369A1""/>");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");

            // Background
            sb.AppendLine($@"  <rect width=""100%"" height=""100%"" fill=""#F8FAFC""/>");

            // Header Banner
            sb.AppendLine($@"  <rect x=""{marginX}"" y=""20"" width=""{canvasWidth - (marginX * 2)}"" height=""85"" rx=""12"" fill=""url(#headerGrad)""/>");
            sb.AppendLine($@"  <text x=""{marginX + 20}"" y=""54"" font-family=""Arial, Helvetica, sans-serif"" font-size=""20"" font-weight=""bold"" fill=""#FFFFFF"">{EscapeXml(opt.Title)}</text>");
            sb.AppendLine($@"  <text x=""{marginX + 20}"" y=""80"" font-family=""Arial, Helvetica, sans-serif"" font-size=""13"" fill=""#E0F2FE"">Trường ĐH Công nghệ Thông tin &amp; Truyền thông (ICTU) • {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</text>");

            int currentY = 120;

            if (matrix.IsEmpty)
            {
                sb.AppendLine($@"  <rect x=""{marginX}"" y=""{currentY}"" width=""{canvasWidth - (marginX * 2)}"" height=""120"" rx=""8"" fill=""#FFFFFF"" stroke=""#E2E8F0""/>");
                sb.AppendLine($@"  <text x=""{canvasWidth / 2}"" y=""{currentY + 65}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""16"" fill=""#64748B"" text-anchor=""middle"">Chưa có phòng nào được chọn để so sánh (Tối đa 3 phòng).</text>");
            }
            else
            {
                // Room Cards Header
                for (int col = 0; col < matrix.Columns.Count; col++)
                {
                    var c = matrix.Columns[col];
                    int cardX = marginX + labelColWidth + (col * roomColWidth);

                    sb.AppendLine($@"  <g transform=""translate({cardX}, {currentY})"">");
                    sb.AppendLine($@"    <rect width=""{roomColWidth - 8}"" height=""{roomCardsHeight - 10}"" rx=""10"" fill=""#FFFFFF"" stroke=""#CBD5E1"" stroke-width=""1.5""/>");

                    var displayTitle = c.Title.Length > 24 ? c.Title[..21] + "..." : c.Title;
                    sb.AppendLine($@"    <text x=""12"" y=""26"" font-family=""Arial, Helvetica, sans-serif"" font-size=""14"" font-weight=""bold"" fill=""#0F172A"">{EscapeXml(displayTitle)}</text>");
                    sb.AppendLine($@"    <text x=""12"" y=""50"" font-family=""Arial, Helvetica, sans-serif"" font-size=""16"" font-weight=""bold"" fill=""#0284C7"">{EscapeXml(c.FormattedRent)}</text>");
                    sb.AppendLine($@"    <text x=""12"" y=""72"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12"" fill=""#64748B"">Cọc: {EscapeXml(c.FormattedDeposit)} | {c.FormattedArea}</text>");
                    sb.AppendLine($@"    <text x=""12"" y=""92"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12"" fill=""#475569"">Cách ICTU: {EscapeXml(c.FormattedDistance)}</text>");

                    if (c.RoomId == matrix.BestValueRoomId)
                    {
                        sb.AppendLine($@"    <rect x=""12"" y=""100"" width=""88"" height=""16"" rx=""4"" fill=""#DCFCE7""/>");
                        sb.AppendLine($@"    <text x=""16"" y=""112"" font-family=""Arial, Helvetica, sans-serif"" font-size=""10"" font-weight=""bold"" fill=""#15803D"">★ Giá tốt nhất</text>");
                    }
                    else if (c.RoomId == matrix.ClosestToCampusRoomId)
                    {
                        sb.AppendLine($@"    <rect x=""12"" y=""100"" width=""88"" height=""16"" rx=""4"" fill=""#E0F2FE""/>");
                        sb.AppendLine($@"    <text x=""16"" y=""112"" font-family=""Arial, Helvetica, sans-serif"" font-size=""10"" font-weight=""bold"" fill=""#0369A1"">★ Gần ICTU nhất</text>");
                    }

                    sb.AppendLine("  </g>");
                }

                currentY += roomCardsHeight;

                // Comparison Metrics Rows
                for (int r = 0; r < matrix.Rows.Count; r++)
                {
                    var row = matrix.Rows[r];
                    var rowBg = r % 2 == 0 ? "#FFFFFF" : "#F8FAFC";

                    sb.AppendLine($@"  <rect x=""{marginX}"" y=""{currentY}"" width=""{canvasWidth - (marginX * 2)}"" height=""{rowHeight}"" fill=""{rowBg}"" stroke=""#E2E8F0"" stroke-width=""0.5""/>");
                    sb.AppendLine($@"  <text x=""{marginX + 12}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12.5"" font-weight=""600"" fill=""#334155"">{EscapeXml(row.MetricName)}</text>");

                    for (int col = 0; col < matrix.Columns.Count; col++)
                    {
                        int cellX = marginX + labelColWidth + (col * roomColWidth);
                        var val = col < row.Values.Count ? row.Values[col] : string.Empty;
                        bool isBest = col == row.BestColumnIndex;

                        if (isBest && opt.HighlightBestValues)
                        {
                            sb.AppendLine($@"  <rect x=""{cellX}"" y=""{currentY + 2}"" width=""{roomColWidth - 8}"" height=""{rowHeight - 4}"" rx=""6"" fill=""#DCFCE7"" fill-opacity=""0.7""/>");
                            sb.AppendLine($@"  <text x=""{cellX + 12}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12.5"" font-weight=""bold"" fill=""#166534"">{EscapeXml(val)}</text>");
                        }
                        else if (val.Contains("✓"))
                        {
                            sb.AppendLine($@"  <text x=""{cellX + 12}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12.5"" font-weight=""bold"" fill=""#16A34A"">{EscapeXml(val)}</text>");
                        }
                        else if (val.Contains("—"))
                        {
                            sb.AppendLine($@"  <text x=""{cellX + 12}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12.5"" fill=""#94A3B8"">{EscapeXml(val)}</text>");
                        }
                        else
                        {
                            sb.AppendLine($@"  <text x=""{cellX + 12}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""12.5"" fill=""#1E293B"">{EscapeXml(val)}</text>");
                        }
                    }

                    currentY += rowHeight;
                }
            }

            // Footer
            currentY += 15;
            sb.AppendLine($@"  <line x1=""{marginX}"" y1=""{currentY}"" x2=""{canvasWidth - marginX}"" y2=""{currentY}"" stroke=""#CBD5E1"" stroke-width=""1""/>");
            sb.AppendLine($@"  <text x=""{marginX}"" y=""{currentY + 24}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""11.5"" fill=""#64748B"">RoomToRentRadar • So sánh phòng trọ sinh viên ICTU • Điểm đích: 21.5850° N, 105.8064° E</text>");
            if (!string.IsNullOrWhiteSpace(matrix.OverallVerdict))
            {
                var verdictDisplay = matrix.OverallVerdict.Length > 90 ? matrix.OverallVerdict[..87] + "..." : matrix.OverallVerdict;
                sb.AppendLine($@"  <text x=""{marginX}"" y=""{currentY + 42}"" font-family=""Arial, Helvetica, sans-serif"" font-size=""11.5"" font-weight=""bold"" fill=""#0369A1"">Đánh giá: {EscapeXml(verdictDisplay)}</text>");
            }
            sb.AppendLine("</svg>");

            var svgString = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(svgString);
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

        private static string EscapeXml(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
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
