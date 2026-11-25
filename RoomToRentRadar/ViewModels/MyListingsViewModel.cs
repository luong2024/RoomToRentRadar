using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;

namespace RoomToRentRadar.ViewModels;

public partial class MyListingsViewModel(IListingManagementClient client, AuthService auth,
    INavigationService navigation) : ObservableObject
{
    public ObservableCollection<OwnedListingItem> Listings { get; } = [];
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;

    [RelayCommand] private Task SignInAsync() => navigation.GoToLoginAsync();
    [RelayCommand] private Task CreateAsync() => navigation.GoToListingEditorAsync();
    [RelayCommand] private Task EditAsync(OwnedListingItem item) => navigation.GoToListingEditorAsync(item.Id);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        IsAuthenticated = false;
        Listings.Clear();
        await navigation.GoToRoleRootAsync(null);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        Message = string.Empty;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            Listings.Clear();
            if (!IsAuthenticated) { Message = "Đăng nhập để đăng và quản lý tin cho thuê của bạn."; return; }
            var listings = await client.GetMineAsync(cancellationToken);
            foreach (var listing in listings) Listings.Add(new OwnedListingItem(listing));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = "Không thể tải tin của bạn. Kiểm tra kết nối và thử lại.";
            if (ex is ListingManagementException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
                IsAuthenticated = false;
        }
        finally { IsBusy = false; }
    }
}
