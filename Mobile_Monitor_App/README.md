# Mobile Monitor App

Flutter client cho hệ thống PBL4: nhận telemetry qua SignalR, hiển thị biểu đồ realtime và phát cảnh báo khẩn cấp trên Android.

## Yêu cầu môi trường

- Flutter SDK tương thích Dart `^3.13.2`
- Android Studio, Android SDK và thiết bị/emulator Android
- Firebase project đã bật Firebase Cloud Messaging (FCM)
- Backend SignalR đang chạy và thiết bị Android có thể truy cập địa chỉ Backend

Kiểm tra môi trường:

```bash
flutter doctor
flutter pub get
```

## Cấu hình Firebase Cloud Messaging

1. Đăng ký Android app trong Firebase bằng đúng application ID của app.
2. Tải `google-services.json` từ Firebase Console.
3. Đặt file tại `android/app/google-services.json`.
4. Cấu hình Gradle Firebase theo hướng dẫn của Firebase trước khi build.

> `google-services.json` bị chặn bởi `.gitignore`. Không commit file này hoặc thông tin Firebase riêng tư. Mỗi môi trường dùng tệp cấu hình riêng.

## Telemetry, SignalR và biểu đồ

SignalR Client kết nối Hub Backend và lắng nghe kênh `ReceiveTelemetry`. Payload kỳ vọng:

```json
{"smoke": 0.85, "gas": 300, "co": 150, "temp": 38, "class": 2}
```

Khi triển khai:

- Cô lập kết nối trong `lib/app/services/signalr_service.dart`.
- Đưa dữ liệu vào model telemetry và các biến phản ứng GetX.
- Dùng `List<FlSpot>` riêng cho mỗi chỉ số với hàng đợi FIFO tối đa 60 điểm: trước khi thêm điểm thứ 61, gọi `removeAt(0)`.
- Chỉ bọc widget biểu đồ bằng `Obx()`; không rebuild toàn bộ Dashboard cho mỗi gói tin.
- Cấu hình endpoint SignalR theo môi trường, không hard-code URL production hay token trong mã nguồn.

## Chạy trên Android thật

1. Bật Developer options và USB debugging trên điện thoại.
2. Kết nối điện thoại, kiểm tra bằng `flutter devices`.
3. Chạy `flutter run` từ thư mục này.
4. Gửi telemetry SignalR và FCM mức khẩn cấp từ Backend.
5. Kiểm tra biểu đồ, rung và âm thanh khi màn hình tắt/chế độ im lặng.

Để đo hiệu năng:

```bash
flutter run --profile
```

Dùng Flutter DevTools để theo dõi FPS và Memory; mục tiêu là không tăng RAM tuyến tính trong lúc nhận dữ liệu liên tục.
