# KẾ HOẠCH TỔNG THỂ DỰ ÁN PBL4 (6-BUCKET MASTER PLAN)
**Tên dự án:** Hệ thống AIoT Đa phương thức Giám sát Hỏa hoạn Thời gian thực.
**Mô hình quản lý:** Monorepo.

## 🪣 Bucket 1: Edge & Hardware (Trạm quan trắc viễn thông)
**Mục tiêu:** Số hóa dữ liệu vật lý và truyền luồng MJPEG thời gian thực.
- [ ] Thiết kế và hàn mạch PCB: ESP32-S3 N16R8, LM2596 (12V xuống 5V), chuyển mức logic 3.3V.
- [ ] Tích hợp mảng cảm biến: MQ-2, MQ-7, MQ-135, DHT20 (đọc qua ADC và I2C).
- [ ] Cấu hình FreeRTOS đa luồng (Dual-core): Core 0 xử lý WiFi/MQTT, Core 1 đọc cảm biến.
- [ ] Xử lý chống nhiễu bằng thuật toán lọc trung bình động (EMA).
- [ ] Thiết lập cơ chế LWT (Last Will and Testament) trên MQTT.

## 🪣 Bucket 2: Backend & Database (Hạ tầng Máy chủ)
**Mục tiêu:** Tiếp nhận dữ liệu tốc độ cao và định tuyến cảnh báo.
- [ ] Khởi tạo ASP.NET Core và MQTT Broker chạy ngầm bằng IHostedService.
- [ ] Thiết lập MySQL (Entity Framework Core).
- [ ] Xây dựng thuật toán In-memory Caching Batching (xả 100 bản ghi/lần hoặc 5s/lần) để chống nghẽn I/O Database.
- [ ] Xây dựng SignalR (WebSockets) Hub để bắn luồng dữ liệu (Push Event) xuống Mobile App.
- [ ] Cấu hình API đồng bộ với Data Contract (JSON) của toàn hệ thống.

## 🪣 Bucket 3: AI & Sensor Fusion (Trí tuệ Nhân tạo Đa phương thức)
**Mục tiêu:** Xóa bỏ rule-based tĩnh, thay bằng AI tự động suy luận và biểu quyết.
- [ ] Train mô hình YOLOv8 phát hiện Lửa/Khói và trích xuất Bounding Box / Confidence Score.
- [ ] Train mô hình Random Forest kết hợp các biến: [Lửa, Khói, Gas, CO, Khét, Nhiệt độ, Độ ẩm].
- [ ] Chuyển đổi Model sang định dạng ONNX và tích hợp vào Backend C# (Microsoft.ML.OnnxRuntime).
- [ ] Viết logic toán học bù trừ sai số vật lý (Data Drift) cho cảm biến MQ dựa trên Nhiệt độ/Độ ẩm từ DHT20.

## 🪣 Bucket 4: Mobile Client (Ứng dụng Giám sát thời gian thực)
**Mục tiêu:** Hiển thị 60FPS mượt mà và nhận cảnh báo khẩn cấp xuyên nền tảng.
- [ ] Xây dựng UI/UX theo chuẩn MVVM GetX (Dark Mode, IoT Dashboard).
- [ ] Triển khai thuật toán Hàng đợi trượt FIFO 60 điểm cho biểu đồ `fl_chart` (chống tràn RAM).
- [ ] Parse và render Video Stream MJPEG truyền trực tiếp từ ESP32-S3.
- [ ] Tích hợp Firebase Cloud Messaging (FCM) với cờ `priority: high`.
- [ ] Kích hoạt Background Isolate, Audioplayers và Vibration API để hú còi xuyên chế độ Im lặng (Silent Mode).

## 🪣 Bucket 5: System Integration & QA (Dung hợp & Kiểm thử)
**Mục tiêu:** Ghép nối 4 Buckets trên thành một hệ sinh thái khép kín và nghiệm thu.
- [ ] **Kịch bản mạng:** Rút điện Router WiFi để test còi hú khẩn cấp và trạng thái LWT (Offline) trên App.
- [ ] **Kịch bản AI:** Bơm khí gas + đốt nhang giả lập để xác minh khả năng biểu quyết chống báo động giả của thuật toán Random Forest.
- [ ] **Tối ưu hóa:** Cấu hình thanh trượt dung sai cảnh báo trên App để test độ nhạy của Model (nới lỏng/siết chặt).
- [ ] Đóng gói hoàn thiện (Firmware Bin, Docker Image cho Server, APK release cho Mobile).

## 🪣 Bucket 6: Monorepo Workflow & Version Control (Quy trình làm việc)
**Mục tiêu:** Đảm bảo 3 phân hệ phát triển độc lập, quản lý version an toàn, tích hợp không xung đột.
- [ ] **Quy tắc Rẽ nhánh (Branching):** Luôn lấy code mới (`git pull origin main`) và tạo nhánh tính năng riêng (VD: `git checkout -b edge/read-sensor`, `backend/mqtt-setup`). Tuyệt đối KHÔNG push thẳng vào `main`.
- [ ] **Phát triển độc lập:** Sử dụng Mock Data (JSON giả) để các phân hệ làm việc song song không chờ đợi nhau.
- [ ] **Quy chuẩn Commit:** Bắt buộc tuân thủ Conventional Commits với cú pháp `loại(phân_hệ): thông_điệp` (VD: `feat(ai): chuyển đổi thành công mô hình YOLO sang ONNX`). Bắt buộc tạo Pull Request (PR) để review trước khi merge.
- [ ] **Tích hợp chéo (Cross-Integration):** Khi các hệ thống kết nối, chuyển cấu hình từ Localhost/IP máy ảo sang IP tĩnh của Server LAN (VD: `192.168.1.x`) để test độ trễ thực tế với phần cứng thật.