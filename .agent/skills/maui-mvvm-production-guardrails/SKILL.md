---
name: maui-mvvm-production-guardrails
description: Best practices, architecture patterns, and concurrency safeguards for .NET MAUI with CommunityToolkit.Mvvm to produce clean, maintainable, production-ready mobile/desktop code.
---

# MAUI MVVM Production Guardrails Skill

## Mục đích
Hướng dẫn AI Agent tuân thủ chuẩn mực kiến trúc của dự án .NET MAUI: tách bạch rõ ràng giữa View, ViewModel (PageModel), Model, và Service; tránh rò rỉ bộ nhớ (memory leaks), deadlock UI luồng và lỗi ràng buộc dữ liệu (Data Binding).

## Khi nào kích hoạt Skill này
- Khi tạo mới hoặc chỉnh sửa XAML Pages trong `RoomToRentRadar/Pages/`.
- Khi viết logic trong `RoomToRentRadar/PageModels/`.
- Khi đăng ký Dependency Injection trong `MauiProgram.cs`.

---

## Chuẩn Mực Kiến Trúc Chi Tiết

### 1. Chuẩn Hóa PageModel (CommunityToolkit.Mvvm)
- Sử dụng các Source Generator attribute:
  ```csharp
  public partial class TaskDetailPageModel : ObservableObject
  {
      private readonly TaskRepository _taskRepository;

      [ObservableProperty]
      private ProjectTask _currentTask;

      [ObservableProperty]
      private bool _isBusy;

      public TaskDetailPageModel(TaskRepository taskRepository)
      {
          _taskRepository = taskRepository;
      }

      [RelayCommand]
      private async Task SaveTaskAsync()
      {
          if (IsBusy) return;
          try
          {
              IsBusy = true;
              await _taskRepository.SaveItemAsync(CurrentTask);
              await Shell.Current.GoToAsync("..");
          }
          catch (Exception ex)
          {
              // Log và hiển thị lỗi an toàn
              System.Diagnostics.Debug.WriteLine($"[Error] SaveTask failed: {ex.Message}");
          }
          finally
          {
              IsBusy = false;
          }
      }
  }
  ```

### 2. Code-Behind (`.xaml.cs`) Tối Giản
- Không viết logic tính toán, validate nghiệp vụ, hoặc gọi trực tiếp SQLite/API trong file `.xaml.cs`.
- File `.xaml.cs` chỉ thực hiện inject BindingContext:
  ```csharp
  public partial class TaskDetailPage : ContentPage
  {
      public TaskDetailPage(TaskDetailPageModel viewModel)
      {
          InitializeComponent();
          BindingContext = viewModel;
      }
  }
  ```

### 3. Quy Tắc Xử Lý Đa Luồng (Threading & UI Safety)
- **Quy tắc 1:** Không chặn luồng UI bằng `.Result` hoặc `.Wait()`. Luôn dùng `await`.
- **Quy tắc 2:** Bất kỳ thay đổi nào tác động tới `ObservableCollection` hoặc các thuộc tính bind lên UI phải được kích hoạt từ Main Thread nếu đang ở background context:
  ```csharp
  MainThread.BeginInvokeOnMainThread(() =>
  {
      Items.Add(newItem);
  });
  ```

### 4. Đăng Ký Dependency Injection Đúng Vòng Đời (`MauiProgram.cs`)
- **Transient:** Thích hợp cho Pages và PageModels (tạo mới mỗi lần điều hướng).
  ```csharp
  builder.Services.AddTransient<TaskDetailPage>();
  builder.Services.AddTransient<TaskDetailPageModel>();
  ```
- **Singleton:** Dành cho Repositories, Services quản lý state xuyên suốt ứng dụng.
  ```csharp
  builder.Services.AddSingleton<TaskRepository>();
  ```

---

## Production Verification Checklist
- [ ] Không có `.Result` hoặc `.Wait()` trong toàn bộ code mới.
- [ ] View và ViewModel được giải phóng tài nguyên an toàn (hủy đăng ký sự kiện nếu có).
- [ ] Mọi command đều có cờ `IsBusy` để ngăn chặn người dùng bấm đúp liên tục (Double tap issue).
- [ ] Đã đăng ký Page và PageModel vào `MauiProgram.cs`.
