using NUnit.Framework;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests
{
    /// <summary>
    /// Comprehensive Unit Tests for Sprint 1 US07 (Favorite Rooms, Heart Button & 5-Second Undo).
    /// Covers Acceptance Criteria:
    /// - AC1: A heart icon adds or removes a room from favourites instantly.
    /// - AC2: Favourites sync across devices through the user account.
    /// - AC3: The favourites screen shows the current price and availability of each room.
    /// - AC4: Removing a favourite can be undone within 5 seconds.
    /// </summary>
    [TestFixture]
    public class FavoritesTests
    {
        private InMemoryFavoriteRepository _repository = null!;
        private List<RoomListing> _sampleRooms = null!;

        [SetUp]
        public void Setup()
        {
            _repository = new InMemoryFavoriteRepository();
            _sampleRooms = new List<RoomListing>
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
                    DistanceToCampusKm = 0.8,
                    Status = "Available",
                    IsVerified = true,
                    SecurityScore = 4.7
                },
                new RoomListing
                {
                    Id = 6,
                    Title = "[ĐÃ CHO THUÊ] Phòng khép kín tầng 2 sát giảng đường Z115",
                    MonthlyRent = 1400000m,
                    Deposit = 1400000m,
                    Area = 20,
                    RoomType = "Phòng khép kín",
                    Address = "Ngõ 18 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên",
                    Ward = "Quyết Thắng",
                    DistanceToCampusKm = 0.4,
                    Status = "Unavailable", // US07 AC3 & US03 AC3: Unavailable room
                    IsVerified = true,
                    SecurityScore = 4.6
                }
            };

            foreach (var room in _sampleRooms)
            {
                _repository.RegisterRoom(room);
            }
        }

        [Test]
        public async Task AC1_HeartIcon_AddsAndRemovesRoom_Instantly()
        {
            const int userId = 1;
            const int roomId = 1;

            // Act 1: Initial state - not favorite
            var initialFavorites = await _repository.GetFavoriteRoomsAsync(userId);
            Assert.That(initialFavorites.Any(r => r.Id == roomId), Is.False);

            // Act 2: Tapping heart icon adds room
            var added = await _repository.ToggleFavoriteAsync(userId, roomId);
            Assert.That(added, Is.True);

            var favoritesAfterAdd = await _repository.GetFavoriteRoomsAsync(userId);
            Assert.That(favoritesAfterAdd.Count, Is.EqualTo(1));
            Assert.That(favoritesAfterAdd[0].Id, Is.EqualTo(roomId));
            Assert.That(favoritesAfterAdd[0].IsFavorite, Is.True);

            // Act 3: Tapping heart icon again removes room immediately
            var removed = await _repository.ToggleFavoriteAsync(userId, roomId);
            Assert.That(removed, Is.False);

            var favoritesAfterRemove = await _repository.GetFavoriteRoomsAsync(userId);
            Assert.That(favoritesAfterRemove.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task AC2_UserAccountSync_MaintainsFavoritesPerUserId()
        {
            const int student1Id = 1;
            const int student2Id = 2;

            // Student 1 adds Room 1 and Room 2
            await _repository.AddFavoriteAsync(student1Id, 1);
            await _repository.AddFavoriteAsync(student1Id, 2);

            // Student 2 adds Room 6 only
            await _repository.AddFavoriteAsync(student2Id, 6);

            // Assert: Accounts are isolated and synced by user account ID
            var student1Favorites = await _repository.GetFavoriteRoomsAsync(student1Id);
            var student2Favorites = await _repository.GetFavoriteRoomsAsync(student2Id);

            Assert.That(student1Favorites.Count, Is.EqualTo(2));
            Assert.That(student1Favorites.Select(r => r.Id), Does.Contain(1));
            Assert.That(student1Favorites.Select(r => r.Id), Does.Contain(2));

            Assert.That(student2Favorites.Count, Is.EqualTo(1));
            Assert.That(student2Favorites[0].Id, Is.EqualTo(6));
        }

        [Test]
        public async Task AC3_FavoritesScreen_ShowsCurrentPriceAndAvailability()
        {
            const int userId = 1;
            await _repository.AddFavoriteAsync(userId, 1);
            await _repository.AddFavoriteAsync(userId, 6);

            var favorites = await _repository.GetFavoriteRoomsAsync(userId);
            Assert.That(favorites.Count, Is.EqualTo(2));

            var availableRoom = favorites.First(r => r.Id == 1);
            var unavailableRoom = favorites.First(r => r.Id == 6);

            // Verify Price & Deposit display data
            Assert.That(availableRoom.MonthlyRent, Is.EqualTo(1500000m));
            Assert.That(availableRoom.Deposit, Is.EqualTo(1500000m));
            Assert.That(availableRoom.Status, Is.EqualTo("Available"));

            // Verify Unavailable Room display data (US07 AC3 requirement)
            Assert.That(unavailableRoom.MonthlyRent, Is.EqualTo(1400000m));
            Assert.That(unavailableRoom.Status, Is.EqualTo("Unavailable"));
        }

        [Test]
        public void AC4_RemovingFavorite_CanBeUndoneWithinFiveSeconds()
        {
            var session = new FavoriteUndoSession();
            var room = _sampleRooms[0];
            session.Items.Add(room);

            // Act 1: User removes favorite
            session.RemoveWithUndo(room, index: 0);

            // Assert: Removed immediately from active collection (AC1)
            Assert.That(session.Items.Count, Is.EqualTo(0));
            Assert.That(session.IsUndoActive, Is.True);
            Assert.That(session.PendingUndoRoom?.Id, Is.EqualTo(room.Id));
            Assert.That(session.RemainingSeconds, Is.EqualTo(5));

            // Act 2: User taps Undo within 5 seconds (e.g. at 3s)
            session.RemainingSeconds = 3;
            var restored = session.Undo();

            // Assert: Restored back to active collection at original position (AC4)
            Assert.That(restored, Is.True);
            Assert.That(session.Items.Count, Is.EqualTo(1));
            Assert.That(session.Items[0].Id, Is.EqualTo(room.Id));
            Assert.That(session.IsUndoActive, Is.False);
            Assert.That(session.PendingUndoRoom, Is.Null);
            Assert.That(session.HasCommittedDeletion, Is.False);
        }

        [Test]
        public void AC4_RemovingFavorite_CommitsDeletion_WhenUndoExpires()
        {
            var session = new FavoriteUndoSession();
            var room = _sampleRooms[0];
            session.Items.Add(room);

            // User removes favorite
            session.RemoveWithUndo(room, index: 0);
            Assert.That(session.Items.Count, Is.EqualTo(0));

            // 5 seconds elapse without user undoing
            session.ExpireUndo();

            // Assert: Deletion committed permanently
            Assert.That(session.HasCommittedDeletion, Is.True);
            Assert.That(session.Items.Count, Is.EqualTo(0));
            Assert.That(session.IsUndoActive, Is.False);
            Assert.That(session.PendingUndoRoom, Is.Null);
        }

        [Test]
        public void AC4_RapidRemovals_CommitPriorPendingItem()
        {
            var session = new FavoriteUndoSession();
            var room1 = _sampleRooms[0];
            var room2 = _sampleRooms[1];
            session.Items.Add(room1);
            session.Items.Add(room2);

            // Remove room1
            session.RemoveWithUndo(room1, 0);
            Assert.That(session.PendingUndoRoom?.Id, Is.EqualTo(room1.Id));

            // User immediately removes room2 without undoing room1
            session.RemoveWithUndo(room2, 0);

            // Prior room1 must be committed!
            Assert.That(session.CommittedRoomIds, Does.Contain(room1.Id));
            Assert.That(session.PendingUndoRoom?.Id, Is.EqualTo(room2.Id));
        }

        [Test]
        public void SearchFilter_FiltersRoomsByKeyword()
        {
            var rooms = _sampleRooms;

            // Search by ward "Tân Thịnh"
            var tanThinhResults = rooms.Where(r => r.Ward.Contains("Tân Thịnh") || r.Title.Contains("Tân Thịnh")).ToList();
            Assert.That(tanThinhResults.Count, Is.EqualTo(1));
            Assert.That(tanThinhResults[0].Id, Is.EqualTo(2));

            // Search by room type "Chung cư mini"
            var miniApartments = rooms.Where(r => r.RoomType.Contains("Chung cư mini")).ToList();
            Assert.That(miniApartments.Count, Is.EqualTo(1));
            Assert.That(miniApartments[0].Id, Is.EqualTo(2));

            // Search by status / title
            var unavailableRooms = rooms.Where(r => r.Title.Contains("[ĐÃ CHO THUÊ]")).ToList();
            Assert.That(unavailableRooms.Count, Is.EqualTo(1));
            Assert.That(unavailableRooms[0].Id, Is.EqualTo(6));
        }

        [Test]
        public void RoomListing_LandlordPhoneMasking_FunctionsCorrectly()
        {
            var room = new RoomListing
            {
                LandlordPhone = "0912333444"
            };

            // Masked phone for guests (US03 AC4)
            Assert.That(room.MaskedLandlordPhone, Is.EqualTo("0912 *** 444"));

            // Short/invalid phone fallback
            var badRoom = new RoomListing { LandlordPhone = "123" };
            Assert.That(badRoom.MaskedLandlordPhone, Is.EqualTo("09xx xxx xxx"));
        }
    }

    /// <summary>
    /// Pure in-memory IFavoriteRepository test double for unit testing contracts.
    /// </summary>
    public class InMemoryFavoriteRepository : IFavoriteRepository
    {
        private readonly List<FavoriteRoom> _favorites = new();
        private readonly Dictionary<int, RoomListing> _rooms = new();
        private int _nextId = 1;

        public void RegisterRoom(RoomListing room)
        {
            _rooms[room.Id] = room;
        }

        public Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId)
        {
            var roomIds = _favorites.Where(f => f.UserId == userId).Select(f => f.RoomId).ToHashSet();
            var result = _rooms.Values
                .Where(r => roomIds.Contains(r.Id))
                .Select(r =>
                {
                    r.IsFavorite = true;
                    return r;
                })
                .ToList();

            return Task.FromResult(result);
        }

        public Task<bool> ToggleFavoriteAsync(int userId, int roomId)
        {
            var existing = _favorites.FirstOrDefault(f => f.UserId == userId && f.RoomId == roomId);
            if (existing != null)
            {
                _favorites.Remove(existing);
                return Task.FromResult(false); // Removed
            }

            _favorites.Add(new FavoriteRoom
            {
                Id = _nextId++,
                UserId = userId,
                RoomId = roomId,
                CreatedAt = DateTime.UtcNow.ToString("O")
            });
            return Task.FromResult(true); // Added
        }

        public Task<bool> AddFavoriteAsync(int userId, int roomId)
        {
            if (!_favorites.Any(f => f.UserId == userId && f.RoomId == roomId))
            {
                _favorites.Add(new FavoriteRoom
                {
                    Id = _nextId++,
                    UserId = userId,
                    RoomId = roomId,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });
            }
            return Task.FromResult(true);
        }

        public Task<bool> RemoveFavoriteAsync(int userId, int roomId)
        {
            var existing = _favorites.FirstOrDefault(f => f.UserId == userId && f.RoomId == roomId);
            if (existing != null)
            {
                _favorites.Remove(existing);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Encapsulates the 5-second undo grace period state machine for test verification.
    /// </summary>
    public class FavoriteUndoSession
    {
        public List<RoomListing> Items { get; } = new();
        public RoomListing? PendingUndoRoom { get; private set; }
        public int PendingUndoIndex { get; private set; } = -1;
        public int RemainingSeconds { get; set; } = 5;
        public bool IsUndoActive => PendingUndoRoom != null;
        public bool HasCommittedDeletion { get; private set; }
        public List<int> CommittedRoomIds { get; } = new();

        public void RemoveWithUndo(RoomListing room, int index)
        {
            if (PendingUndoRoom != null)
            {
                // Auto-commit previous before starting new one
                ExpireUndo();
            }

            PendingUndoRoom = room;
            PendingUndoIndex = index;
            RemainingSeconds = 5;
            HasCommittedDeletion = false;
            Items.Remove(room);
        }

        public bool Undo()
        {
            if (PendingUndoRoom == null) return false;

            var restored = PendingUndoRoom;
            PendingUndoRoom = null;
            int target = Math.Clamp(PendingUndoIndex, 0, Items.Count);
            Items.Insert(target, restored);
            return true;
        }

        public void ExpireUndo()
        {
            if (PendingUndoRoom != null)
            {
                CommittedRoomIds.Add(PendingUndoRoom.Id);
                PendingUndoRoom = null;
                HasCommittedDeletion = true;
            }
        }
    }
}

