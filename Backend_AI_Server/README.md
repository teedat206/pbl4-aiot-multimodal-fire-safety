# Backend AI Server

ASP.NET Core Backend của PBL4: nhận dữ liệu IoT, lưu MySQL, thực thi suy luận ONNX và phát realtime tới Mobile App bằng SignalR Hub.

## Yêu cầu

- .NET SDK phiên bản được ghim trong solution (khuyến nghị .NET LTS)
- MySQL Server và database dành riêng cho PBL4
- Entity Framework Core provider cho MySQL
- ONNX Runtime cho mô-đun suy luận

Sau khi có solution/project:

```bash
dotnet restore
dotnet ef database update
dotnet run
```

## Cấu hình ứng dụng

Tạo `appsettings.json` cục bộ từ `appsettings.example.json` (nếu có). Cấu hình gồm chuỗi kết nối MySQL, endpoint MQTT/SignalR cần thiết, đường dẫn model và logging.

```json
{
  "ConnectionStrings": {
    "Pbl4Database": "Server=localhost;Database=pbl4;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
  },
  "AiModels": {
    "RandomForestOnnxPath": "models/rf_model.onnx",
    "YoloOnnxPath": "models/yolov8n.onnx"
  }
}
```

> `appsettings.json` và các biến thể environment-specific bị chặn bởi `.gitignore`. Không commit connection string, mật khẩu database, token MQTT hay secret bất kỳ. Chỉ commit file `.example` đã loại bỏ credential.

## MySQL và Entity Framework Core

1. Tạo database với quyền tối thiểu cho service account.
2. Đặt connection string qua User Secrets, biến môi trường hoặc `appsettings.json` cục bộ.
3. Khai báo DbContext và entities telemetry/alert.
4. Tạo migration, review SQL, rồi áp dụng migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## SignalR

Expose Hub để Mobile App kết nối và nhận `ReceiveTelemetry`. Payload phải có schema ổn định, ví dụ:

```json
{"smoke":0.85,"gas":300,"co":150,"temp":38,"class":2}
```

Client cần nhận được broadcast ngay sau khi Backend khởi động lại; server cần xử lý reconnect và xác thực trước khi mở public endpoint production.

## ONNX models

Đặt model đã được kiểm định vào thư mục runtime, ví dụ:

```text
models/
├── rf_model.onnx
└── yolov8n.onnx
```

Đảm bảo các file có thuộc tính copy-to-output trong project file. Khi deploy, xác minh hash/version model, quyền đọc tệp và log kết quả khởi tạo ONNX Runtime. Không thay model production khi chưa có version, đánh giá độ chính xác và kế hoạch rollback.
