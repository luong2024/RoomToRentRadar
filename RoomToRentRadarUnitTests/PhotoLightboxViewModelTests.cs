using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class PhotoLightboxViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");

    [Test]
    public void SetPhotos_WhenPhotosProvided_StartsOnTheFirstSlide()
    {
        var lightbox = new PhotoLightboxViewModel();
        var photos = CreatePhotos(3);

        lightbox.SetPhotos(photos);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.Photos, Has.Count.EqualTo(3));
            Assert.That(lightbox.Photos.Select(photo => photo.Id), Is.EqualTo(photos.Select(photo => photo.Id)));
            Assert.That(lightbox.HasPhotos, Is.True);
            Assert.That(lightbox.CurrentIndex, Is.Zero);
            Assert.That(lightbox.CurrentPhoto, Is.SameAs(photos[0]));
            Assert.That(lightbox.CounterText, Is.EqualTo("1 / 3"));
            Assert.That(lightbox.AltText, Is.EqualTo(photos[0].AccessibleDescription));
        });
    }

    [Test]
    public void SetPhotos_WhenCalledAgain_ReplacesTheSlidesAndResetsTheIndex()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3));
        lightbox.NextCommand.Execute(null);
        lightbox.NextCommand.Execute(null);

        var replacement = CreatePhotos(2);
        lightbox.SetPhotos(replacement);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.Photos, Has.Count.EqualTo(2));
            Assert.That(lightbox.CurrentIndex, Is.Zero);
            Assert.That(lightbox.CurrentPhoto, Is.SameAs(replacement[0]));
            Assert.That(lightbox.CounterText, Is.EqualTo("1 / 2"));
        });
    }

    [Test]
    public void OpenCommand_WhenTheIndexIsOutOfRange_ClampsToTheAvailableSlides()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3));

        lightbox.OpenCommand.Execute(99);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.True);
            Assert.That(lightbox.CurrentIndex, Is.EqualTo(2));
            Assert.That(lightbox.CounterText, Is.EqualTo("3 / 3"));
        });

        lightbox.OpenCommand.Execute(-4);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.True);
            Assert.That(lightbox.CurrentIndex, Is.Zero);
            Assert.That(lightbox.CounterText, Is.EqualTo("1 / 3"));
        });
    }

    [Test]
    public void OpenCommand_WhenTheIndexIsInRange_OpensOnThatSlide()
    {
        var lightbox = new PhotoLightboxViewModel();
        var photos = CreatePhotos(3);
        lightbox.SetPhotos(photos);

        lightbox.OpenCommand.Execute(1);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.True);
            Assert.That(lightbox.CurrentIndex, Is.EqualTo(1));
            Assert.That(lightbox.CurrentPhoto, Is.SameAs(photos[1]));
        });
    }

    [Test]
    public void CloseCommand_AfterOpening_HidesTheLightbox()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3));
        lightbox.OpenCommand.Execute(1);

        lightbox.CloseCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.False);
            Assert.That(lightbox.CurrentIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void NextCommand_AfterTheLastSlide_WrapsToTheFirstAndUpdatesTheCounter()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3));
        var counters = new List<string>();

        lightbox.NextCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        lightbox.NextCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        lightbox.NextCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.CurrentIndex, Is.Zero);
            Assert.That(counters, Is.EqualTo(new[] { "2 / 3", "3 / 3", "1 / 3" }));
        });
    }

    [Test]
    public void PreviousCommand_BeforeTheFirstSlide_WrapsToTheLastAndUpdatesTheCounter()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3));
        var counters = new List<string>();

        lightbox.PreviousCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        lightbox.PreviousCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        lightbox.PreviousCommand.Execute(null);
        counters.Add(lightbox.CounterText);

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.CurrentIndex, Is.Zero);
            Assert.That(counters, Is.EqualTo(new[] { "3 / 3", "2 / 3", "1 / 3" }));
        });
    }

    [Test]
    public void IsPanoramaMode_IsTrueOnlyForPanoramaSlides()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(3, panoramaIndex: 1));

        var onPhoto = lightbox.IsPanoramaMode;

        lightbox.NextCommand.Execute(null);
        var onPanorama = lightbox.IsPanoramaMode;

        lightbox.NextCommand.Execute(null);
        var onLastPhoto = lightbox.IsPanoramaMode;

        Assert.Multiple(() =>
        {
            Assert.That(onPhoto, Is.False);
            Assert.That(onPanorama, Is.True);
            Assert.That(lightbox.CurrentPhoto?.Kind, Is.EqualTo(ListingPhotoKind.Photo));
            Assert.That(onLastPhoto, Is.False);
        });
    }

    [Test]
    public void AltText_WhenThePhotoHasNoAlt_UsesTheDefaultDescription()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos([CreatePhoto(0), CreatePhoto(1, alt: "Ban cong")]);

        var defaultAlt = lightbox.AltText;

        lightbox.NextCommand.Execute(null);
        var customAlt = lightbox.AltText;

        Assert.Multiple(() =>
        {
            Assert.That(defaultAlt, Is.EqualTo("Ảnh phòng"));
            Assert.That(customAlt, Is.EqualTo("Ban cong"));
        });
    }

    [Test]
    public void AltText_WhenThereIsNoCurrentPhoto_IsEmpty()
    {
        var lightbox = new PhotoLightboxViewModel();

        Assert.That(lightbox.AltText, Is.Empty);
    }

    [Test]
    public void NavigationCommands_WhenFewerThanTwoPhotos_CannotExecute()
    {
        var lightbox = new PhotoLightboxViewModel();
        var emptyNext = lightbox.NextCommand.CanExecute(null);
        var emptyPrevious = lightbox.PreviousCommand.CanExecute(null);
        var emptyOpen = lightbox.OpenCommand.CanExecute(0);

        lightbox.SetPhotos(CreatePhotos(1));
        var singleNext = lightbox.NextCommand.CanExecute(null);
        var singlePrevious = lightbox.PreviousCommand.CanExecute(null);
        var singleOpen = lightbox.OpenCommand.CanExecute(0);

        Assert.Multiple(() =>
        {
            Assert.That(emptyNext, Is.False);
            Assert.That(emptyPrevious, Is.False);
            Assert.That(emptyOpen, Is.False);
            Assert.That(singleNext, Is.False);
            Assert.That(singlePrevious, Is.False);
            Assert.That(singleOpen, Is.True);
        });
    }

    [Test]
    public void NavigationCommands_WhenTwoOrMorePhotos_CanExecute()
    {
        var lightbox = new PhotoLightboxViewModel();
        lightbox.SetPhotos(CreatePhotos(2));

        Assert.Multiple(() =>
        {
            Assert.That(lightbox.NextCommand.CanExecute(null), Is.True);
            Assert.That(lightbox.PreviousCommand.CanExecute(null), Is.True);
            Assert.That(lightbox.OpenCommand.CanExecute(0), Is.True);
            Assert.That(lightbox.OpenCommand.CanExecute(1), Is.True);
        });
    }

    [Test]
    public void GetPreloadWindow_WhenFewerThanTwoPhotos_IsEmpty()
    {
        var lightbox = new PhotoLightboxViewModel();
        var empty = lightbox.GetPreloadWindow();

        lightbox.SetPhotos(CreatePhotos(1));
        var single = lightbox.GetPreloadWindow();

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Empty);
            Assert.That(single, Is.Empty);
        });
    }

    [Test]
    public void GetPreloadWindow_WithThreePhotos_ReturnsTheWrappedNeighbours()
    {
        var lightbox = new PhotoLightboxViewModel();
        var photos = CreatePhotos(3);
        lightbox.SetPhotos(photos);

        lightbox.OpenCommand.Execute(1);
        var middle = lightbox.GetPreloadWindow();

        lightbox.OpenCommand.Execute(0);
        var first = lightbox.GetPreloadWindow();

        Assert.Multiple(() =>
        {
            Assert.That(middle, Has.Count.EqualTo(2));
            Assert.That(middle[0], Is.SameAs(photos[0]));
            Assert.That(middle[1], Is.SameAs(photos[2]));
            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first[0], Is.SameAs(photos[2]));
            Assert.That(first[1], Is.SameAs(photos[1]));
        });
    }

    [Test]
    public void GetPreloadWindow_WithExactlyTwoPhotos_ReturnsASingleDistinctNeighbour()
    {
        var lightbox = new PhotoLightboxViewModel();
        var photos = CreatePhotos(2);
        lightbox.SetPhotos(photos);

        lightbox.OpenCommand.Execute(1);
        var window = lightbox.GetPreloadWindow();

        Assert.Multiple(() =>
        {
            Assert.That(window, Has.Count.EqualTo(1));
            Assert.That(window[0], Is.SameAs(photos[0]));
            Assert.That(window[0], Is.Not.SameAs(photos[1]));
        });
    }

    private static GalleryPhotoItem[] CreatePhotos(int count, int panoramaIndex = -1)
        => Enumerable.Range(0, count)
            .Select(index => CreatePhoto(index, index == panoramaIndex ? ListingPhotoKind.Panorama : ListingPhotoKind.Photo))
            .ToArray();

    private static GalleryPhotoItem CreatePhoto(
        int index,
        ListingPhotoKind kind = ListingPhotoKind.Photo,
        string? alt = null)
        => new(
            new ListingPhotoDto(
                PhotoId(index),
                ListingId,
                $"/media/photo-{index}.jpg",
                null,
                1600,
                900,
                kind,
                alt,
                index,
                index == 0));

    private static Guid PhotoId(int index) => new(index, 0, 0, new byte[8]);
}
