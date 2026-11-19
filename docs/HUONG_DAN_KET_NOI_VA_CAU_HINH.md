# RoomToRentRadar — Hướng Dẫn Kết Nối & Cấu Hình Môi Trường Phát Triển

Tài liệu này hướng dẫn chi tiết cách cấu hình môi trường, khởi chạy Backend API, và kết nối với Mobile App (.NET MAUI) trên Windows Desktop, Điện thoại Android qua cáp USB và qua Wi-Fi không dây.

---

## 1. Tổng Quan Kiến Trúc Kết Nối

```
+-----------------------------------------------------------+
|                      MÁY TÍNH (PC)                        |
|                                                           |
|  +---------------------------+    +--------------------+  |
|  |  Azure Cosmos DB Emulator |    | Backend API        |  |
|  |  Cổng 8081                |<---| Cổng 49966 (HTTP)  |  |
|  +---------------------------+    +---------^----------+  |
|                                             |             |
+---------------------------------------------|-------------+
                                              |
                     +------------------------+------------------------+
                     |                                                 |
            (Cáp USB / adb reverse)                                (Mạng Wi-Fi LAN)
                     |                                                 |
         +-----------v------------+                       +------------v-----------+
         |  Điện thoại Android    |                       |  Điện thoại / Desktop  |
         |  http://127.0.0.1:49966|                       |  http://192.168.1.x:...|
         +------------------------+                       +------------------------+
```

* **Backend Web API**: ASP.NET Core, lắng nghe trên cổng `http://0.0.0.0:49966`.
* **Cơ sở dữ liệu**: Azure Cosmos DB Emulator (cổng `8081`) hoặc chế độ `in-memory` (chạy trên RAM).
* **Mobile Client**: .NET MAUI (`net10.0-android`, `net10.0-windows10.0.19041.0`).

---

## 2. Khởi Tạo Môi Trường Nhanh (Dành Cho Người Mới)

Để tránh lộ key bí mật lên Git mà người mới vẫn vào làm việc được ngay:

1. Clone dự án về máy.
2. Nhấp đúp chuột vào file **`setup-dev.bat`** (hoặc chạy `.\setup-dev.ps1` trong PowerShell) ở thư mục gốc.
3. Script sẽ tự động:
   * Lưu cấu hình bí mật vào **User Secrets** trên máy cá nhân (`Jwt:Secret`).
   * Tự động dò tìm công cụ `adb` và thiết lập cầu nối cổng nếu có điện thoại Android kết nối.

---

## 3. Khởi Chạy Backend API

### Cách 1: Chạy bằng Terminal (Khuyên dùng)
Mở PowerShell tại thư mục gốc của dự án:
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/RoomToRentRadar.Bootstrap/RoomToRentRadar.Bootstrap.csproj --no-launch-profile --urls "http://0.0.0.0:49966"
```
> **Lưu ý:**
> * Tham số `--no-launch-profile --urls "http://0.0.0.0:49966"` giúp Backend chỉ mở cổng HTTP `49966` mà không kích hoạt chuyển hướng HTTPS (`UseHttpsRedirection`), giúp các thiết bị Android kết nối trực tiếp mượt mà.
> * Nếu muốn chạy database tạm trên RAM (không cần bật Cosmos DB Emulator), chỉ cần thêm:  
>   `$env:CosmosDb__AccountEndpoint="in-memory"`

### Cách 2: Chạy bằng Visual Studio
1. Mở file `RoomToRentRadar.slnx`.
2. Chuột phải vào project **`RoomToRentRadar.Bootstrap`** $\rightarrow$ chọn **Set as Startup Project**.
3. Nhấn **Ctrl + F5** (Run without Debugging) hoặc **F5** (Debug).

---

## 4. Ba Phương Thức Chạy & Kết Nối Mobile App

### Phương thức 1: Chạy trực tiếp trên Windows Desktop (Đơn giản nhất, không cần thiết bị ngoài)

Không cần điện thoại, không cần cáp USB hay cấu hình mạng:
```powershell
dotnet run --project RoomToRentRadar/RoomToRentRadar.csproj -f net10.0-windows10.0.19041.0
```
Giao diện app sẽ mở ngay trên màn hình máy tính và kết nối thẳng vào Backend cổng `49966`.

---

### Phương thức 2: Kết nối Điện thoại Android qua Cáp USB

Dành cho kiểm thử trực tiếp trên điện thoại thật cắm cáp:

1. **Chuẩn bị trên điện thoại:**
   * Bật **Tùy chọn cho nhà phát triển** (Developer Options) $\rightarrow$ Bật **Gỡ lỗi USB** (USB Debugging).
   * Cắm cáp USB vào máy tính $\rightarrow$ Chọn chế độ **Truyền tệp / MTP**.
   * Mở khóa màn hình, khi có thông báo hỏi *"Cho phép gỡ lỗi USB?"* $\rightarrow$ Tích chọn *"Luôn cho phép từ máy tính này"* rồi bấm **Cho phép**.
2. **Kích hoạt cầu nối cổng USB trên máy tính:**
   ```powershell
   & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:49966 tcp:49966
   ```
3. **Cài đặt và chạy App lên điện thoại:**
   ```powershell
   dotnet build RoomToRentRadar/RoomToRentRadar.csproj -t:Run -f net10.0-android
   ```
   *(Hoặc chọn tên điện thoại trên thanh công cụ Visual Studio và bấm F5).*

---

### Phương thức 3: Kết nối Điện thoại Android qua Wi-Fi Không Dây (Wireless Debugging)

Dành cho trường hợp muốn nạp app và gọi API hoàn toàn không cần dây cáp (Android 11+):

#### Bước 3.1: Kết nối điện thoại với máy tính qua Wi-Fi
*(Đảm bảo điện thoại và máy tính kết nối chung một mạng Wi-Fi)*

* **Cách A — Nhanh nhất (Cắm cáp 3 giây để kích hoạt Wi-Fi rồi rút cáp):**
  1. Cắm cáp USB vào máy tính.
  2. Gõ lệnh:
     ```powershell
     & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" tcpip 5555
     ```
  3. **Rút dây cáp USB ra**.
  4. Gõ lệnh kết nối (thay IP bằng IP Wi-Fi của điện thoại):
     ```powershell
     & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" connect <IP_dien_thoai>:5555
     ```

* **Cách B — Ghép nối bằng mã 6 số (Không cần cắm cáp):**
  1. Trên điện thoại: Vào *Cài đặt > Tùy chọn nhà phát triển > Gỡ lỗi qua Wi-Fi (Bật)* $\rightarrow$ Bấm vào mục **Ghép nối thiết bị bằng mã ghép nối**.
  2. **Giữ nguyên popup hiện mã**, mở PowerShell gõ trên 1 dòng duy nhất:
     ```powershell
     & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" pair <IP_dien_thoai>:<cong_trong_popup> <ma_6_so>
     ```
  3. Sau khi báo `Successfully paired`, đóng popup và kết nối vào **cổng chính** hiển thị ở màn hình Gỡ lỗi Wi-Fi:
     ```powershell
     & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" connect <IP_dien_thoai>:<cong_chinh>
     ```

#### Bước 3.2: Kích hoạt cầu nối cổng qua sóng Wi-Fi
Khi lệnh `adb devices` đã hiển thị điện thoại kết nối qua IP:
```powershell
& "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:49966 tcp:49966
```

#### Bước 3.3: Chạy App không dây
Bấm **F5** trên Visual Studio hoặc gõ lệnh:
```powershell
dotnet build RoomToRentRadar/RoomToRentRadar.csproj -t:Run -f net10.0-android
```
App sẽ được truyền qua Wi-Fi và cài thẳng vào điện thoại!

---

## 5. Bảng Khắc Phục Sự Cố Thường Gặp (Troubleshooting)

| Hiện tượng / Báo lỗi | Nguyên nhân | Cách xử lý |
| :--- | :--- | :--- |
| `Unable to connect to authentication server. (Connection failure)` | Điện thoại không kết nối được cổng 49966 trên máy tính do chưa chạy `adb reverse` hoặc cáp USB bị lỏng. | 1. Kiểm tra lại cáp / kết nối Wi-Fi.<br>2. Chạy lại: `adb reverse tcp:49966 tcp:49966`. |
| `protocol fault (couldn't read status message)` khi ghép nối Wi-Fi | Popup mã ghép nối trên điện thoại bị đóng hoặc màn hình tắt trước khi nhập lệnh xong. | Giữ nguyên màn hình popup và nhập cả IP, cổng, mã 6 số trên cùng 1 dòng lệnh. |
| `adb : The term 'adb' is not recognized` | Thư mục `platform-tools` chưa có trong biến môi trường `PATH`. | Thêm vào PATH hoặc dùng đường dẫn đầy đủ: `& "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"`. |
| `HTTP 307 Temporary Redirect` sang HTTPS | Backend chạy có HTTPS làm điện thoại bị đá sang cổng 49965 không tin cậy SSL. | Khởi chạy Backend với cờ `--no-launch-profile --urls "http://0.0.0.0:49966"`. |
