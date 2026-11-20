using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Map
{
    /// <summary>
    /// PageModel for US06 / Task T06.2: Interactive Campus Map &amp; Map Pins / Preview Cards.
    /// Injects <see cref="IRoomListingRepository"/> and <see cref="ICampusMapService"/> directly (DIP).
    /// </summary>
    public partial class RoomMapPageModel : ObservableObject
    {
        private readonly IRoomListingRepository _roomRepository;
        private readonly ICampusMapService _mapService;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial HtmlWebViewSource? MapHtmlSource { get; set; }

        [ObservableProperty]
        public partial RoomListing? SelectedRoom { get; set; }

        [ObservableProperty]
        public partial bool IsPreviewVisible { get; set; }

        [ObservableProperty]
        public partial int TotalPinsCount { get; set; }

        [ObservableProperty]
        public partial string CampusLocationText { get; set; } = $"{Constants.IctuCampusName} ({Constants.IctuLatitude:F4}, {Constants.IctuLongitude:F4})";

        public ObservableCollection<RoomListing> LoadedRooms { get; } = new();

        public RoomMapPageModel(IRoomListingRepository roomRepository, ICampusMapService mapService)
        {
            _roomRepository = roomRepository;
            _mapService = mapService;
        }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            if (LoadedRooms.Count == 0)
            {
                await LoadMapDataAsync();
            }
        }

        [RelayCommand]
        public async Task LoadMapDataAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                var rooms = await _roomRepository.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 50, sortBy: "DistanceAsc");

                LoadedRooms.Clear();
                foreach (var r in rooms)
                {
                    LoadedRooms.Add(r);
                }

                TotalPinsCount = LoadedRooms.Count;
                string html = _mapService.GenerateMapHtml(LoadedRooms, SelectedRoom?.Id);
                MapHtmlSource = new HtmlWebViewSource { Html = html };
            }
            catch
            {
                // Fallback to empty map centered at ICTU
                string emptyHtml = _mapService.GenerateMapHtml(Enumerable.Empty<RoomListing>());
                MapHtmlSource = new HtmlWebViewSource { Html = emptyHtml };
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SelectPin(int roomId)
        {
            var room = LoadedRooms.FirstOrDefault(r => r.Id == roomId);
            if (room is not null)
            {
                SelectedRoom = room;
                IsPreviewVisible = true;
            }
        }

        [RelayCommand]
        public void ClosePreview()
        {
            IsPreviewVisible = false;
            SelectedRoom = null;
        }

        [RelayCommand]
        public async Task ViewDetailAsync()
        {
            if (SelectedRoom is null) return;

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"{Constants.Routes.RoomDetail}?roomId={SelectedRoom.Id}");
            }
        }

        [RelayCommand]
        public async Task RecenterCampusAsync()
        {
            ClosePreview();
            await LoadMapDataAsync();
        }
    }
}
