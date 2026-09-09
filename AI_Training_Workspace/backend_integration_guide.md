# Tài liệu Bàn giao AI Module – Baseline Heuristic Fusion Engine

> **Phiên bản:** 1.0 &nbsp;|&nbsp; **Ngày tạo:** 2026-09-09 &nbsp;|&nbsp; **Trạng thái:** ✅ Sẵn sàng tích hợp  
> **Thuộc:** `AI_Training_Workspace` → bàn giao sang `Backend_AI_Server`

---

## 1. Mục đích

File [`scripts/baseline_heuristic.py`](./scripts/baseline_heuristic.py) cung cấp **Heuristic Fusion Engine** — một bộ luật ngưỡng (threshold-based rule engine) để phân loại mức độ nguy hiểm cháy nổ từ dữ liệu đa cảm biến.

**Vai trò trong Nửa kỳ đầu (Phase 1):**

Trong giai đoạn này, mô hình Random Forest chưa được huấn luyện vì chưa đủ dataset thực tế. Heuristic Engine đóng vai trò **thay thế tạm thời** (fallback) để:

- Cung cấp nhãn phân loại (`class 0–4`) ngay lập tức cho luồng MQTT/SignalR mà không cần chờ AI.
- Làm cơ sở **sinh nhãn tự động** (`auto-labeling`) khi thu thập dữ liệu cảm biến thực tế, phục vụ bước huấn luyện Random Forest ở Phase 2.
- Cho phép team Backend và Mobile **tích hợp end-to-end** từ sớm bằng output có ý nghĩa thực sự thay vì mock cứng.

---

## 2. Đặc tả Giao diện (API Contract)

### 2.1 Đầu vào (Input)

Hàm nhận **5 tham số số thực** từ gói tin telemetry của ESP32:

| Tham số | Kiểu dữ liệu | Đơn vị | Mô tả |
|---------|-------------|--------|-------|
| `temp`  | `float`     | °C     | Nhiệt độ môi trường |
| `hum`   | `float`     | %      | Độ ẩm tương đối (0–100) |
| `smoke` | `float`     | 0–100  | Nồng độ khói (chuẩn hóa từ ADC cảm biến MQ-2) |
| `gas`   | `float`     | ppm    | Nồng độ gas LPG/Butane (MQ-5 hoặc MQ-6) |
| `co`    | `float`     | ppm    | Nồng độ CO – Carbon Monoxide (MQ-7 hoặc MQ-9) |

> **Lưu ý:** Trường `hum` tham gia vào **bước bù trừ nhiễu (humidity compensation)** bên trong engine. Backend **bắt buộc** phải truyền vào, không được bỏ qua.

### 2.2 Đầu ra (Output)

Hàm trả về **một số nguyên duy nhất** trong khoảng `[0, 4]`:

```
int  →  { 0 | 1 | 2 | 3 | 4 }
```

---

## 3. Bảng Class Mapping

| Class | Nhãn tiếng Anh | Nhãn tiếng Việt | Mô tả tình huống | Hành động đề xuất |
|:-----:|----------------|-----------------|------------------|-------------------|
| **0** | Normal | Bình thường | Môi trường ổn định, không có dấu hiệu bất thường | Không cần phản hồi |
| **1** | Early Warning | Cảnh báo sớm | Một hoặc nhiều chỉ số vượt ngưỡng nền nhẹ | Gửi thông báo push nhắc nhở |
| **2** | Gas Leakage | Rò rỉ Gas | Gas LPG/Butane tăng vọt, chưa có dấu hiệu lửa | ⚠️ Cảnh báo khẩn cấp, đề nghị thông gió |
| **3** | Smoldering Fire | Cháy âm ỉ | Nhiệt + khói + CO tăng, đám cháy chưa bùng phát | 🔥 Cảnh báo khẩn cấp, yêu cầu sơ tán |
| **4** | Critical Flaming Fire | Hỏa hoạn bùng phát | Nhiệt độ cực cao, khói đặc, CO nguy hiểm | 🆘 Báo động tối đa, gọi cứu hỏa ngay |

---

## 4. Hướng dẫn tích hợp cho Kỹ sư Backend (C#)

> **Nhiệm vụ:** Chuyển thể logic hàm `evaluate_risk()` từ Python sang C# và gắn vào luồng xử lý MQTT / SignalR.

### Luồng dữ liệu tích hợp

```
ESP32 (MQTT publish)
    │  payload JSON: { "temp": 53.0, "hum": 32.0, "smoke": 58, "gas": 350, "co": 135 }
    ▼
Backend C# (MQTT Subscribe Handler)
    │  Deserialize JSON → TelemetryPayload object
    ▼
HeuristicFusionEngine.EvaluateRisk(temp, hum, smoke, gas, co)
    │  return: int classLabel  (0–4)
    ▼
SignalR Hub (Broadcast sang Mobile App)
    │  payload: { ...telemetry, "class": 3 }
    ▼
Mobile App Flutter (Hiển thị Dashboard + Cảnh báo)
```

### Checklist tích hợp

- [ ] Thêm class `HeuristicFusionEngine` vào project `Backend_AI_Server`.
- [ ] Inject `HeuristicFusionEngine` vào MQTT Message Handler qua Dependency Injection.
- [ ] Sau khi nhận MQTT payload, gọi `EvaluateRisk()` và đính kết quả vào object trả về SignalR.
- [ ] Map `class == 4` hoặc `class == 3` → trigger FCM Push Notification `"priority": "high"`.
- [ ] Khi model ONNX sẵn sàng (Phase 2), thay thế lời gọi `HeuristicFusionEngine` bằng `OnnxInferenceEngine` cùng signature — **không cần sửa phần còn lại của luồng**.

---

## 5. Mã nguồn C# tham chiếu – `HeuristicFusionEngine`

Sao chép class dưới đây vào `Backend_AI_Server/Services/HeuristicFusionEngine.cs`:

```csharp
// =============================================================================
// File: HeuristicFusionEngine.cs
// Namespace: Backend_AI_Server.Services
//
// Mô tả: Chuyển thể logic Python evaluate_risk() sang C#.
//        Dùng làm Fallback Engine trước khi ONNX model sẵn sàng (Phase 1).
//        Khi chuyển sang Phase 2, thay thế bằng OnnxInferenceEngine
//        có cùng interface IFireRiskEngine.
// =============================================================================

namespace Backend_AI_Server.Services;

/// <summary>
/// Contract dùng chung cho cả Heuristic Engine (Phase 1)
/// và ONNX Inference Engine (Phase 2).
/// Giúp Backend không cần sửa code khi nâng cấp AI model.
/// </summary>
public interface IFireRiskEngine
{
    /// <summary>
    /// Đánh giá mức độ rủi ro cháy nổ từ dữ liệu cảm biến.
    /// </summary>
    /// <returns>Class label từ 0 (bình thường) đến 4 (hỏa hoạn bùng phát).</returns>
    int EvaluateRisk(float temp, float hum, float smoke, float gas, float co);
}

/// <summary>
/// Threshold-based Heuristic Fusion Engine.
/// Phân loại môi trường thành 5 class dựa trên ngưỡng cảm biến.
/// Đăng ký vào DI container dưới dạng Singleton.
/// </summary>
public sealed class HeuristicFusionEngine : IFireRiskEngine
{
    // =========================================================================
    // NGƯỠNG CẢM BIẾN – Đồng bộ với baseline_heuristic.py
    // Chỉnh sửa tại đây nếu phần cứng thực tế cho kết quả lệch.
    // =========================================================================

    // Nhiệt độ (°C)
    private const float TempWarn     = 35f;
    private const float TempHigh     = 50f;
    private const float TempCritical = 65f;

    // Khói – thang 0–100
    private const float SmokeWarn     = 20f;
    private const float SmokeHigh     = 45f;
    private const float SmokeCritical = 75f;

    // Gas LPG/Butane (ppm)
    private const float GasWarn     = 200f;
    private const float GasHigh     = 800f;
    private const float GasCritical = 1200f;

    // CO – Carbon Monoxide (ppm)
    private const float CoWarn     = 25f;
    private const float CoHigh     = 80f;
    private const float CoCritical = 200f;

    // Bù trừ độ ẩm
    private const float HumCompensationThreshold = 65f;
    private const float HumCompensationMax       = 90f;
    private const float HumCompensationFactor    = 0.003f;

    // =========================================================================
    // PHƯƠNG THỨC CHÍNH
    // =========================================================================

    /// <inheritdoc />
    public int EvaluateRisk(float temp, float hum, float smoke, float gas, float co)
    {
        // Bước 1: Bù trừ cảm biến khói theo độ ẩm
        // Môi trường ẩm cao làm ADC cảm biến khói đọc sai lệch → giảm nhiễu.
        float smokeAdjusted = smoke;
        if (hum > HumCompensationThreshold)
        {
            float excessHum = Math.Min(hum, HumCompensationMax) - HumCompensationThreshold;
            float correction = 1f - excessHum * HumCompensationFactor;
            smokeAdjusted = smoke * Math.Max(correction, 0.7f); // Không giảm quá 30%
        }

        // Bước 2: Phân loại từ nguy hiểm nhất xuống thấp nhất

        // Class 4 – Hỏa hoạn bùng phát
        if ((temp >= TempCritical && smokeAdjusted >= SmokeCritical) ||
            (smokeAdjusted >= SmokeCritical && co >= CoCritical))
        {
            return 4;
        }

        // Class 3 – Cháy âm ỉ (Smoldering)
        if ((temp >= TempHigh && smokeAdjusted >= SmokeHigh && co >= CoHigh) ||
            (temp >= TempCritical && smokeAdjusted >= SmokeWarn))
        {
            return 3;
        }

        // Class 2 – Rò rỉ Gas (chưa có lửa)
        // Phân biệt: nếu có lửa thì smoke và temp sẽ cùng tăng → rơi vào Class 3/4
        if (gas >= GasCritical && smokeAdjusted < SmokeWarn && temp < TempHigh)
        {
            return 2;
        }

        // Class 1 – Cảnh báo sớm
        if (temp    >= TempWarn  ||
            smokeAdjusted >= SmokeWarn ||
            gas     >= GasWarn   ||
            co      >= CoWarn)
        {
            return 1;
        }

        // Class 0 – Bình thường
        return 0;
    }
}
```

### Cách đăng ký Dependency Injection trong `Program.cs`

```csharp
// Program.cs
builder.Services.AddSingleton<IFireRiskEngine, HeuristicFusionEngine>();

// Khi chuyển sang Phase 2 với ONNX, chỉ cần đổi 1 dòng này:
// builder.Services.AddSingleton<IFireRiskEngine, OnnxInferenceEngine>();
```

### Cách gọi trong MQTT Message Handler

```csharp
// MqttMessageHandler.cs (ví dụ)
public class MqttMessageHandler
{
    private readonly IFireRiskEngine _riskEngine;
    private readonly IHubContext<FireAlertHub> _hubContext;

    public MqttMessageHandler(IFireRiskEngine riskEngine, IHubContext<FireAlertHub> hubContext)
    {
        _riskEngine  = riskEngine;
        _hubContext  = hubContext;
    }

    public async Task HandleMessageAsync(string jsonPayload)
    {
        // Deserialize telemetry từ ESP32
        var telemetry = JsonSerializer.Deserialize<TelemetryPayload>(jsonPayload);

        // Gọi Heuristic Engine (hoặc ONNX Engine ở Phase 2 – cùng interface)
        int riskClass = _riskEngine.EvaluateRisk(
            telemetry.Temp,
            telemetry.Hum,
            telemetry.Smoke,
            telemetry.Gas,
            telemetry.Co
        );

        // Đính nhãn class vào payload và broadcast qua SignalR
        var broadcastPayload = new
        {
            telemetry.Temp, telemetry.Hum,
            telemetry.Smoke, telemetry.Gas, telemetry.Co,
            Class     = riskClass,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", broadcastPayload);

        // Trigger FCM Push Notification nếu nguy hiểm cao
        if (riskClass >= 3)
        {
            // TODO: Gọi FCM Service với priority = high
        }
    }
}
```

---

## 6. Lịch sử phiên bản

| Phiên bản | Ngày | Ghi chú |
|-----------|------|---------|
| 1.0 | 2026-09-09 | Baseline heuristic Python → C# bàn giao Phase 1 |
| *(kế hoạch)* 2.0 | Phase 2 | Thay thế bằng `OnnxInferenceEngine` sử dụng `rf_model.onnx` |

---

> **Liên hệ:** Team AI Training Workspace – PBL4 AIoT Multimodal Fire Safety  
> Mọi thắc mắc về ngưỡng cảm biến hoặc Data Contract, xem thêm [`data/mock_sensor_data.json`](./data/mock_sensor_data.json) và [`scripts/baseline_heuristic.py`](./scripts/baseline_heuristic.py).
