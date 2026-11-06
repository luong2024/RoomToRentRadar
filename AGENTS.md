# AI Agent Governance & Team Collaboration Rules (RoomToRentRadar)

Quy chuẩn tối cao dành cho các AI Agent (Antigravity, Cursor, Claude Code, Copilot, Aider, v.v.) hoạt động trên repository **RoomToRentRadar**. Mọi Agent khi thực hiện task PHẢI tuân thủ nghiêm ngặt các nguyên tắc sau để đảm bảo không đá code, không phá vỡ kiến trúc và đạt tiêu chuẩn **Production-Ready**.

---

## 1. Nguyên Tắc Bất Di Bất Dịch (Zero-Tolerance Rules)

1. **Strict Bounded Context (Ranh giới phạm vi nghiêm ngặt):**
   - Chỉ được phép đọc/ghi các file nằm trong phạm vi task được giao.
   - **Shared Core là Read-Only:** Các thư mục `RoomToRentRadar/Models/`, `RoomToRentRadar/Data/`, và file `MauiProgram.cs` chỉ được phép sửa đổi khi task yêu cầu rõ ràng và đã được thống nhất trong hợp đồng thiết kế.
2. **Contract Immutability (Bất biến Interface & Models):**
   - KHÔNG ĐƯỢC tự ý đổi tên thuộc tính, đổi kiểu dữ liệu (data type), hoặc xóa các phương thức công khai (public methods/properties) trong `Models/` và các `Repository`/`Service`.
   - Nếu cần mở rộng, chỉ thêm mới (backward-compatible) hoặc dùng Overloading/Optional parameters.
3. **Regression Immunity (Bảo toàn tính năng cũ):**
   - TUYỆT ĐỐI KHÔNG được comment-out, xóa, hoặc sửa đổi kết quả kỳ vọng của các bài test hiện có trong `RoomToRentRadarUnitTests` để làm cho test pass.
   - Nếu code mới làm fail test cũ: **Code mới sai, không phải test cũ sai**. Agent phải tự sửa code của mình.
4. **No Code Splatter (Cấm tạo code rác/hỗn độn):**
   - Không tự ý thêm helper linh tinh vào các file chung. Mọi helper chỉ phục vụ riêng cho một màn hình/tính năng phải đặt trong namespace/thư mục cục bộ của tính năng đó.

---

## 2. Bản Đồ Trách Nhiệm Thư Mục (Codebase Boundary Map)

| Khu vực | Trách nhiệm | Mức độ hạn chế cho Agent |
| :--- | :--- | :--- |
| `RoomToRentRadar/Models/` | DTOs, Entities, Domain Models | 🔒 **RESTRICTED** - Cần thống nhất trước; chỉ mở rộng, không phá vỡ. |
| `RoomToRentRadar/Data/` | Repositories, Database context | 🔒 **RESTRICTED** - Thay đổi schema phải kèm migration an toàn. |
| `RoomToRentRadar/MauiProgram.cs` | DI Registration, Handlers, App Config | ⚠️ **SUPERVISED** - Chỉ đăng ký thêm Service/PageModel mới; không sửa config chung. |
| `RoomToRentRadar/PageModels/` | ViewModels (CommunityToolkit.Mvvm) | 🟢 **PERMITTED** - Cho phép sửa theo đúng PageModel của task. |
| `RoomToRentRadar/Pages/` | Views XAML & Code-behind | 🟢 **PERMITTED** - Chỉ sửa Page thuộc task. |
| `RoomToRentRadarUnitTests/` | Unit Tests | 🟢 **MANDATORY** - Bắt buộc bổ sung test cho code mới viết. |

---

## 3. Tiêu Chuẩn Kỹ Thuật Cho .NET MAUI & C#

1. **Kiến trúc MVVM (CommunityToolkit.Mvvm):**
   - Toàn bộ logic nghiệp vụ và dữ liệu nằm ở `PageModel`, sử dụng source generators: `[ObservableProperty]`, `[RelayCommand]`.
   - Code-behind (`.xaml.cs`) chỉ chứa các tương tác UI thuần túy (animation, focus, platform handler), không chứa business logic hay gọi database trực tiếp.
2. **Luồng & Threading Safety:**
   - Các tác vụ I/O, Database, Network phải là `async/await` và chạy trên background thread (`Task.Run` hoặc `ConfigureAwait(false)` khi thích hợp).
   - Mọi thao tác cập nhật UI hoặc ObservableCollection phải được dispatch về Main Thread:
     ```csharp
     await MainThread.InvokeOnMainThreadAsync(() => { ... });
     ```
3. **Quản lý tài nguyên & Exception Handling:**
   - Không bao giờ dùng empty catch block `catch (Exception) { }` để giấu lỗi.
   - Phải có structured logging (`ILogger`) và thông báo lỗi thân thiện cho người dùng nếu phát sinh lỗi thao tác.

---

## 4. Quy Trình Kiểm Thử & Tự Đánh Giá (Agent Pre-flight Checklist)

Trước khi thông báo hoàn tất task, Agent PHẢI tự chạy checklist sau:
- [ ] Lệnh build thành công: `dotnet build` không có warning mới nghiêm trọng.
- [ ] Toàn bộ unit tests pass: `$env:DOTNET_ROLL_FORWARD = "Major"; dotnet test RoomToRentRadarUnitTests`.
- [ ] Git diff kiểm tra:
  - Chỉ chứa các file liên quan trực tiếp đến task.
  - Không có file cấu hình ẩn hay file binary tạm thời bị thêm vào.
  - Không có file core/shared bị sửa ngoài ý muốn.
