using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.DTOs;

/// <summary>
/// Data Transfer Object nhận payload từ MQTT topic pbl4/telemetry.
/// Schema khớp với Data Contract đã thống nhất giữa Edge và Backend.
/// </summary>
public record TelemetryPayloadDto
{
    /// <summary>Device ID của ESP32 gửi lên (VD: "esp32-cam-01").</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Chỉ số khói từ MQ-2 (0.0 - 1.0 normalized hoặc raw ADC).</summary>
    public float Smoke { get; init; }

    /// <summary>Nồng độ khí gas sinh hoạt (ppm).</summary>
    public float Gas { get; init; }

    /// <summary>Nồng độ khí độc CO (ppm).</summary>
    public float Co { get; init; }

    /// <summary>Nhiệt độ môi trường (°C).</summary>
    public float Temp { get; init; }

    /// <summary>Độ ẩm tương đối môi trường (%).</summary>
    public float Humidity { get; init; }

    /// <summary>Chỉ số chất lượng không khí (AQI).</summary>
    public float Aqi { get; init; }

    /// <summary>Timestamp UNIX (giây) từ thiết bị Edge (dùng để phát hiện gói tin trễ).</summary>
    public long? EdgeTimestamp { get; init; }
}

/// <summary>
/// Data Transfer Object nhận kết quả phân tích thị giác từ AI Server qua MQTT topic pbl4/vision.
/// </summary>
public record VisionPayloadDto
{
    /// <summary>Device ID đồng bộ với Edge &amp; Telemetry (VD: "esp32-s3-001").</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Chỉ số lớp YOLOv8 phát hiện được (VD: 0 = Fire, 1 = Smoke).</summary>
    public int YoloClass { get; init; }

    /// <summary>Độ tin cậy YOLOv8 (0.0 - 1.0).</summary>
    public float YoloConf { get; init; }

    /// <summary>Diện tích bounding box đối tượng phát hiện (pixel²).</summary>
    public float BboxArea { get; init; }

    /// <summary>Cờ xác nhận có phát hiện đối tượng hay không.</summary>
    public bool Detected { get; init; }
}

/// <summary>
/// Payload được SignalR broadcast xuống Mobile App (ReceiveTelemetry).
/// Chứa đầy đủ thông tin cảm biến + kết quả phân loại AI.
/// </summary>
public record TelemetryBroadcastDto
{
    public string DeviceId { get; init; } = string.Empty;
    public float Smoke { get; init; }
    public float Gas { get; init; }
    public float Co { get; init; }
    public float Temp { get; init; }

    /// <summary>Kết quả phân loại từ Fusion Decision Matrix (0-4).</summary>
    public int Class { get; init; }

    /// <summary>Nhãn văn bản của Class để App hiển thị (VD: "Nguy hiểm cao").</summary>
    public string ClassLabel { get; init; } = string.Empty;

    public DateTime TimestampUtc { get; init; }
}
