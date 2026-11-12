---
name: maui-mvvm-production-guardrails
description: Best practices, architecture patterns, and concurrency safeguards for .NET MAUI with CommunityToolkit.Mvvm to produce clean, maintainable, production-ready mobile/desktop code.
---

# MAUI MVVM Production Guardrails Skill

## Mục đích
Hướng dẫn AI Agent tuân thủ chuẩn mực kiến trúc 3 tầng SOLID của dự án .NET MAUI `RoomToRentRadar`: tách bạch rõ ràng giữa View (`Features/<Feature>/*Page.xaml`), ViewModel (`*PageModel.cs`), Domain Contract (`RoomToRentRadar.Core`), và Data/SQLite (`RoomToRentRadar.Infrastructure`).

## Khi nào kích hoạt Skill này
- Khi tạo mới hoặc chỉnh sửa XAML Pages và PageModels trong `RoomToRentRadar/Features/<Feature>/`.
- Khi đăng ký Dependency Injection và Shell Route trong `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs`.

---

## Chuẩn Mực Kiến Trúc Chi Tiết

### 1. Chuẩn Hóa PageModel (CommunityToolkit.Mvvm + Constructor Injection)
- Nhận trực tiếp các Interface từ `RoomToRentRadar.Core.Abstractions` qua Constructor Injection (**CẤM dùng `IServiceProvider.GetService` hay tự tạo mock data trong PageModel**):
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

### 2. Code-Behind (`.xaml.cs`) Tối Giản
- Không viết logic tính toán, validate nghiệp vụ, hoặc gọi trực tiếp SQLite trong file `.xaml.cs`.
- File `.xaml.cs` chỉ thực hiện inject `BindingContext`.

### 3. Quy Tắc Xử Lý Đa Luồng (Threading & UI Safety)
- Không chặn luồng UI bằng `.Result` hoặc `.Wait()`. Luôn dùng `await`.

### 4. Đăng Ký Dependency Injection & Route Trong `<Feature>FeatureModule.cs` (Chuẩn OCP)
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
- [ ] Inject trực tiếp Interface qua Constructor (không dùng Service Locator).
- [ ] Mọi command bất đồng bộ đều có cờ `IsBusy` chống bấm đúp.
- [ ] Đã đăng ký Page/PageModel/Service trong `<Feature>FeatureModule.cs` (không chạm vào `MauiProgram.cs`).
