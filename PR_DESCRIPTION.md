# Pull Request: Task T04.1 - Build Search API with Price, Area, and Distance Filters (US04)

## 📌 Description
Pull Request này hiện thực hóa **Task T04.1 (User Story US04 - Search API with filters)** theo chuẩn kiến trúc **Hexagonal Architecture (Ports & Adapters)**. 
Cung cấp endpoint tìm kiếm phòng trọ linh hoạt cho ứng dụng di động dựa trên đa tiêu chí lọc: khoảng giá, diện tích, loại phòng, từ khóa và bán kính khoảng cách địa lý (GPS), có phân trang và bảo vệ nghiêm ngặt tài nguyên hệ thống.

---

## 🚀 Các thay đổi chính (Key Changes)

### 1. API Contract & DTOs (`src/RoomToRentRadar.Application/DTOs`)
- **`SearchRoomsQuery`**: Nhận các tham số tìm kiếm nullable: `MinPrice`, `MaxPrice`, `MinArea`, `MaxArea`, `RoomType`, `Keyword`, `Latitude`, `Longitude`, `RadiusInKm`, cùng `PageNumber` (mặc định 1) và `PageSize` (mặc định 10).
- **`RoomSummaryDto`**: DTO tinh gọn phục vụ hiển thị danh sách phòng: `Id`, `Title`, `Price`, `Area`, `RoomType`, `IsVerified`, `DistanceInKm`.
- **`SearchRoomsResult`**: DTO phản hồi chứa danh sách `Items`, `TotalCount`, `TotalPages`, `CurrentPage`, và danh sách `ActiveFilters` (tên các filter đang có hiệu lực).

### 2. Core & Ports (`src/RoomToRentRadar.Domain` & `Application`)
- **Domain Layer (`Entities/Room.cs`)**:
  - Giữ vững tính thuần khiết của Domain Core: chỉ chứa các thuộc tính tọa độ nguyên thủy `double? Latitude` và `double? Longitude`.
  - **Tuyệt đối không rò rỉ cấu trúc hạ tầng GeoJSON của Cosmos DB vào Domain**.
- **Outbound Port (`Ports/Outbound/IRoomRepository.cs`)**:
  - Bổ sung phương thức `Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct)`.
- **Inbound Port & Use Case (`UseCases/SearchRoomsUseCase.cs`)**:
  - Triển khai `ISearchRoomsUseCase`.
  - **Validate đầu vào nghiêm ngặt**: kiểm tra `MinPrice <= MaxPrice`, `MinArea <= MaxArea`, các giá trị không âm, tọa độ hợp lệ, và bắt buộc truyền tọa độ khi lọc theo bán kính.
  - **Chống tấn công từ chối dịch vụ (Pagination DoS Protection)**: Giới hạn cứng `PageSize` tối đa là 50 và `PageNumber >= 1`. Các request vượt quá 50 sẽ bị từ chối ngay lập tức (`400 Bad Request`).
  - **Bảo đảm quy tắc nghiệp vụ AC4**: Chỉ tìm kiếm và trả về các phòng có `Status == Available` và `IsVerified == true`.
  - **Tính toán khoảng cách động**: Tự động tính `DistanceInKm` theo công thức toán học **Haversine** dựa trên tọa độ người dùng truyền vào và tọa độ phòng. **Không hardcode bất kỳ tọa độ địa phương nào vào Domain/Use Case**.

### 3. Outbound Persistence Adapters (`src/RoomToRentRadar.Adapters`)
- **`RoomDocument.cs` & `CosmosPoint.cs`**: Tách riêng cấu trúc lưu trữ và GeoJSON Point (`type: 'Point'`, `coordinates: [lon, lat]`) về đúng tầng Adapter, mapping 2 chiều với Domain Entity `Room`.
- **`CosmosRoomRepository.cs`**:
  - Xây dựng câu truy vấn SQL động kèm tham số hóa (@params) chống SQL Injection: lọc `status = 0`, `isVerified = true`, giá, diện tích, loại phòng, từ khóa.
  - Sử dụng hàm không gian tích hợp của Cosmos DB: `ST_DISTANCE(c.Location, {'type': 'Point', 'coordinates': [@lon, @lat]}) <= (@radius * 1000)`.
  - **An toàn vận hành đa phân vùng (Cross-Partition Query)**: Cấu hình tường minh `QueryRequestOptions { MaxItemCount = pageSize }` khi gọi `GetItemQueryIterator` cho cả câu đếm và câu phân trang (`OFFSET @offset LIMIT @limit`).
- **`InMemoryRoomRepository.cs`**:
  - Cài đặt `SearchAsync` bằng LINQ phục vụ chạy automated testing nội bộ.
  - Hiện thực hàm `CalculateHaversineDistanceKm` để lọc bán kính.

### 4. Inbound Web API Controller (`src/RoomToRentRadar.Adapters/Inbound/WebApi/Controllers`)
- **`RoomsController.cs`**:
  - Bổ sung endpoint: `GET /api/rooms` nhận `[FromQuery] SearchRoomsQuery query`.
  - Gắn `[AllowAnonymous]` cho phép sinh viên/khách vãng lai tìm kiếm phòng trọ mà không bắt buộc đăng nhập.
  - Trả về `200 OK(SearchRoomsResult)` hoặc `400 BadRequest(ProblemDetails)` nếu tham số filter/phân trang không hợp lệ.

### 5. Dependency Injection (`src/RoomToRentRadar.Bootstrap`)
- Đăng ký scoped `ISearchRoomsUseCase` -> `SearchRoomsUseCase` trong `RoomDetailServiceRegistration` và `ServiceCollectionExtensions`.

---

## 🧪 Kết quả kiểm thử (Verification & Test Coverage)

Toàn bộ test suite được thực thi tự động và đạt 100% tỷ lệ pass:

- [x] **Kiến trúc Hexagonal & Domain Purity (`RoomToRentRadar.Domain.Tests`)**: **43/43 Passed**
  - Đảm bảo Domain Core không bị ô nhiễm bởi bất kỳ thư viện bên ngoài hay cơ chế lưu trữ Cosmos DB nào.
- [x] **Unit Tests Use Case (`RoomToRentRadar.Application.Tests`)**: **57/57 Passed**
  - Kiểm tra toàn diện các trường hợp validation (giá, diện tích, tọa độ, bán kính).
  - Kiểm tra chặn `PageSize > 50`, `PageSize <= 0`, `PageNumber < 1`.
  - Kiểm tra bất biến AC4 (chỉ phòng khả dụng & đã xác thực mới xuất hiện trong kết quả).
  - Kiểm tra tính đúng đắn của công thức Haversine và danh sách `ActiveFilters`.
- [x] **Adapter Unit Tests (`RoomToRentRadar.Adapters.Tests`)**: **49/49 Passed**
  - Kiểm tra controller bindings và status code mapping.
- [x] **Integration Tests (`RoomToRentRadar.IntegrationTests`)**: **24/24 Passed**
  - Bơm biến môi trường giả lập trong `SetUp()` để bypass Fail-Fast Cosmos DB.
  - Test gọi API nặc danh (`[AllowAnonymous]`) trả về `200 OK`.
  - Test truy vấn kết hợp nhiều filter (khoảng giá, diện tích, loại phòng, từ khóa, bán kính GPS).
  - Test bảo vệ DoS khi hacker gửi `?pageSize=1000000` $\rightarrow$ bị chặn với `400 Bad Request`.

---

## 🔗 Related Tickets
- Closes: **#T04.1**
- Implements: **US04 - Search API with filters**
