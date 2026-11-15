using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.Contracts.Services;

/// <summary>
/// Hợp đồng giao tiếp với SignalR Hub.
/// Infrastructure layer implement, Application layer dùng qua DI.
/// </summary>
public interface IFireSafetyHubService
{
    /// <summary>Broadcast dữ liệu telemetry realtime đến tất cả client đang kết nối.</summary>
    Task BroadcastTelemetryAsync(TelemetryBroadcastDto payload, CancellationToken ct = default);

    /// <summary>Broadcast sự kiện cảnh báo đến tất cả client đang kết nối.</summary>
    Task BroadcastAlertAsync(AlertBroadcastDto alert, CancellationToken ct = default);
}

/// <summary>
/// Hợp đồng gửi FCM Push Notification qua Firebase Admin SDK.
/// </summary>
public interface IFcmNotificationService
{
    /// <summary>
    /// Gửi High Priority Data Message đến tất cả thiết bị đã đăng ký FCM token.
    /// </summary>
    Task SendFireAlertAsync(AlertBroadcastDto alert, CancellationToken ct = default);
}

/// <summary>
/// Kết quả tổng hợp từ Ma trận quyết định đa phương thức (Fusion Decision Matrix).
/// </summary>
public record FusionResult
{
    /// <summary>Cấp độ nguy hiểm cuối cùng (0 = Safe -> 4 = FireConfirmed).</summary>
    public FireRiskClass FinalClass { get; init; } = FireRiskClass.Safe;

    /// <summary>Độ tin cậy tổng hợp (0.0 - 1.0).</summary>
    public float FinalConfidence { get; init; }

    /// <summary>Nhãn văn bản mô tả (VD: "SAFE", "HIGH RISK", "FIRE CONFIRMED").</summary>
    public string ClassLabel { get; init; } = "SAFE";

    /// <summary>Giải thích nguyên nhân và bằng chứng đa phương thức dẫn đến quyết định.</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>Xác định xem có cần kích hoạt cảnh báo khẩn cấp không (Class >= HighRisk).</summary>
    public bool IsAlertTriggered => (int)FinalClass >= (int)FireRiskClass.HighRisk;
}

/// <summary>
/// Hợp đồng cho Fusion Decision Matrix.
/// Nhận kết quả từ 2 mô hình AI (Random Forest cảm biến & YOLOv8 thị giác) và xuất ra Class cuối cùng.
/// </summary>
public interface IFusionDecisionService
{
    /// <summary>
    /// Tính toán FireRiskClass cuối cùng dựa trên độ tin cậy cảm biến và camera.
    /// </summary>
    (int FinalClass, float FinalConfidence) Decide(
        int sensorClass,
        float sensorConfidence,
        float? visionConfidence);

    /// <summary>
    /// Tính toán đầy đủ kết quả Fusion đa phương thức bao gồm phân loại lửa, khói và giải trình chi tiết.
    /// </summary>
    FusionResult Evaluate(
        FireRiskClass sensorClass,
        float sensorConfidence,
        float? visionFireConfidence,
        float? visionSmokeConfidence);
}
