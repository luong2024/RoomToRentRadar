---
name: sqlite-and-minimal-api-production
description: Production patterns and guardrails for implementing SQLite Repositories (Microsoft.Data.Sqlite) in RoomToRentRadar.Infrastructure and ASP.NET Core MVC Controllers (JWT, DataAnnotations, Docker) in RoomToRentRadar.Api without package bloat or runtime cast errors. (Folder name kept for compatibility; the API now uses MVC Controllers, not Minimal API.)
---

# SQLite & ASP.NET Core MVC Web API Production Guardrails Skill

## Mục đích
Hướng dẫn AI Agent khi thực hiện 10 Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`) viết code truy vấn SQLite trong `RoomToRentRadar.Infrastructure` và Controller MVC trong `RoomToRentRadar.Api` (chạy trong Docker) đạt chuẩn Production, an toàn kiểu dữ liệu, chống SQL Injection và không làm hỏng `.csproj`.

## Khi nào kích hoạt Skill này
- Khi viết code trong `RoomToRentRadar.Infrastructure/Repositories/` hoặc `Services/`.
- Khi viết code trong `RoomToRentRadar.Api/Controllers/*Controller.cs` hoặc `RoomToRentRadar.Api/Models/*Models.cs`.
- Khi chuyển một file Minimal API cũ `RoomToRentRadar.Api/Endpoints/*Endpoints.cs` sang Controller.

---

## 1. Quy Tắc Sắt Khi Làm Việc Với SQLite (`Microsoft.Data.Sqlite`)

1. **CẤM tự ý cài thêm EF Core, Dapper hay gói NuGet ngoài:**
   - Dự án sử dụng trực tiếp `Microsoft.Data.Sqlite.Core` + `RentalDatabaseService` đã cấu hình sẵn. Tuyệt đối không sửa `.csproj` để thêm ORM khác.
2. **Luôn mở kết nối qua `await Database.GetOpenConnectionAsync()`:**
   - Hàm này đã tự động gọi `EnsureInitializedAsync()` (khởi tạo bảng & seed 7 phòng mẫu ICTU với khóa `SemaphoreSlim` an toàn đa luồng).
   - Luôn bọc kết nối và câu lệnh trong `await using`:
     ```csharp
     await using var connection = await Database.GetOpenConnectionAsync();
     await using var cmd = connection.CreateCommand();
     ```
3. **BẮT BUỘC dùng Parameterized Queries (Chống SQL Injection 100%):**
   - **CẤM** dùng nội suy chuỗi `$"SELECT * FROM RoomListing WHERE Ward = '{ward}'"` (sẽ lỗi hoặc dính SQL Injection khi chuỗi tiếng Việt có dấu nháy).
   - **PHẢI** dùng `cmd.Parameters.AddWithValue("@param", value ?? (object)DBNull.Value)`:
     ```csharp
     cmd.CommandText = @"
         SELECT * FROM RoomListing
         WHERE IsVerified = 1 AND Status = @status
         ORDER BY DistanceToCampusKm ASC
         LIMIT @limit OFFSET @offset;";
     cmd.Parameters.AddWithValue("@status", Constants.ListingStatuses.Available);
     cmd.Parameters.AddWithValue("@limit", pageSize);
     cmd.Parameters.AddWithValue("@offset", pageIndex * pageSize);
     ```
4. **Cách đọc dữ liệu an toàn từ `SqliteDataReader` (Tránh `InvalidCastException`):**
   - Trong SQLite, các cột số thực (`MonthlyRent`, `Deposit`, `Area`, `DistanceToCampusKm`) có kiểu lưu trữ là `REAL` (`double`), và `IsVerified` / `IsEnabled` là `INTEGER` (`long`).
   - Khi đọc bảng `RoomListing` trong `RoomListingRepository.*.cs`, **hãy gọi luôn hàm có sẵn `MapRoomListing(reader)`** trong `RoomListingRepository.cs`:
     ```csharp
     var results = new List<RoomListing>();
     await using var reader = await cmd.ExecuteReaderAsync();
     while (await reader.ReadAsync())
     {
         results.Add(MapRoomListing(reader));
     }
     return results;
     ```
   - Khi đọc các bảng khác (`UserAccount`, `FavoriteRoom`, `SavedSearchAlert`, `HouseRuleVersion`, `ListingReport`), luôn dùng `Convert.ToInt32(reader["Col"])`, `Convert.ToDecimal(reader["Col"])`, `Convert.ToDouble(reader["Col"])`, `Convert.ToString(reader["Col"]) ?? string.Empty`, và kiểm tra `reader["NullableCol"] is DBNull ? null : ...` cho các cột cho phép `NULL` (như `MinPrice`, `MaxPrice` trong `SavedSearchAlert`).
5. **Khi `INSERT` bản ghi mới và cần lấy `Id` vừa sinh:**
   - Gộp `SELECT last_insert_rowid();` ở cuối câu lệnh `INSERT` và gọi `Convert.ToInt32(await cmd.ExecuteScalarAsync())`:
     ```csharp
     cmd.CommandText = @"
         INSERT INTO ListingReport (RoomId, ReporterUserId, Reason, Note, Status, CreatedAt)
         VALUES (@RoomId, @ReporterUserId, @Reason, @Note, @Status, @CreatedAt);
         SELECT last_insert_rowid();";
     var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
     ```

---

## 2. Chuẩn Viết Controller MVC Trong `RoomToRentRadar.Api/Controllers/`

Web API theo mô hình **MVC**: **M** = DTO trong `Api/Models/<Feature>Models.cs`, **C** = `Api/Controllers/<Feature>Controller.cs`, **V** = dữ liệu JSON trả về. Mẫu chuẩn để copy: `Controllers/AuthController.cs` + `Models/AuthModels.cs`.

1. **Tuyệt đối KHÔNG sửa `RoomToRentRadar.Api/Program.cs`:**
   - `Program.cs` đã bật sẵn `AddControllers()`, `MapControllers()` và JWT. Controller mới tự được nhận, mỗi Task chỉ sửa Controller và Models của mình.
   - Nếu Api cần đăng ký repository/service thật mới (ví dụ `AddSingleton<IFavoriteRepository, FavoriteRepository>()`), báo `@architect` (`@quangtldc0301`) thêm vào `Program.cs`.
   - **Không thêm route mới vào `Api/Endpoints/*Endpoints.cs`** (Minimal API cũ). Khi làm Task, chuyển route sang Controller rồi xóa file Endpoint tương ứng và dòng `app.Map...Endpoints()` (báo `@architect`).
2. **Model (DTO) kiểm tra đầu vào bằng DataAnnotations, thông báo tiếng Việt:**
   - Với `record`, gắn attribute **trên tham số** (`[Required] string Email`). **CẤM** dùng `[property: Required]`: MVC ném lỗi và trả 500 cho mọi request.
   - `[ApiController]` tự trả **400** dạng `ErrorResponse { message, errors }` (cấu hình sẵn trong `Program.cs`) trước khi vào action.
   ```csharp
   // Api/Models/FavoriteModels.cs (T07.1)
   namespace RoomToRentRadar.Api.Models
   {
       public sealed record FavoriteRoomResponse(int Id, string Title, decimal MonthlyRent, string Status, bool IsVerified);
   }
   ```
3. **Mẫu Controller chuẩn: mã HTTP rõ ràng, người gọi lấy từ JWT:**
   ```csharp
   // Api/Controllers/RoomDetailController.cs (T03.1)
   [ApiController]
   [Route("api/rooms")]
   [Produces("application/json")]
   public sealed class RoomDetailController : ControllerBase
   {
       private readonly IRoomListingRepository _rooms;

       public RoomDetailController(IRoomListingRepository rooms) => _rooms = rooms; // Constructor Injection

       [HttpGet("{id:int}")]
       [ProducesResponseType<RoomListing>(StatusCodes.Status200OK)]
       [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
       public async Task<IActionResult> GetById(int id)
       {
           if (id <= 0)
               return BadRequest(new ErrorResponse("Mã phòng không hợp lệ."));

           var room = await _rooms.GetByIdAsync(id);
           return room is not null
               ? Ok(room)
               : NotFound(new ErrorResponse("Không tìm thấy phòng trọ."));
       }
   }

   // Route cần đăng nhập (T07.1): userId lấy từ token, KHÔNG nhận từ client
   [HttpGet]
   [Authorize]
   public async Task<IActionResult> GetMine()
   {
       if (!AuthController.TryGetUserId(User, out var userId))
           return Unauthorized(new ErrorResponse("Token không hợp lệ."));
       return Ok(await _favorites.GetFavoriteRoomsAsync(userId));
   }
   ```
   - Phân quyền theo role: `[Authorize(Roles = Constants.UserRoles.Landlord + "," + Constants.UserRoles.Admin)]` (claim `role` trong JWT).
   - **CẤM dùng `IAuthService.CurrentUser` trong Controller**: biến này dùng chung cho mọi request trong web server.
   - Không trả dữ liệu nhạy cảm (`PasswordHash`; số điện thoại chủ trọ cho khách theo US03 AC4): map sang DTO trong `Models/`.
   - Nếu tên `Models` bị trùng giữa `RoomToRentRadar.Api.Models` và `RoomToRentRadar.Models`, dùng alias: `using UserAccount = RoomToRentRadar.Models.UserAccount;`.
4. **Khi hoàn thành một Repository/Service mới trong `Infrastructure`:**
   - **App MAUI (giai đoạn chuyển tiếp):** đăng ký `services.AddSingleton<IRoomListingRepository, RoomListingRepository>();` (hoặc Interface tương ứng của Task) vào đúng `<Feature>FeatureModule.cs` để chuyển từ bản `InMemory*` sang bản SQLite thật.
   - **Web API:** báo `@architect` đăng ký cùng dòng đó trong `Api/Program.cs`, nếu không API vẫn dùng bản `InMemory*`.
5. **Chạy thử trong Docker trước khi báo hoàn tất:**
   ```powershell
   docker compose up --build -d
   # gọi thử các route của Task (thêm request mẫu vào RoomToRentRadar.Api/RoomToRentRadar.Api.http)
   docker compose down
   ```
   CI build `Core`, `Infrastructure`, `Api` với `--warnaserror`: code mới không được có cảnh báo biên dịch.
