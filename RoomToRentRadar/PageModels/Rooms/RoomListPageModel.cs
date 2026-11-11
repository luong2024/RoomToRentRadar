using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels.Rooms
{
    /// <summary>
    /// ViewModel for US02 Verified Room Listings Discovery Screen, Room Cards, and Infinite Scroll.
    /// AC1: Default sorted by distance to ICTU campus (DistanceAsc).
    /// AC2: Cards show title, monthly rent, area, distance to campus, and verification badge.
    /// AC3: Pagination / Infinite scroll handling up to 500 listings efficiently.
    /// AC4: Displays strictly listings with Status = "Available" and IsVerified = true.
    /// </summary>
    public partial class RoomListPageModel : ObservableObject
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IRoomListingRepository? _listingRepository;
        private readonly IFavoriteRepository? _favoriteRepository;
        private readonly RentalDatabaseService? _databaseService;
        private readonly IAuthService? _authService;
        private readonly ILogger<RoomListPageModel>? _logger;

        public const int PageSize = 10;
        private int _currentPage = 0;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ObservableCollection<RoomListing> Rooms { get; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial bool IsRefreshing { get; set; }

        [ObservableProperty]
        public partial bool IsLoadingMore { get; set; }

        [ObservableProperty]
        public partial bool HasMoreItems { get; set; } = true;

        [ObservableProperty]
        public partial int TotalCount { get; set; }

        [ObservableProperty]
        public partial bool IsEmpty { get; set; }

        [ObservableProperty]
        public partial string SelectedSortBy { get; set; } = "DistanceAsc"; // DistanceAsc, RentAsc, RentDesc, AreaDesc

        [ObservableProperty]
        public partial string SearchKeyword { get; set; } = string.Empty;

        public RoomListPageModel(IServiceProvider serviceProvider, ILogger<RoomListPageModel>? logger = null)
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
        public RoomListPageModel(
            IRoomListingRepository? listingRepository,
            IFavoriteRepository? favoriteRepository = null,
            RentalDatabaseService? databaseService = null,
            IAuthService? authService = null,
            ILogger<RoomListPageModel>? logger = null)
        {
            _listingRepository = listingRepository;
            _favoriteRepository = favoriteRepository;
            _databaseService = databaseService;
            _authService = authService;
            _logger = logger;
            _serviceProvider = null;
        }

        partial void OnSearchKeywordChanged(string value)
        {
            _ = LoadRoomsAsync();
        }

        private int GetCurrentUserId()
        {
            return _authService?.CurrentUser?.Id ?? 1;
        }

        /// <summary>
        /// US02 AC1, AC3, AC4: Loads initial page of verified, available rooms sorted by distance to ICTU by default.
        /// </summary>
        [RelayCommand]
        public async Task LoadRoomsAsync()
        {
            if (IsBusy) return;

            await _loadLock.WaitAsync();
            try
            {
                IsBusy = true;
                _currentPage = 0;
                HasMoreItems = true;

                var rooms = await FetchRoomsPageAsync(pageIndex: 0, pageSize: PageSize, SelectedSortBy, SearchKeyword);

                Rooms.Clear();
                foreach (var r in rooms)
                {
                    Rooms.Add(r);
                }

                TotalCount = Rooms.Count;
                IsEmpty = Rooms.Count == 0;
                if (rooms.Count < PageSize)
                {
                    HasMoreItems = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading rooms");
                Rooms.Clear();
                IsEmpty = true;
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                _loadLock.Release();
            }
        }

        [RelayCommand]
        public async Task RefreshRoomsAsync()
        {
            IsRefreshing = true;
            await LoadRoomsAsync();
        }

        /// <summary>
        /// US02 AC3: Infinite scroll pagination triggered when scrolling near the end of list.
        /// </summary>
        [RelayCommand]
        public async Task LoadMoreRoomsAsync()
        {
            if (IsBusy || IsLoadingMore || !HasMoreItems) return;

            await _loadLock.WaitAsync();
            try
            {
                IsLoadingMore = true;
                int nextPage = _currentPage + 1;

                var nextRooms = await FetchRoomsPageAsync(pageIndex: nextPage, pageSize: PageSize, SelectedSortBy, SearchKeyword);

                if (nextRooms.Count == 0)
                {
                    HasMoreItems = false;
                }
                else
                {
                    _currentPage = nextPage;
                    foreach (var r in nextRooms)
                    {
                        if (!Rooms.Any(existing => existing.Id == r.Id))
                        {
                            Rooms.Add(r);
                        }
                    }

                    TotalCount = Rooms.Count;
                    if (nextRooms.Count < PageSize)
                    {
                        HasMoreItems = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading more rooms");
                HasMoreItems = false;
            }
            finally
            {
                IsLoadingMore = false;
                _loadLock.Release();
            }
        }

        /// <summary>
        /// US02 AC1: Sort options (DistanceAsc is default).
        /// </summary>
        [RelayCommand]
        public async Task ChangeSortByAsync(string sortBy)
        {
            if (SelectedSortBy == sortBy) return;
            SelectedSortBy = sortBy;
            await LoadRoomsAsync();
        }

        /// <summary>
        /// Toggles room favorite status and updates local item state immediately.
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
                        cmd.ExecuteNonQuery();
                    }
                }

                string msg = room.IsFavorite
                    ? $"Đã thêm \"{room.Title}\" vào danh sách yêu thích"
                    : $"Đã xóa \"{room.Title}\" khỏi danh sách yêu thích";
                NotifySnackbar(msg);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to toggle favorite for room {RoomId}", room.Id);
            }
        }

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

        [RelayCommand]
        public async Task NavigateToFilterAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(Constants.Routes.RoomFilter);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room filter");
            }
        }

        [RelayCommand]
        public async Task NavigateToMapAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(Constants.Routes.RoomMap);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room map");
            }
        }

        private async Task<List<RoomListing>> FetchRoomsPageAsync(int pageIndex, int pageSize, string sortBy, string keyword)
        {
            List<RoomListing> result;

            if (_listingRepository != null)
            {
                // Call real repository when merged
                result = await _listingRepository.ListVerifiedRoomsAsync(pageIndex, pageSize, sortBy);
            }
            else if (_databaseService != null)
            {
                // Direct SQLite query adhering to AC1 & AC4
                result = await LoadRoomsFromDatabaseAsync(pageIndex, pageSize, sortBy, keyword);
            }
            else
            {
                // Standalone Mock fallback
                result = GetFallbackMockRooms(pageIndex, pageSize, sortBy, keyword);
            }

            // Sync with user favorites
            await SyncFavoriteStatusesAsync(result);

            return result;
        }

        private async Task<List<RoomListing>> LoadRoomsFromDatabaseAsync(int pageIndex, int pageSize, string sortBy, string keyword)
        {
            var list = new List<RoomListing>();
            if (_databaseService == null) return list;

            await using var connection = await _databaseService.GetOpenConnectionAsync();

            string orderByClause = sortBy switch
            {
                "RentAsc" => "MonthlyRent ASC",
                "RentDesc" => "MonthlyRent DESC",
                "AreaDesc" => "Area DESC",
                _ => "DistanceToCampusKm ASC" // Default: US02 AC1 (sorted by distance from ICTU campus)
            };

            string whereClause = "Status = 'Available' AND IsVerified = 1"; // US02 AC4 (Only Available and Verified)

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClause += " AND (Title LIKE @kw OR Address LIKE @kw OR Ward LIKE @kw OR RoomType LIKE @kw)";
            }

            int offset = pageIndex * pageSize;
            string sql = $@"
            SELECT * FROM RoomListing 
            WHERE {whereClause}
            ORDER BY {orderByClause}
            LIMIT @pageSize OFFSET @offset;";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                cmd.Parameters.AddWithValue("@kw", $"%{keyword.Trim()}%");
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var room = new RoomListing
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
                    BusMinutes = reader.GetInt32(reader.GetOrdinal("BusMinutes")),
                    WalkMinutes = reader.GetInt32(reader.GetOrdinal("WalkMinutes")),
                    AmenitiesCsv = reader.GetString(reader.GetOrdinal("AmenitiesCsv")),
                    ThumbnailUrl = reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
                    PhotosJson = reader.GetString(reader.GetOrdinal("PhotosJson")),
                    Panorama360Url = reader.GetString(reader.GetOrdinal("Panorama360Url")),
                    LandlordId = reader.GetInt32(reader.GetOrdinal("LandlordId")),
                    LandlordName = reader.GetString(reader.GetOrdinal("LandlordName")),
                    LandlordPhone = reader.GetString(reader.GetOrdinal("LandlordPhone")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    IsVerified = reader.GetInt32(reader.GetOrdinal("IsVerified")) == 1,
                    SecurityScore = reader.GetDouble(reader.GetOrdinal("SecurityScore")),
                    QuietHours = reader.GetString(reader.GetOrdinal("QuietHours")),
                    GuestPolicy = reader.GetString(reader.GetOrdinal("GuestPolicy")),
                    CookingRules = reader.GetString(reader.GetOrdinal("CookingRules")),
                    ParkingRules = reader.GetString(reader.GetOrdinal("ParkingRules")),
                    RulesVersion = reader.GetInt32(reader.GetOrdinal("RulesVersion")),
                    UpdatedAt = reader.GetString(reader.GetOrdinal("UpdatedAt"))
                };
                list.Add(room);
            }

            return list;
        }

        private async Task SyncFavoriteStatusesAsync(List<RoomListing> rooms)
        {
            if (rooms.Count == 0) return;
            int userId = GetCurrentUserId();

            try
            {
                HashSet<int> favoriteRoomIds;
                if (_favoriteRepository != null)
                {
                    var favs = await _favoriteRepository.GetFavoriteRoomsAsync(userId);
                    favoriteRoomIds = favs.Select(f => f.Id).ToHashSet();
                }
                else if (_databaseService != null)
                {
                    favoriteRoomIds = new HashSet<int>();
                    await using var connection = await _databaseService.GetOpenConnectionAsync();
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT RoomId FROM FavoriteRoom WHERE UserId = @userId;";
                    cmd.Parameters.AddWithValue("@userId", userId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        favoriteRoomIds.Add(reader.GetInt32(0));
                    }
                }
                else
                {
                    favoriteRoomIds = new HashSet<int> { 1, 2 };
                }

                foreach (var r in rooms)
                {
                    r.IsFavorite = favoriteRoomIds.Contains(r.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to sync favorite statuses for user {UserId}", userId);
            }
        }

        private static List<RoomListing> GetFallbackMockRooms(int pageIndex, int pageSize, string sortBy, string keyword)
        {
            var all = new List<RoomListing>
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
                    DistanceToCampusKm = 0.3,
                    MotorbikeMinutes = 2,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.8
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
                    DistanceToCampusKm = 0.6,
                    MotorbikeMinutes = 2,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.5
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
                    DistanceToCampusKm = 0.8,
                    MotorbikeMinutes = 3,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.7
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
                    DistanceToCampusKm = 1.2,
                    MotorbikeMinutes = 4,
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
                    DistanceToCampusKm = 1.9,
                    MotorbikeMinutes = 6,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.4
                }
            };

            // Filter
            var filtered = all.Where(r => r.Status == "Available" && r.IsVerified);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    r.Title.ToLowerInvariant().Contains(kw) ||
                    r.Address.ToLowerInvariant().Contains(kw) ||
                    r.Ward.ToLowerInvariant().Contains(kw) ||
                    r.RoomType.ToLowerInvariant().Contains(kw));
            }

            // Sort
            filtered = sortBy switch
            {
                "RentAsc" => filtered.OrderBy(r => r.MonthlyRent),
                "RentDesc" => filtered.OrderByDescending(r => r.MonthlyRent),
                "AreaDesc" => filtered.OrderByDescending(r => r.Area),
                _ => filtered.OrderBy(r => r.DistanceToCampusKm)
            };

            return filtered.Skip(pageIndex * pageSize).Take(pageSize).ToList();
        }

        private static void NotifySnackbar(string message)
        {
            try
            {
                if (Shell.Current != null)
                {
                    _ = AppShell.DisplaySnackbarAsync(message);
                }
            }
            catch
            {
            }
        }
    }
}

