using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Domain.Entities;

/// <summary>
/// Trạng thái kết nối và heartbeat của từng thiết bị Edge (ESP32).
/// Được cập nhật mỗi khi nhận được gói tin MQTT mới.
/// </summary>
public class DeviceStatus
{
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>True nếu thiết bị đã gửi heartbeat trong vòng 60 giây gần nhất.</summary>
    public bool IsOnline { get; set; }

    /// <summary>Class nguy hiểm mới nhất được báo cáo từ thiết bị này.</summary>
    public FireRiskClass LastRiskClass { get; set; }

    /// <summary>Thời điểm UTC nhận được gói tin MQTT cuối cùng.</summary>
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Địa chỉ IP LAN của thiết bị Edge (nếu có).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Phiên bản firmware hiện tại trên thiết bị.</summary>
    public string FirmwareVersion { get; set; } = "unknown";
}
