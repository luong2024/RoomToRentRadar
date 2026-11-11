using RoomToRentRadar.Abstractions.Alerts;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Abstractions.HouseRules;
using RoomToRentRadar.Abstractions.Reports;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Infrastructure.Persistence.Seed;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Infrastructure.Fakes
{
    public sealed class InMemoryFavoriteRepository : IFavoriteRepository
    {
        private readonly HashSet<(int UserId, int RoomId)> _favorites = new() { (1, 1), (1, 2) };
        private readonly IRoomListingRepository _roomRepo;

        public InMemoryFavoriteRepository(IRoomListingRepository roomRepo)
        {
            _roomRepo = roomRepo;
        }

        public async Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId)
        {
            var roomIds = _favorites.Where(f => f.UserId == userId).Select(f => f.RoomId).ToList();
            var result = new List<RoomListing>();
            foreach (var id in roomIds)
            {
                var room = await _roomRepo.GetByIdAsync(id);
                if (room is not null)
                {
                    room.IsFavorite = true;
                    result.Add(room);
                }
            }
            return result;
        }

        public Task<bool> ToggleFavoriteAsync(int userId, int roomId)
        {
            if (_favorites.Contains((userId, roomId)))
            {
                _favorites.Remove((userId, roomId));
                return Task.FromResult(false);
            }
            _favorites.Add((userId, roomId));
            return Task.FromResult(true);
        }

        public Task<bool> AddFavoriteAsync(int userId, int roomId) =>
            Task.FromResult(_favorites.Add((userId, roomId)));

        public Task<bool> RemoveFavoriteAsync(int userId, int roomId) =>
            Task.FromResult(_favorites.Remove((userId, roomId)));
    }

    public sealed class InMemorySavedSearchAlertRepository : ISavedSearchAlertRepository
    {
        private readonly List<SavedSearchAlert> _alerts = new()
        {
            new SavedSearchAlert
            {
                Id = 1,
                UserId = 1,
                Name = "Phòng khép kín quanh cổng ICTU < 1.8tr",
                MaxPrice = 1800000m,
                MaxDistanceKm = 1.0,
                RoomType = "Phòng khép kín",
                IsEnabled = true
            }
        };

        public Task<List<SavedSearchAlert>> ListByUserAsync(int userId) =>
            Task.FromResult(_alerts.Where(a => a.UserId == userId).ToList());

        public Task<int> SaveAlertAsync(SavedSearchAlert alert)
        {
            if (alert.Id <= 0)
            {
                alert.Id = _alerts.Count == 0 ? 1 : _alerts.Max(a => a.Id) + 1;
                _alerts.Add(alert);
            }
            else
            {
                var idx = _alerts.FindIndex(a => a.Id == alert.Id);
                if (idx >= 0) _alerts[idx] = alert;
            }
            return Task.FromResult(alert.Id);
        }

        public Task<bool> ToggleAlertAsync(int alertId, bool isEnabled)
        {
            var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert is null) return Task.FromResult(false);
            alert.IsEnabled = isEnabled;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAlertAsync(int alertId) =>
            Task.FromResult(_alerts.RemoveAll(a => a.Id == alertId) > 0);
    }

    public sealed class InMemoryHouseRuleRepository : IHouseRuleRepository
    {
        private readonly List<HouseRuleVersion> _versions = IctuSeedData.HouseRules();

        public Task<List<HouseRuleVersion>> GetHistoryByRoomAsync(int roomId) =>
            Task.FromResult(_versions.Where(v => v.RoomId == roomId).OrderByDescending(v => v.VersionNumber).ToList());

        public Task<int> SaveNewVersionAsync(HouseRuleVersion ruleVersion)
        {
            var nextVersion = _versions.Where(v => v.RoomId == ruleVersion.RoomId).Select(v => v.VersionNumber).DefaultIfEmpty(0).Max() + 1;
            ruleVersion.Id = _versions.Count == 0 ? 1 : _versions.Max(v => v.Id) + 1;
            ruleVersion.VersionNumber = nextVersion;
            _versions.Add(ruleVersion);
            return Task.FromResult(ruleVersion.Id);
        }
    }

    public sealed class InMemoryListingReportRepository : IListingReportRepository
    {
        private readonly List<ListingReport> _reports = IctuSeedData.Reports();

        public Task<int> SubmitReportAsync(ListingReport report)
        {
            report.Id = _reports.Count == 0 ? 1 : _reports.Max(r => r.Id) + 1;
            _reports.Add(report);
            return Task.FromResult(report.Id);
        }

        public Task<List<ListingReport>> ListPendingReportsAsync() =>
            Task.FromResult(_reports.Where(r => r.Status == Constants.ReportStatuses.Pending).ToList());
    }
}
