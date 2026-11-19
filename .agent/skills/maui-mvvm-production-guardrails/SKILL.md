---
name: maui-mvvm-production-guardrails
description: Best practices, XAML SourceGen rules, Shell navigation patterns, and concurrency safeguards for .NET MAUI with CommunityToolkit.Mvvm to produce clean, maintainable, production-ready mobile/desktop code.
---

# MAUI MVVM Production Guardrails Skill

## Mục đích
Hướng dẫn AI Agent tuân thủ chuẩn mực kiến trúc 4 project SOLID của dự án `.NET MAUI RoomToRentRadar`: tách bạch rõ ràng giữa View (`Features/<Feature>/*Page.xaml`), ViewModel (`*PageModel.cs`), Domain Contract (`RoomToRentRadar.Core`), và Data/SQLite (`RoomToRentRadar.Infrastructure`).

## Khi nào kích hoạt Skill này
- Khi tạo mới hoặc chỉnh sửa XAML Pages, Custom Controls và PageModels trong `RoomToRentRadar/Features/<Feature>/` hoặc `RoomToRentRadar/Pages/Controls/`.
- Khi đăng ký Dependency Injection và Shell Route trong `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs`.

---

## Chuẩn Mực Kiến Trúc Chi Tiết

### 1. Chuẩn Hóa PageModel (CommunityToolkit.Mvvm + Constructor Injection)
- Nhận trực tiếp các Interface từ `RoomToRentRadar.Core.Abstractions` qua Constructor Injection (**CẤM dùng `IServiceProvider.GetService` hay tự tạo mock data trong PageModel**).
- Dùng cú pháp `partial property` (hoặc field) với `[ObservableProperty]` trên `partial class`:
  ```csharp
  public partial class RoomListPageModel : ObservableObject
  {
      private readonly IRoomListingRepository _roomRepository;
      private readonly IErrorHandler _errorHandler;

      [ObservableProperty]
      public partial bool IsBusy { get; set; }

      public ObservableCollection<RoomListing> Rooms { get; } = new();

      public RoomListPageModel(IRoomListingRepository roomRepository, IErrorHandler errorHandler)
      {
          _roomRepository = roomRepository;
          _errorHandler = errorHandler;
      }

      [RelayCommand]
      private async Task LoadRoomsAsync()
      {
          if (IsBusy) return;
          try
          {
              IsBusy = true;
              var items = await _roomRepository.ListVerifiedRoomsAsync();
              Rooms.Clear();
              foreach (var item in items) Rooms.Add(item);
          }
          catch (Exception ex)
          {
              _errorHandler.HandleError(ex);
          }
          finally
          {
              IsBusy = false;
          }
      }
  }
  ```

### 2. Quy Tắc Bắt Buộc Với XAML SourceGen (`MauiXamlInflator=SourceGen`)
Dự án bật biên dịch XAML sang C# lúc compile-time (`SourceGen` + `MauiEnableXamlCBindingWithSourceCompilation`). Để không bị lỗi biên dịch XAML:
1. **Luôn khai báo `x:DataType` trên `ContentPage`** trỏ đúng tới `PageModel`.
2. **Luôn khai báo `x:DataType="models:RoomListing"` (kèm `assembly=RoomToRentRadar.Core`) bên trong mọi `<DataTemplate>`:**
   ```xml
   <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:rooms="clr-namespace:RoomToRentRadar.Features.Rooms"
                xmlns:models="clr-namespace:RoomToRentRadar.Models;assembly=RoomToRentRadar.Core"
                x:Class="RoomToRentRadar.Features.Rooms.RoomListPage"
                x:DataType="rooms:RoomListPageModel">
       <CollectionView ItemsSource="{Binding Rooms}">
           <CollectionView.ItemTemplate>
               <DataTemplate x:DataType="models:RoomListing">
                   <Label Text="{Binding Title}" />
               </DataTemplate>
           </CollectionView.ItemTemplate>
       </CollectionView>
   </ContentPage>
   ```
3. **Không dùng API đã lỗi thời trong .NET 10 MAUI:** Dùng `DisplayAlertAsync(...)` thay vì `DisplayAlert(...)`; dùng `Border` với `StrokeShape="RoundRectangle 12"` thay vì `Frame`.

### 3. Nhận Tham Số Điều Hướng (`[QueryProperty]`) An Toàn Độc Lập
Khi một màn hình nhận tham số từ màn hình trước (ví dụ `RoomDetailPageModel` nhận `roomId`):
- Luôn gán **giá trị mặc định `RoomId = 1`** để nếu thành viên hoặc người chấm mở trực tiếp màn hình đó khi test độc lập, màn hình vẫn tải và hiển thị ngay Phòng số 1 của ICTU mà không bị trống trang:
  ```csharp
  [QueryProperty(nameof(RoomId), "roomId")]
  public partial class RoomDetailPageModel : ObservableObject
  {
      [ObservableProperty]
      public partial int RoomId { get; set; } = 1;
  }
  ```

### 4. Quy Tắc Xử Lý Đa Luồng (Threading & UI Safety)
- **CẤM TUYỆT ĐỐI** chặn luồng UI bằng `.Result` hoặc `.Wait()` (CI và `preflight.ps1` sẽ báo lỗi đỏ ngay lập tức). Luôn dùng `async`/`await`.

### 5. Đăng Ký Dependency Injection & Route Trong `<Feature>FeatureModule.cs` (Chuẩn OCP)
- **TUYỆT ĐỐI KHÔNG sửa `MauiProgram.cs`**.
- Mở đúng file `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs` của tính năng mình để đăng ký:
  ```csharp
  // Ví dụ trong FavoritesFeatureModule.cs:
  services.AddSingleton<IFavoriteRepository, FavoriteRepository>(); // T07.1
  services.AddTransientWithShellRoute<FavoritesPage, FavoritesPageModel>(Constants.Routes.Favorites); // T07.2
  ```

---

## Production Verification Checklist
- [ ] Không có `.Result` hoặc `.Wait()` trong toàn bộ code mới.
- [ ] Đã khai báo đủ `x:DataType` trên `ContentPage` và `<DataTemplate>` (có `;assembly=RoomToRentRadar.Core` khi trỏ tới `RoomToRentRadar.Models`).
- [ ] Inject trực tiếp Interface qua Constructor (không dùng Service Locator).
- [ ] Mọi command bất đồng bộ đều có cờ `IsBusy` chống bấm đúp.
- [ ] Đã đăng ký Page/PageModel/Service trong `<Feature>FeatureModule.cs` (không chạm vào `MauiProgram.cs`).
