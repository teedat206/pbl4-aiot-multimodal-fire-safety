using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.DTOs;

/// <summary>
/// Payload cảnh báo được Backend gửi lên cho Mobile App
/// qua SignalR (ReceiveAlert) và FCM (High Priority).
/// </summary>
public record AlertBroadcastDto
{
    public long AlertId { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public FireRiskClass TriggerClass { get; init; }
    public float SensorConfidence { get; init; }
    public float? VisionConfidence { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
}
