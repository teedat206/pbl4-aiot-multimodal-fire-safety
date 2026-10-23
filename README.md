# PBL4: HỆ THỐNG AIoT DUNG HỢP DỮ LIỆU: GIÁM SÁT AN TOÀN KHÔNG GIAN KÍN VÀ DỰ BÁO SỚM HỎA HOẠN THEO THỜI GIAN THỰC

Hệ thống giám sát an toàn không gian kín và dự báo sớm hỏa hoạn theo thời gian thực sử dụng Trí tuệ nhân tạo (YOLOv8 & Random Forest) kết hợp Mạng lưới vạn vật kết nối (IoT).

## 🌟 Tính năng nổi bật

- **Giám sát thời gian thực:** Truyền phát luồng Video (MJPEG) và Dữ liệu cảm biến (Nhiệt độ, Gas, Khói, CO) với độ trễ < 50ms qua MQTT & WebSockets.
- **Dung hợp dữ liệu (Sensor Fusion):** Loại bỏ ngưỡng báo động tĩnh (Rule-based), sử dụng Random Forest để tự động quyết định mức độ nguy hiểm, chống báo động giả.
- **Cảnh báo xuyên im lặng:** Đẩy thông báo khẩn cấp (FCM) ép điện thoại rung và hú còi ngay cả khi đang ở chế độ ngủ (Doze mode/Silent mode).

##  Cấu trúc Monorepo (Project Structure)

Dự án được chia thành 4 phân hệ chính, sử dụng kiến trúc phân tán:

 - Edge_Firmware/          # C/C++ (FreeRTOS) chạy trên ESP32-S3 N16R8 CAM
 - Backend_AI_Server/      # C# ASP.NET Core API & SignalR Hub, chứa mô hình AI
 - Mobile_Monitor_App/     # Flutter/Dart (Android App) giám sát và nhận cảnh báo
 - AI_Training_Workspace/  # Python (Jupyter Notebook) dùng để train Random Forest/YOLOv8

##  Công nghệ sử dụng (Tech Stack)

- **Phần cứng:** ESP32-S3, Cảm biến MQ-2/MQ-7/MQ-135/DHT20, Mạch giảm áp LM2596.
- **AI/ML:** Scikit-Learn (Random Forest), Ultralytics (YOLOv8), ONNX Runtime.
- **Backend:** C# .NET Core, Entity Framework Core, MySQL, MQTT Broker.
- **Frontend (Mobile):** Flutter, GetX (State Management), Fl_Chart, Firebase Cloud Messaging.

##  Hướng dẫn khởi chạy (Getting Started)

*(Chi tiết xem tại README.md của từng thư mục con)*

1. Setup Database MySQL và chạy `Backend_AI_Server`.
2. Nạp firmware qua PlatformIO/Arduino IDE cho `Edge_Firmware`.
3. Cài đặt các package và build `Mobile_Monitor_App` lên máy ảo hoặc thiết bị Android.

---

**Được phát triển bởi nhóm sinh viên Đại học Bách Khoa - ĐHĐN (Nhóm Trường: Nguyễn Tiến Đạt).**
