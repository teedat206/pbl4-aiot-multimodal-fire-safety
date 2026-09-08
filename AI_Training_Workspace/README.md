# AI Training Workspace

Không gian Python/Jupyter để chuẩn bị dữ liệu, huấn luyện Random Forest cho telemetry và YOLOv8 cho hình ảnh, sau đó xuất ONNX cho Backend.

## Môi trường

Có thể dùng Google Colab hoặc Jupyter Notebook cục bộ. Cài các thư viện lõi:

```bash
pip install scikit-learn pandas numpy jupyter ultralytics onnx onnxruntime skl2onnx
```

Khóa phiên bản dependency trong `requirements.txt` khi pipeline đã ổn định để bảo đảm có thể tái lập kết quả.

## Telemetry CSV và Random Forest

1. Đặt dataset `.csv` ngoài Git nếu có dữ liệu nhạy cảm hoặc dung lượng lớn; commit chỉ metadata/sampling đã được ẩn danh.
2. Kiểm tra schema tối thiểu: `timestamp, smoke, gas, co, temp, class`.
3. Loại bản ghi lỗi, xử lý missing value, chuẩn hóa đơn vị cảm biến và tránh data leakage theo thời gian/thiết bị.
4. Chia train/validation/test theo thời gian hoặc theo thiết bị, không random split mù với chuỗi telemetry.
5. Huấn luyện `RandomForestClassifier`, lưu seed, metrics và confusion matrix.

## YOLOv8

Chuẩn bị dataset ảnh theo cấu trúc Ultralytics và file `data.yaml`. Huấn luyện bằng notebook hoặc:

```bash
yolo detect train model=yolov8n.pt data=data.yaml epochs=100 imgsz=640
```

Đánh giá mAP, precision, recall và kiểm tra false positive trên điều kiện khói/ánh sáng thực tế trước khi bàn giao.

## Xuất ONNX và bàn giao

- Chuyển Random Forest bằng `skl2onnx` thành `rf_model.onnx`.
- Xuất YOLOv8:

```bash
yolo export model=best.pt format=onnx
```

- Kiểm thử ONNX bằng `onnxruntime`, so sánh output với model gốc và ghi nhận phiên bản, checksum, input shape, class mapping.
- Bàn giao `rf_model.onnx`, `yolov8n.onnx`, file class mapping và báo cáo metrics cho `Backend_AI_Server/models/`.

Không đưa API key Colab, dataset nhận diện cá nhân, hoặc dữ liệu telemetry thô có thông tin nhạy cảm vào repository.
