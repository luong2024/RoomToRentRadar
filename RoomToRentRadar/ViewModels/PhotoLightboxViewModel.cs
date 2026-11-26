using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.ViewModels;

public partial class PhotoLightboxViewModel : ObservableObject
{
    public ObservableCollection<GalleryPhotoItem> Photos { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPhoto))]
    [NotifyPropertyChangedFor(nameof(CurrentFullSource))]
    [NotifyPropertyChangedFor(nameof(IsPanoramaMode))]
    [NotifyPropertyChangedFor(nameof(CounterText))]
    [NotifyPropertyChangedFor(nameof(AltText))]
    public partial int CurrentIndex { get; set; }

    public GalleryPhotoItem? CurrentPhoto =>
        CurrentIndex >= 0 && CurrentIndex < Photos.Count ? Photos[CurrentIndex] : null;

    public ImageSource? CurrentFullSource => CurrentPhoto?.FullSource;

    public bool IsPanoramaMode => CurrentPhoto?.IsPanorama == true;

    public string CounterText => Photos.Count == 0 ? string.Empty : $"{CurrentIndex + 1} / {Photos.Count}";

    public string AltText => CurrentPhoto?.AccessibleDescription ?? string.Empty;

    public bool HasPhotos => Photos.Count > 0;

    public void SetPhotos(IReadOnlyList<GalleryPhotoItem> photos)
    {
        Photos.Clear();
        foreach (var photo in photos)
        {
            Photos.Add(photo);
        }

        CurrentIndex = 0;
        RaiseGalleryChanged();
    }

    [RelayCommand(CanExecute = nameof(HasPhotos))]
    private void Open(int startIndex)
    {
        CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, Photos.Count - 1));
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void Next() => CurrentIndex = (CurrentIndex + 1) % Photos.Count;

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void Previous() => CurrentIndex = (CurrentIndex - 1 + Photos.Count) % Photos.Count;

    // The lightbox keeps the neighbours of the current page warm; the carousel
    // loads only the images in this window plus the current one.
    public IReadOnlyList<GalleryPhotoItem> GetPreloadWindow()
    {
        if (Photos.Count < 2)
        {
            return [];
        }

        var previous = Photos[(CurrentIndex - 1 + Photos.Count) % Photos.Count];
        var next = Photos[(CurrentIndex + 1) % Photos.Count];

        return ReferenceEquals(previous, next) ? [previous] : [previous, next];
    }

    private bool CanNavigate() => Photos.Count > 1;

    private void RaiseGalleryChanged()
    {
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(CurrentPhoto));
        OnPropertyChanged(nameof(CurrentFullSource));
        OnPropertyChanged(nameof(IsPanoramaMode));
        OnPropertyChanged(nameof(CounterText));
        OnPropertyChanged(nameof(AltText));
        OpenCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }
}
