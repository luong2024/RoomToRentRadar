using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

public partial class PhotoGalleryViewModel : ObservableObject
{
    private readonly IPhotoGalleryService _photoGalleryService;
    private readonly INavigationService? _navigationService;

    private string _listingId = "room-2026-001";
    private bool _isLoading;
    private int _currentPosition;
    private PhotoGalleryItemModel? _currentPhoto;
    private string _errorMessage = string.Empty;

    public PhotoGalleryViewModel(
        IPhotoGalleryService photoGalleryService,
        INavigationService? navigationService = null)
    {
        _photoGalleryService = photoGalleryService;
        _navigationService = navigationService;
    }

    public ObservableCollection<PhotoGalleryItemModel> Photos { get; } = [];

    public string ListingId
    {
        get => _listingId;
        set => SetProperty(ref _listingId, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowPlaceholder));
            }
        }
    }

    public int CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                SyncCurrentPhoto();
            }
        }
    }

    public PhotoGalleryItemModel? CurrentPhoto
    {
        get => _currentPhoto;
        private set
        {
            if (SetProperty(ref _currentPhoto, value))
            {
                OnPropertyChanged(nameof(IsCurrentPhoto360));
                OnPropertyChanged(nameof(CurrentCaption));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasPhotos => Photos.Count > 0;
    public bool ShowPlaceholder => !IsLoading && Photos.Count == 0;
    public bool IsCurrentPhoto360 => CurrentPhoto?.Is360 == true;
    public string CurrentCaption => CurrentPhoto?.Caption ?? "Phòng này chưa có ảnh thực tế";
    public string CounterText => Photos.Count == 0 ? "0 / 0" : $"{CurrentPosition + 1} / {Photos.Count}";

    [RelayCommand]
    public async Task LoadPhotosAsync(string? listingId = null, CancellationToken cancellationToken = default)
    {
        var targetId = string.IsNullOrWhiteSpace(listingId) ? ListingId : listingId;
        ListingId = targetId;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var items = await _photoGalleryService.GetPhotosAsync(targetId, cancellationToken);
            Photos.Clear();
            foreach (var item in items)
            {
                Photos.Add(item);
            }

            _currentPosition = 0;
            OnPropertyChanged(nameof(CurrentPosition));
            SyncCurrentPhoto();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Không tải được bộ ảnh: {ex.Message}";
            Photos.Clear();
            SyncCurrentPhoto();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasPhotos));
            OnPropertyChanged(nameof(ShowPlaceholder));
            OnPropertyChanged(nameof(CounterText));
        }
    }

    [RelayCommand]
    public async Task Open360ViewerAsync()
    {
        if (CurrentPhoto is null || !CurrentPhoto.Is360 || _navigationService is null)
            return;

        await _navigationService.GoToAsync("viewer-360", new Dictionary<string, object>
        {
            ["imageUrl"] = CurrentPhoto.ImageUrl,
            ["caption"] = CurrentPhoto.Caption
        });
    }

    [RelayCommand]
    public async Task CloseGalleryAsync()
    {
        if (_navigationService is null)
            return;

        await _navigationService.GoBackAsync();
    }

    private void SyncCurrentPhoto()
    {
        CurrentPhoto = (CurrentPosition >= 0 && CurrentPosition < Photos.Count)
            ? Photos[CurrentPosition]
            : Photos.FirstOrDefault();

        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(CounterText));
    }
}
