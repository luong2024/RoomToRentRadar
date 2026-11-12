using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.Map
{
    /// <summary>
    /// ViewModel for Sprint 1 US06 (Interactive Map with pins and preview cards).
    /// AC1: Map displays interactive pins for all verified rooms around ICTU.
    /// AC2: Tapping a pin highlights it and reveals a room preview card.
    /// AC3: Tapping the preview card navigates to the full room detail screen.
    /// AC4: Center map button resets camera to ICTU campus.
    /// </summary>
    public partial class RoomMapPageModel : ObservableObject
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IRoomListingRepository? _listingRepository;
        private readonly IFavoriteRepository? _favoriteRepository;
        private readonly RentalDatabaseService? _databaseService;
        private readonly IAuthService? _authService;
        private readonly ILogger<RoomMapPageModel>? _logger;

        public ObservableCollection<RoomListing> VerifiedRooms { get; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPreviewCardVisible))]
        public partial RoomListing? SelectedRoom { get; set; }

        public bool IsPreviewCardVisible => SelectedRoom != null;

        [ObservableProperty]
        public partial int VerifiedRoomsCount { get; set; }

        [ObservableProperty]
        public partial double CenterLatitude { get; set; } = Constants.IctuLatitude;

        [ObservableProperty]
        public partial double CenterLongitude { get; set; } = Constants.IctuLongitude;

        [ObservableProperty]
        public partial int ZoomLevel { get; set; } = 15;

        [ObservableProperty]
        public partial string MapHtmlContent { get; set; } = string.Empty;

        public RoomMapPageModel(IServiceProvider serviceProvider, ILogger<RoomMapPageModel>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _listingRepository = serviceProvider.GetService<IRoomListingRepository>();
            _favoriteRepository = serviceProvider.GetService<IFavoriteRepository>();
            _databaseService = serviceProvider.GetService<RentalDatabaseService>();
            _authService = serviceProvider.GetService<IAuthService>();
        }

        /// <summary>
        /// Secondary constructor for Unit Testing without relying on MAUI DI container.
        /// </summary>
        public RoomMapPageModel(
            IRoomListingRepository? listingRepository,
            IFavoriteRepository? favoriteRepository = null,
            RentalDatabaseService? databaseService = null,
            IAuthService? authService = null,
            ILogger<RoomMapPageModel>? logger = null)
        {
            _listingRepository = listingRepository;
            _favoriteRepository = favoriteRepository;
            _databaseService = databaseService;
            _authService = authService;
            _logger = logger;
            _serviceProvider = null;
        }

        private int GetCurrentUserId()
        {
            return _authService?.CurrentUser?.Id ?? 1;
        }

        /// <summary>
        /// US06 AC1: Loads all verified, available rooms around ICTU campus.
        /// </summary>
        [RelayCommand]
        public async Task LoadMapRoomsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                List<RoomListing> rooms;

                if (_listingRepository != null)
                {
                    rooms = await _listingRepository.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 50, sortBy: "DistanceAsc");
                }
                else if (_databaseService != null)
                {
                    rooms = await LoadVerifiedRoomsFromDatabaseAsync();
                }
                else
                {
                    rooms = GetFallbackMapRooms();
                }

                // Strictly verified and available
                var verifiedList = rooms.Where(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available).ToList();

                VerifiedRooms.Clear();
                foreach (var r in verifiedList)
                {
                    VerifiedRooms.Add(r);
                }

                VerifiedRoomsCount = VerifiedRooms.Count;
                MapHtmlContent = GenerateLeafletMapHtml(verifiedList, SelectedRoom?.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load map rooms");
                var fallback = GetFallbackMapRooms();
                VerifiedRooms.Clear();
                foreach (var r in fallback)
                {
                    VerifiedRooms.Add(r);
                }
                VerifiedRoomsCount = VerifiedRooms.Count;
                MapHtmlContent = GenerateLeafletMapHtml(fallback, SelectedRoom?.Id);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// US06 AC2: Tapping a pin highlights it and reveals a room preview card.
        /// </summary>
        [RelayCommand]
        public void SelectRoomById(int roomId)
        {
            var match = VerifiedRooms.FirstOrDefault(r => r.Id == roomId);
            if (match != null)
            {
                SelectedRoom = match;
            }
        }

        /// <summary>
        /// Dismisses the bottom preview card.
        /// </summary>
        [RelayCommand]
        public void DismissPreviewCard()
        {
            SelectedRoom = null;
        }

        /// <summary>
        /// US06 AC4: Center map button resets camera to ICTU campus (21.5850, 105.8064).
        /// </summary>
        [RelayCommand]
        public void CenterIctuCampus()
        {
            CenterLatitude = Constants.IctuLatitude;
            CenterLongitude = Constants.IctuLongitude;
            ZoomLevel = 15;
            MapHtmlContent = GenerateLeafletMapHtml(VerifiedRooms.ToList(), SelectedRoom?.Id);
        }

        /// <summary>
        /// US06 AC3: Tapping the preview card navigates to the full room detail screen.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToDetailAsync(RoomListing? room)
        {
            if (room == null) return;
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync($"{Constants.Routes.RoomDetail}?roomId={room.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room detail {RoomId}", room.Id);
            }
        }

        /// <summary>
        /// Toggles favorite status directly from the map preview card.
        /// </summary>
        [RelayCommand]
        public async Task ToggleFavoriteAsync(RoomListing? room)
        {
            if (room == null) return;
            room.IsFavorite = !room.IsFavorite;
            int userId = GetCurrentUserId();

            try
            {
                if (_favoriteRepository != null)
                {
                    await _favoriteRepository.ToggleFavoriteAsync(userId, room.Id);
                }
                else if (_databaseService != null)
                {
                    await using var connection = await _databaseService.GetOpenConnectionAsync();
                    if (room.IsFavorite)
                    {
                        await using var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                        INSERT INTO FavoriteRoom (UserId, RoomId, CreatedAt)
                        SELECT @userId, @roomId, @createdAt
                        WHERE NOT EXISTS (SELECT 1 FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId);";
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@roomId", room.Id);
                        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        await using var cmd = connection.CreateCommand();
                        cmd.CommandText = "DELETE FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId;";
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@roomId", room.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                if (Shell.Current != null)
                {
                    string msg = room.IsFavorite ? "Đã lưu vào danh sách yêu thích" : "Đã hủy lưu phòng";
                    _ = AppShell.DisplaySnackbarAsync(msg);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling favorite for room {RoomId}", room.Id);
            }
        }

        private async Task<List<RoomListing>> LoadVerifiedRoomsFromDatabaseAsync()
        {
            var list = new List<RoomListing>();
            if (_databaseService == null) return list;

            await using var connection = await _databaseService.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM RoomListing WHERE Status = 'Available' AND IsVerified = 1 ORDER BY DistanceToCampusKm ASC;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RoomListing
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    MonthlyRent = (decimal)reader.GetDouble(reader.GetOrdinal("MonthlyRent")),
                    Deposit = (decimal)reader.GetDouble(reader.GetOrdinal("Deposit")),
                    Area = reader.GetDouble(reader.GetOrdinal("Area")),
                    RoomType = reader.GetString(reader.GetOrdinal("RoomType")),
                    Address = reader.GetString(reader.GetOrdinal("Address")),
                    Ward = reader.GetString(reader.GetOrdinal("Ward")),
                    Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                    Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                    DistanceToCampusKm = reader.GetDouble(reader.GetOrdinal("DistanceToCampusKm")),
                    MotorbikeMinutes = reader.GetInt32(reader.GetOrdinal("MotorbikeMinutes")),
                    ThumbnailUrl = reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    IsVerified = reader.GetInt32(reader.GetOrdinal("IsVerified")) == 1,
                    SecurityScore = reader.GetDouble(reader.GetOrdinal("SecurityScore"))
                });
            }
            return list;
        }

        public string GenerateLeafletMapHtml(List<RoomListing> rooms, int? selectedRoomId = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'/><meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'/>");
            sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>");
            sb.AppendLine("<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; background-color: #e5e3df; }");
            sb.AppendLine(".ictu-marker { background: #0D47A1; color: white; border-radius: 50%; padding: 6px; border: 3px solid #FFD54F; font-size: 14px; text-align: center; box-shadow: 0 3px 8px rgba(0,0,0,0.4); }");
            sb.AppendLine(".room-marker { background: #2E7D32; color: white; border-radius: 14px; padding: 4px 8px; font-weight: bold; font-size: 11px; border: 2px solid white; box-shadow: 0 2px 6px rgba(0,0,0,0.3); white-space: nowrap; cursor: pointer; }");
            sb.AppendLine(".room-marker.selected { background: #E53935; border-color: #FFEB3B; transform: scale(1.15); box-shadow: 0 4px 12px rgba(229,57,53,0.6); }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div id='map'></div><script>");

            sb.AppendLine($"var map = L.map('map', {{ zoomControl: false }}).setView([{CenterLatitude:F4}, {CenterLongitude:F4}], {ZoomLevel});");
            sb.AppendLine("L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '© OpenStreetMap' }).addTo(map);");
            sb.AppendLine("L.control.zoom({ position: 'topright' }).addTo(map);");

            // ICTU Campus Landmark
            sb.AppendLine($"var ictuIcon = L.divIcon({{ className: 'custom-icon', html: '<div class=\"ictu-marker\">🎓 ICTU</div>', iconSize: [60, 32], iconAnchor: [30, 16] }});");
            sb.AppendLine($"L.marker([{Constants.IctuLatitude:F4}, {Constants.IctuLongitude:F4}], {{ icon: ictuIcon }}).addTo(map).bindPopup('<b>Đại học CNTT & TT Thái Nguyên (ICTU)</b><br>Khuôn viên trung tâm');");

            // Room Pins
            foreach (var room in rooms)
            {
                if (room.Latitude == 0 || room.Longitude == 0) continue;

                string isSelectedClass = selectedRoomId.HasValue && selectedRoomId.Value == room.Id ? " selected" : "";
                string rentFormatted = $"{room.MonthlyRent / 1_000_000m:F1}tr";
                string titleSafe = room.Title.Replace("'", "\\'");

                sb.AppendLine($"var icon_{room.Id} = L.divIcon({{ className: 'custom-icon', html: '<div class=\"room-marker{isSelectedClass}\" onclick=\"selectRoom({room.Id})\">🏠 {rentFormatted}</div>', iconSize: [65, 26], iconAnchor: [32, 13] }});");
                sb.AppendLine($"var m_{room.Id} = L.marker([{room.Latitude:F4}, {room.Longitude:F4}], {{ icon: icon_{room.Id} }}).addTo(map);");
                sb.AppendLine($"m_{room.Id}.on('click', function() {{ selectRoom({room.Id}); }});");
            }

            sb.AppendLine("function selectRoom(id) { window.location.href = 'roommap://select?id=' + id; }");
            sb.AppendLine("</script></body></html>");

            return sb.ToString();
        }

        private static List<RoomListing> GetFallbackMapRooms() => new()
        {
            new RoomListing
            {
                Id = 1,
                Title = "Phòng khép kín ban công thoáng sát cổng chính ICTU",
                MonthlyRent = 1500000m,
                Deposit = 1500000m,
                Area = 22,
                RoomType = "Phòng khép kín",
                Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                Latitude = 21.5862,
                Longitude = 105.8075,
                DistanceToCampusKm = 0.3,
                MotorbikeMinutes = 2,
                ThumbnailUrl = "dotnet_bot.png",
                Status = "Available",
                IsVerified = true,
                SecurityScore = 4.8
            },
            new RoomListing
            {
                Id = 2,
                Title = "Chung cư mini 28m2 full nội thất hồ điều hòa Tân Thịnh",
                MonthlyRent = 2200000m,
                Deposit = 2000000m,
                Area = 28,
                RoomType = "Chung cư mini",
                Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                Ward = "Tân Thịnh",
                Latitude = 21.5825,
                Longitude = 105.8112,
                DistanceToCampusKm = 0.8,
                MotorbikeMinutes = 3,
                ThumbnailUrl = "dotnet_bot.png",
                Status = "Available",
                IsVerified = true,
                SecurityScore = 4.7
            },
            new RoomListing
            {
                Id = 3,
                Title = "Phòng trọ sinh viên giá rẻ khu Quyết Thắng an ninh tốt",
                MonthlyRent = 950000m,
                Deposit = 500000m,
                Area = 16,
                RoomType = "Phòng khép kín",
                Address = "Xóm Gò Móc, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                Latitude = 21.5880,
                Longitude = 105.8020,
                DistanceToCampusKm = 0.6,
                MotorbikeMinutes = 2,
                ThumbnailUrl = "dotnet_bot.png",
                Status = "Available",
                IsVerified = true,
                SecurityScore = 4.5
            },
            new RoomListing
            {
                Id = 4,
                Title = "Phòng Studio cửa sổ lớn đường Z115 gần chợ sinh viên",
                MonthlyRent = 1800000m,
                Deposit = 1800000m,
                Area = 24,
                RoomType = "Chung cư mini",
                Address = "Số 145 Đường Z115, Phường Tân Thịnh, TP. Thái Nguyên",
                Ward = "Tân Thịnh",
                Latitude = 21.5840,
                Longitude = 105.8150,
                DistanceToCampusKm = 1.2,
                MotorbikeMinutes = 4,
                ThumbnailUrl = "dotnet_bot.png",
                Status = "Available",
                IsVerified = true,
                SecurityScore = 4.6
            },
            new RoomListing
            {
                Id = 5,
                Title = "Phòng ở ghép 2 người khu vực Thịnh Đán gần vòng xuyến",
                MonthlyRent = 800000m,
                Deposit = 800000m,
                Area = 25,
                RoomType = "Ở ghép",
                Address = "Tổ 4 Phường Thịnh Đán, TP. Thái Nguyên",
                Ward = "Thịnh Đán",
                Latitude = 21.5765,
                Longitude = 105.8190,
                DistanceToCampusKm = 1.9,
                MotorbikeMinutes = 6,
                ThumbnailUrl = "dotnet_bot.png",
                Status = "Available",
                IsVerified = true,
                SecurityScore = 4.4
            }
        };
    }
}

