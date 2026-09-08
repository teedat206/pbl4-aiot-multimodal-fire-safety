# Kế hoạch phát triển phân hệ Mobile App (PBL4 AIoT System)

## Trạng thái hiện tại

- Đây là phân hệ Client (`Mobile_Monitor_App`) nằm trong cấu trúc Monorepo của dự án giám sát hỏa hoạn PBL4.
- Công nghệ cốt lõi: Flutter (Dart SDK `^3.13.2`), State Management bằng **GetX** (tối ưu tốc độ render cho IoT), giao tiếp thời gian thực hai chiều (SignalR), và Push Notification (Firebase Cloud Messaging - FCM).
- Ứng dụng tập trung tối ưu hóa biên dịch cho nền tảng **Android** để phục vụ kiểm thử hệ thống nhúng thực tế.

## Ưu tiên cao nhất

Xây dựng luồng nhận dữ liệu cảm biến đa phương thức (Khói, Gas sinh hoạt, Khí độc CO, Nhiệt độ) từ C# Backend qua WebSockets/SignalR, render biểu đồ realtime 60FPS không tràn bộ nhớ (Memory Leak), và xử lý cảnh báo khẩn cấp có khả năng xuyên thủng chế độ Doze Mode/Silent Mode của Android.

---

## Kế hoạch 6 bước triển khai App chuyên sâu

### 1. Chốt kiến trúc State Management và thư viện lõi

- **Kiến trúc thư mục:** Áp dụng mô hình **MVVM (Model-View-ViewModel)** kết hợp Clean Architecture để tách biệt hoàn toàn giao diện (View) khỏi logic mạng (SignalR Service).
  - `/lib/app/modules`: Chứa các View và Controller (GetXController).
  - `/lib/app/data/models`: Định nghĩa Object Model cho gói tin Telemetry từ Server.
  - `/lib/app/services`: Chứa `SignalRService` và `FCMService` chạy ngầm.
- **Thư viện lõi (Dependencies):** Cài đặt các package bắt buộc gồm `get` (quản lý state/route), `fl_chart` (vẽ đồ thị), `signalr_netcore` (kết nối Backend), `firebase_messaging` & `firebase_core` (Push Notification), `audioplayers` & `vibration` (Cảnh báo vật lý).
- **Verify:** Chạy thành công `flutter pub get`. Toàn bộ logic giao tiếp mạng được cô lập trong `services`, View chỉ làm nhiệm vụ lắng nghe biến phản ứng (Rx variables).

### 2. Xây dựng UI Dashboard và thuật toán Hàng đợi (FIFO)

- **Cấu trúc UI Dashboard:** Chia làm 3 phân vùng chính:
  1. Khu vực hiển thị Video Stream (MJPEG) từ ESP32-CAM.
  2. Khu vực Biểu đồ Đa biến (Nhiệt độ, Khí Gas sinh hoạt, Khí độc CO, Khói) cập nhật liên tục.
  3. Bảng trạng thái AI (Hiển thị 1 trong 5 Class mà Random Forest trả về).
- **Logic chống tràn RAM (Thuật toán FIFO):**
  - Khởi tạo mảng `List<FlSpot>` làm nguồn dữ liệu cho `fl_chart`.
  - Cài đặt giới hạn $N = 60$ (tương đương 60 điểm dữ liệu trong 1 phút).
  - Khi có điểm dữ liệu thứ 61 đổ về, thực thi hàm `list.removeAt(0)` để giải phóng bộ nhớ điểm cũ nhất trước khi `list.add()` điểm mới. Trục X của biểu đồ sẽ tự động tịnh tiến (Sliding Window).
- **Verify:** Sử dụng GetX `Obx()` để chỉ render lại chính xác Widget biểu đồ, không gọi `setState` làm re-build toàn bộ màn hình. Trình quản lý bộ nhớ (DevTools) xác nhận RAM không tăng tuyến tính theo thời gian.

### 3. Tích hợp kết nối SignalR với C# Backend

- **Vòng đời kết nối (Lifecycle):** Khởi tạo `HubConnectionBuilder` trong hàm `onInit()` của GetX Controller.
- **Cơ chế Reconnect:** Viết logic tự động đếm lùi và thử kết nối lại (Exponential Backoff) nếu Server C# bị sập hoặc App mất mạng (3s, 5s, 10s).
- **Xử lý Payload:** Lắng nghe kênh `ReceiveTelemetry`. Mapping chuỗi JSON trả về (`{"smoke": 0.85, "gas": 300, "co": 150, "temp": 38, "class": 2}`) vào Object Model và bắn dữ liệu vào luồng Stream của biểu đồ.
- **Verify:** App tự động nhận và hiển thị Data ngay khi Server C# vừa khởi động xong mà không cần người dùng thao tác pull-to-refresh.

### 4. Cấu hình chuông báo khẩn cấp (FCM) cấp độ OS

- **Xử lý dưới nền (Background Execution):** Khai báo hàm `@pragma('vm:entry-point')` cấp độ top-level để bắt gói tin FCM ngay cả khi App đã bị vuốt tắt (Terminated).
- **Quyền hệ thống (Android Manifest):** Cấp quyền `<uses-permission android:name="android.permission.WAKE_LOCK" />` và `VIBRATE` để cho phép App đánh thức màn hình sáng lên.
- **Logic Âm thanh & Rung:** Khi Payload FCM chứa cờ `"priority": "high"` và `"class": "4"`, kích hoạt vòng lặp `Vibration.vibrate(pattern: [500, 1000])` và phát file âm thanh `siren.mp3`.
- **Verify (Kiểm thử thực tế):**
  - *Trên máy ảo (Emulator):* Loa máy tính phát âm thanh còi hú thành công, màn hình Logcat ghi nhận lệnh gọi API Vibration mà không bị crash.
  - *Trên thiết bị thật (Physical Device):* Cắm cáp USB Debugging, điện thoại đang tắt màn hình và ở chế độ im lặng vẫn sáng đèn, rung bần bật và hú còi khi Backend phát tín hiệu.

### 5. Củng cố hiệu năng (Profiling) và Đóng gói APK

- **Kiểm thử hiệu năng (Profiling):** Chạy App ở chế độ Profile mode (`flutter run --profile`). Mở Flutter DevTools kiểm tra tab Memory và Performance để đảm bảo khung hình luôn duy trì > 50 FPS.
- **Đóng gói bảo mật:** Build file cài đặt với lệnh mã hóa mã nguồn: `flutter build apk --release --obfuscate --split-debug-info=./debug_info`.
- **Verify:** Sinh ra file `app-release.apk` cài đặt mượt mà trên thiết bị Android thật, tài liệu `README.md` cập nhật đủ các bước setup biến môi trường để sinh viên khác có thể clone về chạy thử.

### 6. Quy trình quản lý mã nguồn Git & Tích hợp chéo (Monorepo Workflow)

- **Rẽ nhánh và Code độc lập:** Bắt đầu công việc bằng cách cập nhật code mới nhất (`git pull origin main`), sau đó tạo nhánh riêng biệt cho tính năng Mobile (VD: `git checkout -b mobile/setup-ui`). Mọi thao tác viết code chỉ diễn ra bên trong thư mục `Mobile_Monitor_App`.
- **Sử dụng Dữ liệu giả (Mock Data):** Để đảm bảo tính độc lập, trong lúc chờ Backend xây dựng xong SignalR Hub, Mobile Developer sử dụng cấu trúc JSON giả lập (đã chốt ở Data Contracts) để nhồi vào thuật toán biểu đồ FIFO, đảm bảo tiến độ UI không bị đình trệ.
- **Commit và Push an toàn:** Khai báo thông điệp Git theo chuẩn Monorepo, ví dụ: `feat(mobile): hoàn thiện vẽ biểu đồ fl_chart 60fps`. Sau đó push nhánh lên GitHub và tạo Pull Request (PR) chờ review, tuyệt đối không push thẳng vào `main`.
- **Tích hợp thực tế (Cross-Integration):** Khi bạn Backend thông báo máy chủ đã sẵn sàng chạy trên mạng nội bộ (LAN), Mobile Developer tiến hành đổi Endpoint trong file config từ `10.0.2.2` (chạy máy ảo) sang địa chỉ IP tĩnh của Backend (VD: `192.168.1.5`). Build App lên thiết bị thật, đặt cạnh mạch phần cứng ESP32 để nghiệm thu độ trễ thực tế.
