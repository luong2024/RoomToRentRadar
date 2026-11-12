using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.Favorites
{
    /// <summary>
    /// ViewModel for US07 Favorites Screen, Heart Button interaction, and 5-Second Undo Toast/Banner.
    /// Supports automatic connection to IFavoriteRepository (T07.1), direct SQLite fallback via
    /// RentalDatabaseService, or local mock data so the screen operates 100% independently.
    /// </summary>
    public partial class FavoritesPageModel : ObservableObject
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IFavoriteRepository? _favoriteRepository;
        private readonly RentalDatabaseService? _databaseService;
        private readonly IAuthService? _authService;
        private readonly ILogger<FavoritesPageModel>? _logger;

        private readonly SemaphoreSlim _undoLock = new(1, 1);
        private CancellationTokenSource? _undoCts;
        private RoomListing? _pendingUndoRoom;
        private int _pendingUndoIndex = -1;
        private List<RoomListing> _allLoadedRooms = new();

        public ObservableCollection<RoomListing> FavoriteRooms { get; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial bool IsRefreshing { get; set; }

        [ObservableProperty]
        public partial bool IsEmpty { get; set; }

        [ObservableProperty]
        public partial int FavoritesCount { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CurrentUserDisplayName { get; set; } = "Sinh viên ICTU";

        [ObservableProperty]
        public partial bool ShowUndoBanner { get; set; }

        [ObservableProperty]
        public partial string UndoMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int UndoRemainingSeconds { get; set; } = 5;

        public FavoritesPageModel(IServiceProvider serviceProvider, ILogger<FavoritesPageModel>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _favoriteRepository = serviceProvider.GetService<IFavoriteRepository>();
            _databaseService = serviceProvider.GetService<RentalDatabaseService>();
            _authService = serviceProvider.GetService<IAuthService>();
        }

        /// <summary>
        /// Secondary constructor for Unit Testing without relying on MAUI DI container.
        /// </summary>
        public FavoritesPageModel(
            IFavoriteRepository? favoriteRepository,
            RentalDatabaseService? databaseService = null,
            IAuthService? authService = null,
            ILogger<FavoritesPageModel>? logger = null)
        {
            _favoriteRepository = favoriteRepository;
            _databaseService = databaseService;
            _authService = authService;
            _logger = logger;
            _serviceProvider = null;
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterRooms();
        }

        public int GetCurrentUserId()
        {
            return _authService?.CurrentUser?.Id ?? 1;
        }

        [RelayCommand]
        public async Task LoadFavoritesAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                await CommitPendingUndoAsync();

                int userId = GetCurrentUserId();
                CurrentUserDisplayName = _authService?.CurrentUser?.FullName ?? "Sinh viên ICTU";

                List<RoomListing> rooms;
                if (_favoriteRepository != null)
                {
                    rooms = await _favoriteRepository.GetFavoriteRoomsAsync(userId);
                }
                else if (_databaseService != null)
                {
                    rooms = await LoadFavoritesFromDatabaseAsync(userId);
                }
                else
                {
                    rooms = GetFallbackMockFavorites();
                }

                foreach (var r in rooms)
                {
                    r.IsFavorite = true;
                }

                _allLoadedRooms = rooms;
                FilterRooms();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading favorite rooms");
                _allLoadedRooms = GetFallbackMockFavorites();
                FilterRooms();
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        public async Task RefreshFavoritesAsync()
        {
            IsRefreshing = true;
            await LoadFavoritesAsync();
        }

        /// <summary>
        /// US07 AC1 and AC4: Removes room from favorites list immediately with a 5-second undo grace period.
        /// </summary>
        [RelayCommand]
        public async Task RemoveFavoriteAsync(RoomListing? room)
        {
            if (room == null) return;

            await CommitPendingUndoAsync();

            _pendingUndoRoom = room;
            _pendingUndoIndex = FavoriteRooms.IndexOf(room);
            room.IsFavorite = false;

            FavoriteRooms.Remove(room);
            _allLoadedRooms.Remove(room);
            FavoritesCount = FavoriteRooms.Count;
            IsEmpty = FavoriteRooms.Count == 0;

            UndoMessage = $"Đã xóa \"{TruncateTitle(room.Title, 28)}\" khỏi danh sách yêu thích";
            UndoRemainingSeconds = 5;
            ShowUndoBanner = true;

            NotifySnackbar($"Đã xóa phòng \"{TruncateTitle(room.Title, 24)}\". Nhấn Hoàn tác để khôi phục trong 5s.");

            _undoCts?.Cancel();
            _undoCts = new CancellationTokenSource();
            var token = _undoCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    for (int s = 5; s >= 1; s--)
                    {
                        if (token.IsCancellationRequested) return;
                        UpdateRemainingSeconds(s);
                        await Task.Delay(1000, token);
                    }

                    if (token.IsCancellationRequested) return;
                    await CommitPendingUndoAsync();
                }
                catch (OperationCanceledException)
                {
                    // User tapped Undo before timer expired
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in favorite undo countdown");
                }
            }, token);
        }

        /// <summary>
        /// US07 AC4: Restores the deleted room within 5 seconds without changing persistent storage.
        /// </summary>
        [RelayCommand]
        public void UndoRemoveFavorite()
        {
            _undoCts?.Cancel();

            if (_pendingUndoRoom != null)
            {
                var restored = _pendingUndoRoom;
                _pendingUndoRoom = null;
                restored.IsFavorite = true;

                int targetIndex = Math.Clamp(_pendingUndoIndex, 0, FavoriteRooms.Count);
                FavoriteRooms.Insert(targetIndex, restored);

                if (!_allLoadedRooms.Contains(restored))
                {
                    int allTarget = Math.Clamp(_pendingUndoIndex, 0, _allLoadedRooms.Count);
                    _allLoadedRooms.Insert(allTarget, restored);
                }

                FavoritesCount = FavoriteRooms.Count;
                IsEmpty = FavoriteRooms.Count == 0;
                ShowUndoBanner = false;

                NotifySnackbar($"Đã khôi phục \"{TruncateTitle(restored.Title, 24)}\" vào danh sách yêu thích.");
            }
        }

        /// <summary>
        /// Immediately commits pending deletion when user taps dismiss 'X' on undo banner.
        /// </summary>
        [RelayCommand]
        public async Task DismissUndoBannerAsync()
        {
            _undoCts?.Cancel();
            await CommitPendingUndoAsync();
        }

        /// <summary>
        /// US07 AC1: Heart icon adds or removes room instantly.
        /// </summary>
        [RelayCommand]
        public async Task ToggleFavoriteAsync(RoomListing? room)
        {
            if (room == null) return;

            if (room.IsFavorite)
            {
                await RemoveFavoriteAsync(room);
            }
            else
            {
                await AddFavoriteAsync(room);
            }
        }

        public async Task AddFavoriteAsync(RoomListing? room)
        {
            if (room == null) return;

            if (_pendingUndoRoom?.Id == room.Id)
            {
                _undoCts?.Cancel();
                _pendingUndoRoom = null;
                ShowUndoBanner = false;
            }

            room.IsFavorite = true;
            if (!FavoriteRooms.Contains(room))
            {
                FavoriteRooms.Insert(0, room);
            }
            if (!_allLoadedRooms.Contains(room))
            {
                _allLoadedRooms.Insert(0, room);
            }

            FavoritesCount = FavoriteRooms.Count;
            IsEmpty = FavoriteRooms.Count == 0;

            int userId = GetCurrentUserId();
            try
            {
                if (_favoriteRepository != null)
                {
                    await _favoriteRepository.AddFavoriteAsync(userId, room.Id);
                }
                else if (_databaseService != null)
                {
                    await using var connection = await _databaseService.GetOpenConnectionAsync();
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                    INSERT INTO FavoriteRoom (UserId, RoomId, CreatedAt)
                    SELECT @userId, @roomId, @createdAt
                    WHERE NOT EXISTS (
                        SELECT 1 FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId
                    );";
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@roomId", room.Id);
                    cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
                    await cmd.ExecuteNonQueryAsync();
                }

                NotifySnackbar($"Đã thêm \"{TruncateTitle(room.Title, 24)}\" vào danh sách yêu thích.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist favorite room {RoomId}", room.Id);
            }
        }

        public async Task CommitPendingUndoAsync()
        {
            RoomListing? toCommit = null;
            await _undoLock.WaitAsync();
            try
            {
                if (_pendingUndoRoom == null) return;
                toCommit = _pendingUndoRoom;
                _pendingUndoRoom = null;
                UpdateShowUndoBanner(false);
            }
            finally
            {
                _undoLock.Release();
            }

            if (toCommit == null) return;

            int userId = GetCurrentUserId();
            try
            {
                if (_favoriteRepository != null)
                {
                    await _favoriteRepository.RemoveFavoriteAsync(userId, toCommit.Id);
                }
                else if (_databaseService != null)
                {
                    await using var connection = await _databaseService.GetOpenConnectionAsync();
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId;";
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@roomId", toCommit.Id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error committing favorite removal for room {RoomId}", toCommit.Id);
            }
        }

        [RelayCommand]
        public async Task OpenRoomDetailAsync(RoomListing? room)
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
                _logger?.LogError(ex, "Error navigating to room detail {RoomId}", room.Id);
            }
        }

        [RelayCommand]
        public async Task ExploreRoomsAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    try
                    {
                        await Shell.Current.GoToAsync($"//{Constants.Routes.RoomList}");
                    }
                    catch
                    {
                        await Shell.Current.GoToAsync("//home");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to rooms list");
            }
        }

        private void FilterRooms()
        {
            FavoriteRooms.Clear();
            var query = SearchText?.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allLoadedRooms
                : _allLoadedRooms.Where(r =>
                    (r.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                    (r.Address?.ToLowerInvariant().Contains(query) ?? false) ||
                    (r.Ward?.ToLowerInvariant().Contains(query) ?? false) ||
                    (r.RoomType?.ToLowerInvariant().Contains(query) ?? false)).ToList();

            foreach (var r in filtered)
            {
                FavoriteRooms.Add(r);
            }

            FavoritesCount = FavoriteRooms.Count;
            IsEmpty = FavoriteRooms.Count == 0;
        }

        private async Task<List<RoomListing>> LoadFavoritesFromDatabaseAsync(int userId)
        {
            var list = new List<RoomListing>();
            if (_databaseService == null) return list;

            await using var connection = await _databaseService.GetOpenConnectionAsync();

            var roomIds = new List<int>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT RoomId FROM FavoriteRoom WHERE UserId = @userId ORDER BY Id DESC;";
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    roomIds.Add(reader.GetInt32(0));
                }
            }

            // Seed initial 2 favorites on first run if database table is completely empty
            if (roomIds.Count == 0)
            {
                await using var totalCmd = connection.CreateCommand();
                totalCmd.CommandText = "SELECT COUNT(1) FROM FavoriteRoom;";
                var totalFavorites = Convert.ToInt32(await totalCmd.ExecuteScalarAsync());
                if (totalFavorites == 0)
                {
                    await using var seedCmd = connection.CreateCommand();
                    seedCmd.CommandText = @"
                    INSERT INTO FavoriteRoom (UserId, RoomId, CreatedAt) VALUES 
                    (@userId, 1, @now),
                    (@userId, 2, @now);";
                    seedCmd.Parameters.AddWithValue("@userId", userId);
                    seedCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                    await seedCmd.ExecuteNonQueryAsync();

                    roomIds.Add(1);
                    roomIds.Add(2);
                }
            }

            if (roomIds.Count == 0) return list;

            var inClause = string.Join(",", roomIds);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM RoomListing WHERE Id IN ({inClause});";
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
                        UpdatedAt = reader.GetString(reader.GetOrdinal("UpdatedAt")),
                        IsFavorite = true
                    };
                    list.Add(room);
                }
            }

            return list;
        }

        private static List<RoomListing> GetFallbackMockFavorites() => new()
        {
            new RoomListing
            {
                Id = 1,
                Title = "Phòng khép kín ban công thoáng sát cổng chính ICTU",
                Description = "Phòng xây mới sạch đẹp, có điều hòa, nóng lạnh, giường tủ bàn học đầy đủ. Cách cổng trường ĐH CNTT & TT đúng 300m đi bộ.",
                MonthlyRent = 1500000m,
                Deposit = 1500000m,
                Area = 22,
                RoomType = "Phòng khép kín",
                Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                DistanceToCampusKm = 0.3,
                MotorbikeMinutes = 2,
                BusMinutes = 5,
                WalkMinutes = 4,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Wifi cáp quang,Khóa vân tay,Camera 24/7",
                ThumbnailUrl = "dotnet_bot.png",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.8,
                IsFavorite = true
            },
            new RoomListing
            {
                Id = 2,
                Title = "Chung cư mini 28m2 full nội thất hồ điều hòa Tân Thịnh",
                Description = "Phòng rộng rãi cho 2-3 bạn ở ghép, có gác xép cao, kệ bếp nấu ăn riêng, sân phơi có mái che và nhà để xe miễn phí.",
                MonthlyRent = 2200000m,
                Deposit = 2000000m,
                Area = 28,
                RoomType = "Chung cư mini",
                Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                Ward = "Tân Thịnh",
                DistanceToCampusKm = 0.8,
                MotorbikeMinutes = 3,
                BusMinutes = 7,
                WalkMinutes = 10,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Tủ lạnh,Máy giặt chung,Gác xép,Bếp riêng",
                ThumbnailUrl = "dotnet_bot.png",
                Status = Constants.ListingStatuses.Available,
                IsVerified = true,
                SecurityScore = 4.7,
                IsFavorite = true
            },
            new RoomListing
            {
                Id = 6,
                Title = "[ĐÃ CHO THUÊ] Phòng khép kín tầng 2 sát giảng đường Z115",
                Description = "Phòng này hiện đã có sinh viên đặt cọc thuê.",
                MonthlyRent = 1400000m,
                Deposit = 1400000m,
                Area = 20,
                RoomType = "Phòng khép kín",
                Address = "Ngõ 18 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                Ward = "Quyết Thắng",
                DistanceToCampusKm = 0.4,
                MotorbikeMinutes = 2,
                BusMinutes = 5,
                WalkMinutes = 5,
                AmenitiesCsv = "Điều hòa,Nóng lạnh,Wifi,Bàn học",
                ThumbnailUrl = "dotnet_bot.png",
                Status = Constants.ListingStatuses.Unavailable,
                IsVerified = true,
                SecurityScore = 4.6,
                IsFavorite = true
            }
        };

        private void UpdateRemainingSeconds(int seconds)
        {
            try
            {
                if (MainThread.IsMainThread)
                {
                    UndoRemainingSeconds = seconds;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() => UndoRemainingSeconds = seconds);
                }
            }
            catch
            {
                UndoRemainingSeconds = seconds;
            }
        }

        private void UpdateShowUndoBanner(bool show)
        {
            try
            {
                if (MainThread.IsMainThread)
                {
                    ShowUndoBanner = show;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() => ShowUndoBanner = show);
                }
            }
            catch
            {
                ShowUndoBanner = show;
            }
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

        private static string TruncateTitle(string? title, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            return title.Length <= maxLength ? title : string.Concat(title.AsSpan(0, maxLength), "...");
        }
    }
}

