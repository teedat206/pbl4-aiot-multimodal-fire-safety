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

    /// <summary>Timestamp UNIX (giây) từ thiết bị Edge (dùng để phát hiện gói tin trễ).</summary>
    public long? EdgeTimestamp { get; init; }
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
