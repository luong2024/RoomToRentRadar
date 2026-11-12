# Pull Request: Task T02.1 - Build Room Detail API (Description, Amenities, Rules)

## 📌 Description
Pull Request này hiện thực hóa **Task T02.1: Xây dựng API Chi tiết phòng trọ (Room Detail API)** theo chuẩn kiến trúc **Hexagonal Architecture (Ports & Adapters)**. Cung cấp endpoint cho phép ứng dụng di động truy vấn thông tin chi tiết của phòng trọ (mô tả, giá, diện tích, địa chỉ, trạng thái xác thực, v.v.) từ cơ sở dữ liệu Azure Cosmos DB.

---

## 🚀 Các thay đổi chính (Key Changes)

### 1. Domain Layer (`src/RoomToRentRadar.Domain`)
- Bổ sung thực thể `Room` (`Entities/Room.cs`) đại diện cho thông tin phòng trọ: `Id`, `Title`, `Description`, `Price`, `Area`, `Address`, `IsVerified`, `Status`.
- Bổ sung `DomainException` (`Exceptions/DomainException.cs`).
- Đảm bảo tính thuần khiết của Core Domain (hoàn toàn độc lập, không phụ thuộc Framework hay Database SDK).

### 2. Application Layer (`src/RoomToRentRadar.Application`)
- **DTO:** `RoomDetailsDto` chứa thông tin chi tiết phòng kèm định dạng tiền tệ và diện tích hiển thị (`FormattedPrice`, `FormattedArea`).
- **Ports:**
  - **Inbound Port:** Khai báo interface `IGetRoomDetailsUseCase`.
  - **Outbound Port:** Khai báo interface `IRoomRepository`.
- **Use Case:** Triển khai `GetRoomDetailsUseCase` xử lý logic kiểm tra phòng tồn tại, ném `NotFoundException` nếu không tìm thấy dữ liệu.

### 3. Adapters Layer (`src/RoomToRentRadar.Adapters`)
- **Inbound Web API Adapter:** Triển khai `RoomsController` (Thin Controller) với endpoint:
  - `GET /api/rooms/{id}`: Trả về `200 OK` (chi tiết phòng) hoặc `404 Not Found` (`ProblemDetails`).
- **Outbound Persistence Adapter:** Triển khai `CosmosRoomRepository` kết nối tới Azure Cosmos DB thông qua `Container.ReadItemAsync<Room>`.
- Quản lý gói phụ thuộc: Tham chiếu `Microsoft.Azure.Cosmos` (v3.64.0) và `Newtonsoft.Json` (v13.0.4).

### 4. Bootstrap Layer (`src/RoomToRentRadar.Bootstrap`)
- Cấu hình Dependency Injection trong `ServiceCollectionExtensions` đăng ký `Scoped` cho `IRoomRepository` và `IGetRoomDetailsUseCase`.
- Cấu hình khởi tạo `CosmosClient` và `Container` kết nối cục bộ với Azure Cosmos DB Emulator (`https://localhost:8081`).
- Bổ sung cấu hình `appsettings.json` cho Cosmos DB và hỗ trợ `UseDeveloperExceptionPage()` trong môi trường Development.

---

## 🧪 Kết quả kiểm thử & Đánh giá (Verification & Audit)

- [x] **Hexagonal Architecture Audit:** Đạt 100% 5 tiêu chí kiến trúc (Core thuần khiết, Thin Controller, Outbound Adapter, DI Scoped, Async/Await & CancellationToken).
- [x] **Build Solution:** `Build succeeded` (0 Warning, 0 Error).
- [x] **Kiểm thử thực tế với Postman & Azure Cosmos DB Emulator:**
  - `GET http://localhost:5000/api/rooms/room-01` $\rightarrow$ **`200 OK`** (Trả về dữ liệu JSON chi tiết phòng trọ chính xác).
  - `GET http://localhost:5000/api/rooms/room-999` $\rightarrow$ **`404 Not Found`** (Trả về ProblemDetails báo phòng không tồn tại).

---

## 🔗 Related Tickets
- Closes: **#T02.1**
