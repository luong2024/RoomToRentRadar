using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// ViewModel for Sprint 1 US04 (Photo Gallery &amp; 360Â° Room Viewer - Task T04.2).
    /// AC1: The gallery supports at least 8 photos per listing.
    /// AC2: 360Â° photos open in an interactive viewer (Pannellum equirectangular panorama).
    /// AC3: Images are cached and load progressively.
    /// AC4: A placeholder is shown when a listing has no photo.
    /// </summary>
    [QueryProperty(nameof(RoomId), "roomId")]
    [QueryProperty(nameof(InitialTab), "tab")]
    public partial class PhotoGallery360PageModel : ObservableObject
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IRoomListingRepository? _listingRepository;
        private readonly RentalDatabaseService? _databaseService;
        private readonly ILogger<PhotoGallery360PageModel>? _logger;

        [ObservableProperty]
        public partial int RoomId { get; set; }

        [ObservableProperty]
        public partial string InitialTab { get; set; } = "Photos";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPhotosTabSelected))]
        [NotifyPropertyChangedFor(nameof(Is360TabSelected))]
        public partial string SelectedTab { get; set; } = "Photos"; // "Photos" or "360"

        public bool IsPhotosTabSelected => SelectedTab == "Photos";
        public bool Is360TabSelected => SelectedTab == "360";

        [ObservableProperty]
        public partial string RoomTitle { get; set; } = "Phòng trọ sinh viên ICTU";

        [ObservableProperty]
        public partial string MonthlyRentFormatted { get; set; } = string.Empty;

        public ObservableCollection<string> Photos { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPhotoCounter))]
        public partial int CurrentPhotoIndex { get; set; }

        public string CurrentPhotoCounter => Photos.Count > 0
            ? $"{CurrentPhotoIndex + 1} / {Photos.Count}"
            : "0 / 0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPhotos))]
        public partial bool IsEmptyPhotos { get; set; } = false;

        public bool HasPhotos => !IsEmptyPhotos && Photos.Count > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPanorama360))]
        public partial string Panorama360Url { get; set; } = string.Empty;

        public bool HasPanorama360 => !string.IsNullOrWhiteSpace(Panorama360Url);

        [ObservableProperty]
        public partial string PanoramaHtmlContent { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public PhotoGallery360PageModel(IServiceProvider serviceProvider, ILogger<PhotoGallery360PageModel>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _listingRepository = serviceProvider.GetService<IRoomListingRepository>();
            _databaseService = serviceProvider.GetService<RentalDatabaseService>();
        }

        /// <summary>
        /// Secondary constructor for Unit Testing without relying on MAUI DI container.
        /// </summary>
        public PhotoGallery360PageModel(
            IRoomListingRepository? listingRepository,
            RentalDatabaseService? databaseService = null,
            ILogger<PhotoGallery360PageModel>? logger = null)
        {
            _listingRepository = listingRepository;
            _databaseService = databaseService;
            _logger = logger;
            _serviceProvider = null;
        }

        partial void OnRoomIdChanged(int value)
        {
            if (value > 0)
            {
                _ = LoadMediaAsync(value);
            }
        }

        partial void OnInitialTabChanged(string value)
        {
            if (string.Equals(value, "360", StringComparison.OrdinalIgnoreCase))
            {
                SelectedTab = "360";
            }
            else
            {
                SelectedTab = "Photos";
            }
        }

        [RelayCommand]
        public void SelectTab(string tab)
        {
            SelectedTab = string.Equals(tab, "360", StringComparison.OrdinalIgnoreCase) ? "360" : "Photos";
        }

        /// <summary>
        /// Loads room media, gallery photos, and 360 panorama URL.
        /// </summary>
        [RelayCommand]
        public async Task LoadMediaAsync(int roomId)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                RoomListing? room = null;

                if (_listingRepository != null)
                {
                    room = await _listingRepository.GetByIdAsync(roomId);
                }
                else if (_databaseService != null)
                {
                    room = await LoadRoomFromDatabaseAsync(roomId);
                }

                room ??= GetFallbackRoom(roomId);
                ApplyRoomData(room);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load media for room {RoomId}", roomId);
                var fallback = GetFallbackRoom(roomId);
                ApplyRoomData(fallback);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ApplyRoomData(RoomListing room)
        {
            RoomTitle = string.IsNullOrWhiteSpace(room.Title) ? "Phòng trọ sinh viên ICTU" : room.Title;
            MonthlyRentFormatted = $"{room.MonthlyRent:N0} đ/tháng";
            Panorama360Url = room.Panorama360Url ?? string.Empty;

            Photos.Clear();

            // US04 AC1: Parse photos list from PhotosJson, supporting 8+ photos
            List<string>? parsedPhotos = null;
            if (!string.IsNullOrWhiteSpace(room.PhotosJson))
            {
                try
                {
                    parsedPhotos = JsonSerializer.Deserialize<List<string>>(room.PhotosJson);
                }
                catch
                {
                }
            }

            if (parsedPhotos != null && parsedPhotos.Count > 0)
            {
                foreach (var photo in parsedPhotos)
                {
                    Photos.Add(photo);
                }
                IsEmptyPhotos = false;
            }
            else if (!string.IsNullOrWhiteSpace(room.ThumbnailUrl))
            {
                Photos.Add(room.ThumbnailUrl);
                IsEmptyPhotos = false;
            }
            else
            {
                // US04 AC4: Placeholder when room has no photo
                IsEmptyPhotos = true;
            }

            CurrentPhotoIndex = 0;

            // US04 AC2: Generate interactive 360 viewer HTML
            PanoramaHtmlContent = GeneratePannellumHtml(Panorama360Url, RoomTitle);
        }

        [RelayCommand]
        public void NextPhoto()
        {
            if (Photos.Count == 0) return;
            if (CurrentPhotoIndex < Photos.Count - 1)
            {
                CurrentPhotoIndex++;
            }
            else
            {
                CurrentPhotoIndex = 0; // Wrap around
            }
        }

        [RelayCommand]
        public void PreviousPhoto()
        {
            if (Photos.Count == 0) return;
            if (CurrentPhotoIndex > 0)
            {
                CurrentPhotoIndex--;
            }
            else
            {
                CurrentPhotoIndex = Photos.Count - 1; // Wrap around
            }
        }

        [RelayCommand]
        public void SelectPhoto(int index)
        {
            if (index >= 0 && index < Photos.Count)
            {
                CurrentPhotoIndex = index;
            }
        }

        [RelayCommand]
        public async Task GoBackAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch
            {
            }
        }

        public static string GeneratePannellumHtml(string panoramaUrl, string title)
        {
            if (string.IsNullOrWhiteSpace(panoramaUrl))
            {
                return @"<!DOCTYPE html>
                <html><head><meta charset='utf-8'/><style>body { background: #121214; color: #9E9E9E; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; font-family: sans-serif; text-align: center; }</style></head>
                <body><div><h3>Chưa có ảnh 360°</h3><p>Chủ trọ chưa cập nhật ảnh toàn cảnh 360° cho phòng này.</p></div></body></html>";
            }

            string safeUrl = panoramaUrl.Replace("'", "%27");
            string safeTitle = title.Replace("'", "\\'");

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'/>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/pannellum@2.5.6/build/pannellum.css'/>
    <script type='text/javascript' src='https://cdn.jsdelivr.net/npm/pannellum@2.5.6/build/pannellum.js'></script>
    <style>
        html, body, #panorama {{ width: 100%; height: 100%; margin: 0; padding: 0; background: #000; overflow: hidden; }}
        .pnlm-load-button {{ background-color: rgba(13, 71, 161, 0.8) !important; }}
    </style>
</head>
<body>
    <div id='panorama'></div>
    <script>
        pannellum.viewer('panorama', {{
            type: 'equirectangular',
            panorama: '{safeUrl}',
            autoLoad: true,
            autoRotate: -2,
            compass: true,
            showControls: true,
            title: '{safeTitle}',
            author: 'ICTU Radar 360°'
        }});
    </script>
</body>
</html>";
        }

        private async Task<RoomListing?> LoadRoomFromDatabaseAsync(int roomId)
        {
            if (_databaseService == null) return null;

            await using var connection = await _databaseService.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM RoomListing WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", roomId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RoomListing
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    MonthlyRent = (decimal)reader.GetDouble(reader.GetOrdinal("MonthlyRent")),
                    ThumbnailUrl = reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
                    PhotosJson = reader.GetString(reader.GetOrdinal("PhotosJson")),
                    Panorama360Url = reader.GetString(reader.GetOrdinal("Panorama360Url"))
                };
            }
            return null;
        }

        private static RoomListing GetFallbackRoom(int roomId) => new()
        {
            Id = roomId > 0 ? roomId : 1,
            Title = "Phòng khép kín ban công thoáng sát cổng chính ICTU",
            MonthlyRent = 1500000m,
            ThumbnailUrl = "dotnet_bot.png",
            PhotosJson = "[\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\",\"dotnet_bot.png\"]",
            Panorama360Url = "https://pannellum.org/images/alma.jpg"
        };
    }
}

