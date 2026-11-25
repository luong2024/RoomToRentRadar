using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Home
{
    /// <summary>
    /// Baseline Home PageModel for RoomToRentRadar (ICTU).
    /// Demonstrates clean Constructor Dependency Injection (DIP): injects <see cref="IRoomListingRepository"/>
    /// and <see cref="IAuthService"/> directly without any Service Locator or inline mock data.
    /// </summary>
    public partial class HomePageModel : ObservableObject
    {
        private readonly IRoomListingRepository _roomRepository;
        private readonly IAuthService _authService;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string CampusBanner { get; set; } = $"{Constants.IctuCampusName} ({Constants.IctuLatitude:F4}, {Constants.IctuLongitude:F4})";

        [ObservableProperty]
        public partial int VerifiedRoomCount { get; set; }

        public ObservableCollection<RoomListing> FeaturedRooms { get; } = new();

        public HomePageModel(
            IRoomListingRepository roomRepository,
            IAuthService authService,
            IErrorHandler errorHandler)
        {
            _roomRepository = roomRepository;
            _authService = authService;
            _errorHandler = errorHandler;
        }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                var rooms = await _roomRepository.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 6, sortBy: "DistanceAsc");
                FeaturedRooms.Clear();
                foreach (var room in rooms)
                {
                    FeaturedRooms.Add(room);
                }
                VerifiedRoomCount = FeaturedRooms.Count;
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
