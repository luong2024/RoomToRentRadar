---
name: maui-shell-navigation
description: Guidance on standard navigation patterns using MAUI AppShell and passing parameters via QueryProperty.
---

# MAUI Shell Navigation & Parameter Passing

Derived from `davidortinau/maui-skills/maui-navigation`. Standardizes route registration and safe parameter passing across `RoomToRentRadar`.

## 1. Route Registration in `AppShell.xaml.cs`
Pages not directly anchored to root tabs or flyout items (e.g., Detail Page, 360 Panorama Viewer, Comparison Matrix) must be explicitly registered:

```csharp
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(RoomDetailPage), typeof(RoomDetailPage));
        Routing.RegisterRoute(nameof(CompareRoomsPage), typeof(CompareRoomsPage));
        Routing.RegisterRoute(nameof(PanoramaViewerPage), typeof(PanoramaViewerPage));
    }
}
```

## 2. Page Navigation & Passing Listing IDs
Avoid serializing and passing large complex objects across navigation routes. Pass only scalar identifiers:

```csharp
[RelayCommand]
private async Task OpenRoomDetailAsync(string roomId)
{
    if (string.IsNullOrWhiteSpace(roomId)) return;
    await Shell.Current.GoToAsync($"{nameof(RoomDetailPage)}?id={roomId}");
}
```

## 3. Receiving Parameters in `*PageModel` (Example)

```csharp
// Example PageModel following the *PageModel naming convention
[QueryProperty(nameof(RoomId), "id")]
public partial class RoomDetailPageModel : ObservableObject
{
    private readonly IGetRoomDetailUseCase _getRoomDetailUseCase;

    [ObservableProperty]
    private string _roomId = string.Empty;

    // Triggered automatically whenever RoomId is populated via navigation
    async partial void OnRoomIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            await LoadRoomDetailsAsync(value);
        }
    }
}
```
