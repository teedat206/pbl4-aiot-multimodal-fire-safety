# Edge Firmware

Firmware C/C++ cho ESP32-S3 N16R8 CAM. Thiết bị đọc cảm biến khí/nhiệt độ, phát MJPEG và gửi telemetry tới MQTT Broker.

## Phần cứng

- ESP32-S3 N16R8 CAM
- MQ-2 (khói/khí cháy), MQ-7 (CO), MQ-135 (chất lượng không khí/khí độc)
- DHT20 giao tiếp I2C
- Module hạ áp LM2596
- Mạch chuyển mức hoặc mạch chia áp 5V → 3.3V cho mọi ngõ ra analog MQ

> Không đưa ngõ ra 5V của cảm biến MQ trực tiếp vào GPIO ESP32-S3. ADC chỉ chịu mức tối đa 3.3V. Nối mass chung giữa ESP32, cảm biến và LM2596.

## Pinout đề xuất

Sơ đồ sau là baseline cho firmware; cần đối chiếu pin camera đã chiếm dụng trên đúng revision bo mạch trước khi hàn/nạp.

| Thiết bị | Tín hiệu | ESP32-S3 | Ghi chú |
| --- | --- | --- | --- |
| MQ-2 | AO qua 5V→3.3V | GPIO4 / ADC1_CH3 | Cấp nguồn cảm biến 5V |
| MQ-7 | AO qua 5V→3.3V | GPIO5 / ADC1_CH4 | Cấp nguồn cảm biến 5V |
| MQ-135 | AO qua 5V→3.3V | GPIO6 / ADC1_CH5 | Cấp nguồn cảm biến 5V |
| DHT20 | SDA | GPIO1 | I2C, pull-up 3.3V |
| DHT20 | SCL | GPIO2 | I2C, pull-up 3.3V |
| LM2596 | OUT+ | 5V/VIN board và MQ VCC | Chỉnh chính xác 5.0V trước khi nối |
| LM2596 | OUT− | GND | Mass chung |

Nếu GPIO4–6, GPIO1 hoặc GPIO2 trùng camera/PSRAM trên bo thực tế, đổi sang chân ADC/I2C còn trống và cập nhật hằng số pin trong firmware; không tự ý dùng chân boot/strapping.

## FreeRTOS

- **Core 0:** Wi-Fi, MQTT reconnect/publish và video/MJPEG.
- **Core 1:** lấy mẫu MQ, DHT20, lọc/chuẩn hóa dữ liệu và đóng gói telemetry.
- Dùng queue FreeRTOS để chuyển telemetry từ sensor task sang MQTT task; tránh truy cập buffer dùng chung không khóa.
- Đặt watchdog timeout hợp lý; task cảm biến không được block trong vòng lặp mạng.

## PlatformIO

1. Cài VS Code và PlatformIO IDE.
2. Mở thư mục `Edge_Firmware` sau khi đã thêm `platformio.ini` và thư mục `src/`.
3. Chọn board ESP32-S3 tương ứng bo mạch, cổng serial và upload speed phù hợp.
4. Build/upload bằng:

```bash
pio run
pio run --target upload
pio device monitor
```

## Wi-Fi và MQTT

Tạo `include/secrets.h` cục bộ (bị ignore) từ mẫu sau:

``cpp
#define WIFI_SSID "YOUR_WIFI_SSID"
#define WIFI_PASSWORD "YOUR_WIFI_PASSWORD"
#define MQTT_HOST "192.168.x.x"
#define MQTT_PORT 1883
#define MQTT_TOPIC "pbl4/telemetry"
```

Không commit SSID, mật khẩu Wi-Fi, MQTT credential hoặc địa chỉ broker production. Xác minh Serial Monitor báo Wi-Fi connected, MQTT connected và payload telemetry được publish trước khi kiểm thử Mobile App.
