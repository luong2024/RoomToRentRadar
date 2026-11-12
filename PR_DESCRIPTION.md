# Pull Request: Task T02.1 - Build Room Detail API (Description, Amenities, Rules)

## 📌 Description
Pull Request này hoàn thiện **Task T02.1 (User Story US02 - Room Detail API)** theo chuẩn kiến trúc **Hexagonal Architecture (Ports & Adapters)**.
Cung cấp endpoint chi tiết phòng trọ phục vụ ứng dụng di động hiển thị thông tin mô tả, giá, diện tích, tiện nghi, nội quy, địa chỉ, trạng thái xác thực và xử lý nghiệp vụ bảo mật thông tin liên hệ chủ nhà cùng gợi ý phòng tương tự khi phòng không khả dụng.

---

## 🚀 Các thay đổi chính (Key Changes)

### 1. Domain Layer (`src/RoomToRentRadar.Domain`)
- **`Room.cs` (`Entities/Room.cs`)**:
  - Thực thể phòng trọ cốt lõi: `Id`, `Title`, `Description`, `Price`, `Area`, `Address`, `IsVerified`, `Status` (Enum: `Available`, `Rented`, `Pending`, `UnderMaintenance`), `Deposit`, `RoomType`, `Amenities` (danh sách tiện ích), `HouseRules` (danh sách nội quy), `LandlordPhone`, `LastUpdatedDate`.
  - Giữ vững tính thuần khiết của Core Domain: không phụ thuộc framework hay SDK lưu trữ.

### 2. Application Layer (`src/RoomToRentRadar.Application`)
- **DTOs (`DTOs/RoomDetailsDto.cs`)**:
  - `RoomDetailsDto`: Chứa đầy đủ thông tin phòng, tiện ích, nội quy, tiền cọc, số điện thoại chủ nhà (được xử lý che/hiển thị theo quyền), cờ `IsAvailable`, thông báo `UnavailableMessage`, và danh sách `SimilarRooms`.
  - `SimilarRoomDto`: DTO rút gọn cho các phòng gợi ý tương tự (`Id`, `Title`, `Price`, `Area`, `Address`, `ThumbnailUrl`).
- **Ports & Use Cases (`Ports/` & `UseCases/`)**:
  - **Inbound Port:** `IGetRoomDetailsUseCase`.
  - **Outbound Port:** `IRoomRepository` (`GetByIdAsync`, `GetSimilarRoomsAsync`).
  - **`GetRoomDetailsUseCase`**: Hiện thực toàn bộ các quy tắc nghiệp vụ của US02:
    - **AC1:** Trả về đầy đủ thông tin chi tiết phòng (mô tả, giá, diện tích, tiện nghi, nội quy, loại phòng).
    - **AC2 (Bảo mật liên hệ):** Với người dùng chưa đăng nhập hoặc tài khoản chưa xác thực (`IsVerified = false`), số điện thoại chủ trọ được che thành `"*** (Yêu cầu xác thực để xem)"`. Chỉ hiển thị số điện thoại thật khi tài khoản người dùng đã được xác thực (`IsVerified = true`, áp dụng cho mọi tài khoản đã xác thực: Student, Landlord, Admin) nhằm bảo vệ quyền riêng tư và chống cào dữ liệu (scraping).
    - **AC3 (Phòng không khả dụng):** Khi phòng có trạng thái khác `Available` (đã thuê/bảo trì): đặt `IsAvailable = false`, hiển thị `UnavailableMessage = "Phòng này hiện không khả dụng."`, che thông tin nhạy cảm (địa chỉ và số điện thoại), đồng thời tự động truy vấn danh sách tối đa 5 phòng tương tự thỏa mãn cả dải giá [70% - 130%] và dải diện tích [70% - 130%].
    - **AC4 (Không tồn tại):** Ném `NotFoundException` khi không tìm thấy phòng theo `id`.

### 3. Adapters Layer (`src/RoomToRentRadar.Adapters`)
- **Inbound Web API Controller (`RoomsController.cs`)**:
  - Endpoint `GET /api/rooms/{id}`: Cho phép truy cập nặc danh hoặc có JWT Bearer token.
  - Tự động trích xuất claim `IsVerified = true` từ JWT token để xác định quyền xem số điện thoại thật của chủ trọ.
  - Xử lý status code chuẩn RESTful: `200 OK`, `404 Not Found`.
- **Outbound Persistence (`CosmosDb/` & `InMemory/`)**:
  - **`CosmosDbOptions.cs`**: Bổ sung `AuthKey` đồng bộ với `AuthKeyOrResourceToken`.
  - **`CosmosDbContext.cs`**:
    - Ưu tiên sử dụng `AuthKey`, fallback sang `AuthKeyOrResourceToken`.
    - Tự động khởi tạo Container `"Rooms"` với Partition Key `"/id"` trong `EnsureDatabaseAndContainersAsync()`.
  - **`CosmosRoomRepository.cs`**:
    - `GetByIdAsync`: Đọc point-read tối ưu O(1) qua Partition Key `new PartitionKey(id)`.
    - `GetSimilarRoomsAsync`: Truy vấn SQL tìm phòng tương tự lọc theo cả dải giá và dải diện tích:
      `SELECT TOP 5 * FROM c WHERE c.id != @roomId AND c.status = 0 AND c.price >= @minPrice AND c.price <= @maxPrice AND c.area >= @minArea AND c.area <= @maxArea`.
  - **`InMemoryRoomRepository.cs`**:
    - Repository giả lập trong RAM phục vụ unit test và integration test.
    - Đồng bộ bộ lọc tìm kiếm phòng tương tự theo cả dải giá [70% - 130%] và dải diện tích [70% - 130%], bảo đảm kết quả trả về hoàn toàn nhất quán với `CosmosRoomRepository`.

### 4. Bootstrap Layer (`src/RoomToRentRadar.Bootstrap`)
- **`RoomDetailServiceRegistration.cs`**: Đăng ký Scoped cho `IRoomRepository`, `IGetRoomDetailsUseCase` và cấu hình Cosmos DB Client linh hoạt theo môi trường.
- **`Program.cs`**: Gọi `EnsureDatabaseAndContainersAsync()` tự động provision các container cần thiết khi app khởi động.

---

## 🧪 Kết quả kiểm thử (Verification & Test Coverage)

Toàn bộ test suite được thực thi tự động với **100% test thành công**:

- [x] **Domain Tests (`RoomToRentRadar.Domain.Tests`)**: **43/43 Passed**
  - Kiểm tra tính thuần khiết và bất biến của thực thể phòng trọ.
- [x] **Application Use Case Tests (`RoomToRentRadar.Application.Tests`)**: **31/31 Passed**
  - Kiểm tra đầy đủ AC1 (hiển thị chi tiết), AC2 (che số điện thoại), AC3 (phòng không khả dụng & gợi ý phòng tương tự), AC4 (báo lỗi 404).
- [x] **Adapter Unit Tests (`RoomToRentRadar.Adapters.Tests`)**: **51/51 Passed**
  - Kiểm tra controller bindings, authentication/claims mapping, exception handling.
  - Bổ sung bộ kiểm thử `InMemoryRoomRepositoryTests` kiểm tra:
    - Phòng phù hợp cả giá và diện tích được trả về.
    - Phòng có giá phù hợp nhưng diện tích nằm ngoài khoảng [70% - 130%] (quá nhỏ hoặc quá lớn) bị loại bỏ chính xác.
    - Phòng có diện tích phù hợp nhưng giá nằm ngoài khoảng bị loại bỏ.
    - Loại bỏ phòng mục tiêu và phòng không Available.
    - Giới hạn tối đa 5 phòng tương tự.
- [x] **Integration Tests (`RoomToRentRadar.IntegrationTests`)**:
  - **Môi trường có Cosmos DB Emulator chạy cục bộ**: **18/18 Passed, 0 Skipped**
    - `RoomDetailsIntegrationTests`: Kiểm tra toàn diện luồng API thực tế qua `WebApplicationFactory` cho cả khách nặc danh, tài khoản chưa xác thực, và tài khoản đã xác thực; kiểm chứng loại bỏ phòng có diện tích ngoài khoảng cho phép khỏi danh sách gợi ý.
    - `CosmosRoomRepositoryTests`: Kiểm chứng trực tiếp với Azure Cosmos DB Emulator (thêm phòng, đọc point-read theo partition key, truy vấn lọc dải giá và diện tích).
  - **Môi trường CI không có Cosmos DB Emulator**: **15 Passed, 3 Skipped**
    - 3 test của `CosmosRoomRepositoryTests` tự động nhận diện emulator không khả dụng và skip an toàn qua `Assert.Ignore()`, bảo đảm CI pipeline không bị ngắt quãng.
- [x] **MAUI Shared UI & Unit Tests (`RoomToRentRadarUnitTests`, `RoomToRentRadarUIAppiumTests`)**: **2/2 Passed**

---

## 🔗 Related Tickets
- Closes: **#T02.1**
- Implements: **US02 - Room Detail API (Description, Amenities, Rules)**
