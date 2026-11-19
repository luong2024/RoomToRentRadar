using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

/// <summary>
/// Landlord listing management screen (US08 AC2 and AC3). Navigation and the
/// unpublish confirmation live outside this class so it stays unit-testable.
/// </summary>
public partial class MyListingsViewModel : ObservableObject
{
    private const string EmptyMessageText = "Bạn chưa có tin đăng nào.";

    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string OperationMessage { get; set; }

    /// <summary>True when the API rejected the request with 401.</summary>
    [ObservableProperty]
    public partial bool IsSignInRequired { get; set; }

    public MyListingsViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        Message = string.Empty;
        ErrorMessage = string.Empty;
        OperationMessage = string.Empty;
        IsEmpty = true;
    }

    public ObservableCollection<MyListingsItem> Listings { get; } = new();

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasOperationMessage => !string.IsNullOrEmpty(OperationMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnOperationMessageChanged(string value)
        => OnPropertyChanged(nameof(HasOperationMessage));

    partial void OnIsBusyChanged(bool value)
    {
        foreach (var item in Listings)
            item.IsActionEnabled = !value;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        IsSignInRequired = false;

        try
        {
            var listings = await _apiClient.GetMyListingsAsync(cancellationToken);

            Listings.Clear();
            foreach (var listing in listings)
                Listings.Add(MyListingsItem.FromDto(listing));

            IsEmpty = Listings.Count == 0;
            Message = IsEmpty ? EmptyMessageText : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Listings.Clear();
            IsEmpty = true;
            IsSignInRequired = true;
            Message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (Exception)
        {
            Listings.Clear();
            IsEmpty = true;
            IsSignInRequired = false;
            Message = EmptyMessageText;
            ErrorMessage = "Không tải được danh sách tin đăng. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        try
        {
            await _navigationService.GoToListingEditorAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Không mở được trang đăng tin. Vui lòng thử lại.";
        }
    }

    [RelayCommand]
    private async Task EditAsync(MyListingsItem? item)
    {
        if (item is null)
            return;

        try
        {
            await _navigationService.GoToListingEditorAsync(item.Id);
        }
        catch (Exception)
        {
            ErrorMessage = "Không mở được trang sửa tin. Vui lòng thử lại.";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task UnpublishAsync(MyListingsItem? item, CancellationToken cancellationToken)
    {
        if (item is null || IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        OperationMessage = string.Empty;
        var reloadRequested = false;

        try
        {
            var updated = await _apiClient.UnpublishListingAsync(item.Id, cancellationToken);
            item.Apply(updated);
            OperationMessage = $"Đã ẩn tin \u201c{item.Title}\u201d.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsSignInRequired = true;
            ErrorMessage = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (ApiException ex) when (ex.Code == "listing_not_found")
        {
            ErrorMessage = "Tin đăng không còn tồn tại.";
            reloadRequested = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Không ẩn được tin đăng. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }

        if (reloadRequested)
            await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            await _navigationService.GoToLoginAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Không mở được trang đăng nhập.";
        }
    }
}
