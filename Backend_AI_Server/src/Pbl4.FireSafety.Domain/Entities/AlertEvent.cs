using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Domain.Entities;

/// <summary>
/// Bản ghi sự kiện cảnh báo cháy nổ được Backend tạo ra
/// khi Fusion Decision Matrix xuất ra Class >= HighRisk.
/// </summary>
public class AlertEvent
{
    public long Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Class nguy hiểm tại thời điểm cảnh báo được kích hoạt.</summary>
    public FireRiskClass TriggerClass { get; set; }

    /// <summary>Độ tin cậy từ mô hình cảm biến Random Forest (0.0 - 1.0).</summary>
    public float SensorConfidence { get; set; }

    /// <summary>Độ tin cậy từ mô hình hình ảnh YOLOv8 (0.0 - 1.0). Null nếu không có frame.</summary>
    public float? VisionConfidence { get; set; }

    /// <summary>Trạng thái FCM Push Notification đã được gửi hay chưa.</summary>
    public bool FcmSent { get; set; } = false;

    /// <summary>Thông báo mô tả ngắn gọn của cảnh báo.</summary>
    public string Message { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
