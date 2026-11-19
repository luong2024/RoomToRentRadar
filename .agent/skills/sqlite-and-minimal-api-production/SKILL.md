---
name: sqlite-and-minimal-api-production
description: Production patterns and guardrails for implementing SQLite Repositories (Microsoft.Data.Sqlite) in RoomToRentRadar.Infrastructure and ASP.NET Core Minimal API Endpoints in RoomToRentRadar.Api without package bloat or runtime cast errors.
---

# SQLite & ASP.NET Core Minimal API Production Guardrails Skill

## Mục đích
Hướng dẫn AI Agent khi thực hiện 10 Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`) viết code truy vấn SQLite trong `RoomToRentRadar.Infrastructure` và ánh xạ HTTP Endpoints trong `RoomToRentRadar.Api` đạt chuẩn Production, an toàn kiểu dữ liệu, chống SQL Injection và không làm hỏng `.csproj`.

## Khi nào kích hoạt Skill này
- Khi viết code trong `RoomToRentRadar.Infrastructure/Repositories/` hoặc `Services/`.
- Khi viết code trong `RoomToRentRadar.Api/Endpoints/*Endpoints.cs`.

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

## 2. Chuẩn Viết ASP.NET Core Minimal API Trong `RoomToRentRadar.Api/Endpoints/`

1. **Tuyệt đối KHÔNG sửa `RoomToRentRadar.Api/Program.cs`:**
   - `Program.cs` đã gọi sẵn tất cả `app.Map...Endpoints()`. Mỗi Task chỉ sửa đúng 1 file `*Endpoints.cs` của mình.
2. **Mẫu chuẩn viết Endpoint có kiểm tra đầu vào (Validation) và mã HTTP rõ ràng:**
   ```csharp
   // Ví dụ trong RoomDetailEndpoints.cs (T03.1):
   public static IEndpointRouteBuilder MapRoomDetailEndpoints(this IEndpointRouteBuilder app)
   {
       var group = app.MapGroup("/api/rooms");

       group.MapGet("/{id:int}", async (int id, IRoomListingRepository repo) =>
       {
           if (id <= 0)
               return Results.BadRequest(new { message = "Mã phòng không hợp lệ." });

           var room = await repo.GetByIdAsync(id);
           return room is not null
               ? Results.Ok(room)
               : Results.NotFound(new { message = "Không tìm thấy phòng trọ." });
       });

       group.MapGet("/{id:int}/similar", async (int id, string? ward, decimal? targetRent, IRoomListingRepository repo) =>
       {
           var similar = await repo.GetSimilarRoomsAsync(id, ward ?? "Quyết Thắng", targetRent ?? 1500000m, limit: 3);
           return Results.Ok(similar);
       });

       return app;
   }
   ```
3. **Khi hoàn thành một Repository/Service mới trong `Infrastructure`:**
   - Nhớ đăng ký `services.AddSingleton<IRoomListingRepository, RoomListingRepository>();` (hoặc Interface tương ứng của Task) vào đúng `<Feature>FeatureModule.cs` để ứng dụng tự động chuyển từ bản `InMemory*` sang bản SQLite thật.
