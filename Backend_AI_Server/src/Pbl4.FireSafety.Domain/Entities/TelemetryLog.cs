using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Domain.Entities;

/// <summary>
/// Bản ghi dữ liệu cảm biến đa phương thức nhận từ Edge (ESP32).
/// Được lưu vào bảng TelemetryLogs theo thứ tự thời gian (Time-Series).
/// </summary>
public class TelemetryLog
{
    public long Id { get; set; }

    /// <summary>Device ID của ESP32-S3 gửi lên, phục vụ định danh đa thiết bị.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Chỉ số khói (smoke density), đơn vị tùy cảm biến MQ-2.</summary>
    public float Smoke { get; set; }

    /// <summary>Nồng độ khí gas sinh hoạt (LPG/CH4), đơn vị ppm, từ MQ-2.</summary>
    public float Gas { get; set; }

    /// <summary>Nồng độ khí độc CO (Carbon Monoxide), đơn vị ppm, từ MQ-7.</summary>
    public float Co { get; set; }

    /// <summary>Nhiệt độ môi trường (°C), từ cảm biến DHT20.</summary>
    public float Temperature { get; set; }

    /// <summary>Kết quả phân loại cuối cùng từ Fusion Decision Matrix.</summary>
    public FireRiskClass RiskClass { get; set; }

    /// <summary>Timestamp UTC khi bản ghi được tạo tại Backend.</summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
