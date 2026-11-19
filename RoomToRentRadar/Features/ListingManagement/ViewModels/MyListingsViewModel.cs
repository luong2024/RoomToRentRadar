using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

public partial class MyListingsViewModel(IListingManagementClient client, AuthService auth,
    INavigationService navigation) : ObservableObject
{
    public ObservableCollection<OwnedListingItem> Listings { get; } = [];
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;
    [ObservableProperty] public partial string OperationMessage { get; set; } = string.Empty;

    public bool IsNotBusy => !IsBusy;

    [RelayCommand] private Task SignInAsync() => navigation.GoToLoginAsync();
    [RelayCommand] private Task CreateAsync() => navigation.GoToListingEditorAsync();
    [RelayCommand] private Task EditAsync(OwnedListingItem item) => navigation.GoToListingEditorAsync(item.Id);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        Message = string.Empty;
        OperationMessage = string.Empty;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            Listings.Clear();
            if (!IsAuthenticated) { Message = "Đăng nhập để đăng và quản lý tin cho thuê của bạn."; return; }
            var listings = await client.GetMineAsync(cancellationToken);
            foreach (var listing in listings) Listings.Add(new OwnedListingItem(listing));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ListingManagementException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsAuthenticated = false;
            Message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = "Không thể tải tin của bạn. Kiểm tra kết nối và thử lại.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task UnpublishAsync(OwnedListingItem? item, CancellationToken cancellationToken)
    {
        if (item is null || IsBusy || !item.CanUnpublish) return;

        IsBusy = true;
        Message = string.Empty;
        OperationMessage = string.Empty;
        var reloadRequested = false;
        try
        {
            item.Apply(await client.UnpublishAsync(item.Id, cancellationToken));
            OperationMessage = $"Đã gỡ đăng tin \"{item.Title}\".";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ListingManagementException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsAuthenticated = false;
            Message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (ListingManagementException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            reloadRequested = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = "Không thể gỡ tin. Kiểm tra kết nối và thử lại.";
        }
        finally { IsBusy = false; }

        if (reloadRequested)
        {
            await LoadAsync(cancellationToken);
            if (Message.Length == 0)
                Message = "Tin đăng không còn tồn tại. Danh sách đã được làm mới.";
        }
    }
}
