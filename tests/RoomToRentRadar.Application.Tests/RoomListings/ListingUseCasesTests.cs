namespace RoomToRentRadar.Application.Tests.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using VerificationStatus = RoomToRentRadar.Domain.Enums.VerificationStatus;

public sealed class ListingUseCasesTests
{
    private static readonly Guid OwnerId = Guid.Parse("c95a1a7d-9727-4db3-a2a4-637c99c8dba6");
    private FakeRoomRepository _repository = null!;
    private FakeImageStorage _storage = null!;
    private FakeRoomListingRepository _listingRepository = null!;
    private FakeCacheInvalidator _cacheInvalidator = null!;
    private UpdateListingUseCase _updateListing = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeRoomRepository();
        _storage = new FakeImageStorage();
        _listingRepository = new FakeRoomListingRepository();
        _cacheInvalidator = new FakeCacheInvalidator();
        _updateListing = new UpdateListingUseCase(_repository, _storage, _listingRepository, _cacheInvalidator);
    }

    [Test]
    public async Task CreateListing_ValidRequest_SetsTrustedOwnerAndPendingReview()
    {
        var result = await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, ValidRequest(3));
        var created = await _repository.GetByIdAsync(result.ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.LandlordId, Is.EqualTo(OwnerId.ToString("D")));
            Assert.That(created.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(created.IsVerified, Is.False);
            Assert.That(created.Deposit, Is.Zero);
            Assert.That(created.ImageUrls, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task CreateListing_MoreThanThreePhotos_PersistsAllValidPhotos()
    {
        var result = await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, ValidRequest(5));

        Assert.That((await _repository.GetByIdAsync(result.ListingId))!.ImageUrls, Has.Count.EqualTo(5));
    }

    [Test]
    public void CreateListing_PartialImageStorageFailure_CleansUploadedFilesAndDoesNotPersistListing()
    {
        _storage.FailOnStoreCall = 2;

        Assert.ThrowsAsync<IOException>(async () =>
            await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, ValidRequest(3)));

        Assert.Multiple(() =>
        {
            Assert.That(_storage.StoredUrls, Has.Count.EqualTo(1));
            Assert.That(_storage.DeletedUrls, Is.EqualTo(_storage.StoredUrls));
            Assert.That(_repository.Rooms, Is.Empty);
        });
    }

    [TestCase("title")]
    [TestCase("address")]
    [TestCase("monthlyRentVnd")]
    [TestCase("depositVnd")]
    [TestCase("areaM2")]
    [TestCase("roomType")]
    public void CreateListing_MissingRequiredField_ReturnsFieldValidationError(string field)
    {
        var request = ValidRequest(3);
        request = field switch
        {
            "title" => request with { Title = "  " },
            "address" => request with { Address = null },
            "monthlyRentVnd" => request with { MonthlyRentVnd = 0 },
            "depositVnd" => request with { DepositVnd = -1 },
            "areaM2" => request with { AreaM2 = 0 },
            _ => request with { RoomType = "unknown" }
        };

        var exception = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, request));
        Assert.That(exception!.Errors.ContainsKey(field), Is.True);
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CreateListing_FewerThanThreePhotos_IsRejectedBeforeStorage(int count)
    {
        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, ValidRequest(count)));
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [Test]
    public void CreateListing_MoreThanTwentyPhotos_IsRejectedBeforeStorage()
    {
        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await new CreateListingUseCase(_repository, _storage).ExecuteAsync(OwnerId, ValidRequest(21)));
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [Test]
    public async Task UpdateListing_ValidOwnerRetainsExistingImagesAndAddsPhoto()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        listing.ConcurrencyToken = "etag-before-upload";
        await _repository.CreateAsync(listing);
        var request = ValidRequest(1) with { ImageUrlsToRetain = ["one", "two"] };

        var result = await _updateListing.ExecuteAsync(OwnerId, listing.Id, request);
        var updated = await _repository.GetByIdAsync(listing.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.ImageUrls, Has.Count.EqualTo(3));
            Assert.That(result.ImageUrls, Does.Contain("one"));
            Assert.That(updated!.Title, Is.EqualTo("Updated title"));
            Assert.That(updated.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(updated.IsVerified, Is.False);
            Assert.That(updated.ConcurrencyToken, Is.EqualTo("etag-before-upload"));
            Assert.That(_storage.DeletedUrls, Is.EqualTo(new[] { "three" }));
        });
    }

    [Test]
    public async Task UpdateListing_WhenRetainImageFieldIsOmitted_KeepsExistingGallery()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        await _repository.CreateAsync(listing);

        var result = await _updateListing.ExecuteAsync(
            OwnerId, listing.Id, ValidRequest(0));

        Assert.Multiple(() =>
        {
            Assert.That(result.ImageUrls, Is.EqualTo(new[] { "one", "two", "three" }));
            Assert.That(_storage.StoredUrls, Is.Empty);
            Assert.That(_storage.DeletedUrls, Is.Empty);
        });
    }

    [Test]
    public async Task UpdateListing_FinalGalleryAboveTwenty_IsRejectedBeforeStorage()
    {
        var listing = ExistingListing(OwnerId, Enumerable.Range(0, 20).Select(i => $"image-{i}").ToList());
        await _repository.CreateAsync(listing);

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _updateListing.ExecuteAsync(
                OwnerId, listing.Id, ValidRequest(1) with { ImageUrlsToRetain = listing.ImageUrls }));
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [Test]
    public async Task UpdateListing_AnotherLandlordCannotModifyListing()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        await _repository.CreateAsync(listing);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _updateListing.ExecuteAsync(Guid.NewGuid(), listing.Id, ValidRequest(0)));
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [Test]
    public async Task UpdateListing_FinalGalleryBelowThree_IsRejectedWithoutChangingListing()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        await _repository.CreateAsync(listing);
        var request = ValidRequest(0) with { ImageUrlsToRetain = ["one", "two"] };

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _updateListing.ExecuteAsync(OwnerId, listing.Id, request));
        var unchanged = await _repository.GetByIdAsync(listing.Id);
        Assert.That(unchanged!.ImageUrls, Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public async Task UpdateListing_CannotAttachImageFromAnotherListing()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        await _repository.CreateAsync(listing);

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _updateListing.ExecuteAsync(OwnerId, listing.Id,
                ValidRequest(0) with { ImageUrlsToRetain = ["https://other-owner.test/private.png"] }));
        Assert.That(_storage.StoredUrls, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateListing_SaveFailureCleansNewImagesAndPreservesExistingData(bool concurrencyConflict)
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        await _repository.CreateAsync(listing);
        var projection = CreatePublicProjection(listing.Id);
        await _listingRepository.AddAsync(projection);
        _repository.UpdateError = concurrencyConflict
            ? new ConcurrencyConflictException("Listing was changed.")
            : new InvalidOperationException("Persistence failed.");

        Assert.ThrowsAsync(_repository.UpdateError.GetType(), async () =>
            await _updateListing.ExecuteAsync(OwnerId, listing.Id,
                ValidRequest(1) with { ImageUrlsToRetain = ["one", "two", "three"] }));

        var persisted = await _repository.GetByIdAsync(listing.Id);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Title, Is.EqualTo("Original title"));
            Assert.That(persisted.ImageUrls, Is.EqualTo(new[] { "one", "two", "three" }));
            Assert.That(_storage.StoredUrls, Has.Count.EqualTo(1));
            Assert.That(_storage.DeletedUrls, Is.EqualTo(_storage.StoredUrls));
            Assert.That(projection.IsDisplayable, Is.True);
            Assert.That(_cacheInvalidator.CampusIds, Is.Empty);
        });
    }

    [Test]
    public async Task UpdateListing_WithMultiplePublicProjections_HidesAndInvalidatesEveryCampus()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        listing.Status = RoomStatus.Available;
        listing.IsVerified = true;
        await _repository.CreateAsync(listing);
        var first = CreatePublicProjection(listing.Id, "campus-ictu");
        var second = CreatePublicProjection(listing.Id, "campus-second");
        await _listingRepository.AddAsync(first);
        await _listingRepository.AddAsync(second);

        await _updateListing.ExecuteAsync(OwnerId, listing.Id, ValidRequest(0));

        Assert.Multiple(() =>
        {
            Assert.That(new[] { first.Status, second.Status }, Is.All.EqualTo(ListingRoomStatus.Hidden));
            Assert.That(new[] { first.VerificationStatus, second.VerificationStatus }, Is.All.EqualTo(VerificationStatus.Unverified));
            Assert.That(_cacheInvalidator.CampusIds, Is.EquivalentTo(new[] { first.CampusId, second.CampusId }));
        });
    }

    [Test]
    public async Task UpdateListing_VisibilityFailureAfterSave_PreservesNewGalleryAndCanRetry()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        listing.Status = RoomStatus.Available;
        listing.IsVerified = true;
        await _repository.CreateAsync(listing);
        var projection = CreatePublicProjection(listing.Id);
        await _listingRepository.AddAsync(projection);
        _listingRepository.HideError = new IOException("Projection storage unavailable.");

        Assert.ThrowsAsync<IOException>(() => _updateListing.ExecuteAsync(OwnerId, listing.Id, ValidRequest(3) with { ImageUrlsToRetain = [] }));

        var saved = await _repository.GetByIdAsync(listing.Id);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(saved.ImageUrls, Is.EqualTo(_storage.StoredUrls));
            Assert.That(saved.ImageUrls, Has.Count.EqualTo(3));
            Assert.That(_storage.DeletedUrls, Is.Empty);
        });

        _listingRepository.HideError = null;
        var retried = await _updateListing.ExecuteAsync(OwnerId, listing.Id, ValidRequest(0));
        Assert.Multiple(() =>
        {
            Assert.That(projection.IsDisplayable, Is.False);
            Assert.That(retried.ImageUrls, Is.EqualTo(saved!.ImageUrls));
            Assert.That(_cacheInvalidator.CampusIds, Is.EqualTo(new[] { projection.CampusId }));
        });
    }

    [Test]
    public async Task UnpublishListing_OwnerCanUnpublishRepeatedlyAndDataIsPreserved()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        listing.Status = RoomStatus.Available;
        listing.IsVerified = true;
        await _repository.CreateAsync(listing);
        var publicProjection = CreatePublicProjection(listing.Id);
        await _listingRepository.AddAsync(publicProjection);
        var useCase = new UnpublishListingUseCase(_repository, _listingRepository, _cacheInvalidator);

        await useCase.ExecuteAsync(OwnerId, listing.Id);
        await useCase.ExecuteAsync(OwnerId, listing.Id);
        var saved = await _repository.GetByIdAsync(listing.Id);

        Assert.Multiple(() =>
        {
            Assert.That(saved!.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(saved.IsVerified, Is.False);
            Assert.That(saved.ImageUrls, Is.EqualTo(new[] { "one", "two", "three" }));
            Assert.That(saved.Title, Is.EqualTo("Original title"));
            Assert.That(publicProjection.Status, Is.EqualTo(ListingRoomStatus.Hidden));
            Assert.That(publicProjection.VerificationStatus, Is.EqualTo(VerificationStatus.Unverified));
            Assert.That(_cacheInvalidator.CampusIds, Is.EqualTo(new[] { "campus-ictu", "campus-ictu" }));
        });
    }

    [Test]
    public async Task UnpublishListing_AnotherLandlordCannotUnpublishListing()
    {
        var listing = ExistingListing(OwnerId, ["one", "two", "three"]);
        listing.Status = RoomStatus.Available;
        await _repository.CreateAsync(listing);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await new UnpublishListingUseCase(_repository, _listingRepository, _cacheInvalidator).ExecuteAsync(Guid.NewGuid(), listing.Id));
        Assert.That((await _repository.GetByIdAsync(listing.Id))!.Status, Is.EqualTo(RoomStatus.Available));
    }

    private static RoomListing CreatePublicProjection(string id, string campusId = "campus-ictu") => new(
        id, "Original title", "Description", new Money(2_000_000m, "VND"), 20,
        "Old address", new GeoLocation(21.58, 105.8), ListingRoomStatus.Available,
        VerificationStatus.Verified, campusId, "thumbnail.jpg");

    private static ListingWriteRequest ValidRequest(int photoCount) => new(
        "Updated title", "10 Main Street", 2500000m, 0m, 24, "BoardingRoom", "Description",
        Enumerable.Range(0, photoCount).Select(i => new ListingImageUpload(
            () => new MemoryStream([1, 2, (byte)i]), $"image-{i}.jpg", "image/jpeg", 3)).ToList());

    private static Room ExistingListing(Guid ownerId, List<string> photos) => new()
    {
        Id = "listing-1", LandlordId = ownerId.ToString("D"), Title = "Original title", Address = "Old address",
        Price = 2000000, Deposit = 0, Area = 20, RoomType = "BoardingRoom", ImageUrls = photos,
        ThumbnailUrl = photos[0], Status = RoomStatus.Pending, CreatedAt = DateTime.UtcNow
    };

    private sealed class FakeImageStorage : IListingImageStorageService
    {
        private int _storeCalls;
        public List<string> StoredUrls { get; } = [];
        public List<string> DeletedUrls { get; } = [];
        public int FailOnStoreCall { get; set; }

        public Task<string> StoreAsync(Guid ownerId, string listingId, Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
        {
            _storeCalls++;
            if (_storeCalls == FailOnStoreCall) throw new IOException("Storage failed.");
            var url = $"https://images.test/{ownerId:D}/{listingId}/{Guid.NewGuid():N}.jpg";
            StoredUrls.Add(url);
            return Task.FromResult(url);
        }

        public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            DeletedUrls.Add(imageUrl);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly Dictionary<string, Room> _rooms = new(StringComparer.Ordinal);
        public IReadOnlyCollection<Room> Rooms => _rooms.Values;
        public Exception? UpdateError { get; set; }

        public Task<Room> CreateAsync(Room room, CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryAdd(room.Id, room)) throw new InvalidOperationException("Duplicate listing.");
            return Task.FromResult(room);
        }

        public Task UpdateAsync(Room room, CancellationToken cancellationToken = default)
        {
            if (UpdateError is not null) throw UpdateError;
            _rooms[room.Id] = room;
            return Task.CompletedTask;
        }

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            _rooms.TryGetValue(id, out var room);
            return Task.FromResult(room);
        }

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>([]);

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((_rooms.Values.ToList(), _rooms.Count));
    }

    private sealed class FakeRoomListingRepository : IRoomListingVisibilityWriter
    {
        private readonly Dictionary<(string RoomId, string CampusId), RoomListing> _listings = new();
        public Exception? HideError { get; set; }

        public Task<IReadOnlyList<string>> HideAsync(string roomId, CancellationToken cancellationToken = default)
        {
            if (HideError is not null) throw HideError;

            var campuses = new List<string>();
            foreach (var listing in _listings.Values.Where(listing => listing.Id == roomId))
            {
                listing.UpdateStatus(ListingRoomStatus.Hidden);
                listing.UpdateVerificationStatus(VerificationStatus.Unverified);
                campuses.Add(listing.CampusId);
            }
            return Task.FromResult<IReadOnlyList<string>>(campuses);
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _listings[(roomListing.Id, roomListing.CampusId)] = roomListing;
            return Task.CompletedTask;
        }

    }

    private sealed class FakeCacheInvalidator : IRoomListingCacheInvalidator
    {
        public List<string> CampusIds { get; } = [];

        public Task InvalidateCampusAsync(string campusId, CancellationToken cancellationToken = default)
        {
            CampusIds.Add(campusId);
            return Task.CompletedTask;
        }
    }
}
