using Pbl4.FireSafety.Application.DTOs;

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
/// Hợp đồng cho Fusion Decision Matrix.
/// Nhận kết quả từ 2 mô hình AI và xuất ra Class cuối cùng.
/// </summary>
public interface IFusionDecisionService
{
    /// <summary>
    /// Tính toán FireRiskClass cuối cùng dựa trên độ tin cậy cảm biến và camera.
    /// </summary>
    /// <param name="sensorClass">Class từ Random Forest ONNX.</param>
    /// <param name="sensorConfidence">Xác suất dự đoán từ Random Forest.</param>
    /// <param name="visionConfidence">Confidence từ YOLOv8 (null nếu không có frame).</param>
    (int FinalClass, float FinalConfidence) Decide(
        int sensorClass,
        float sensorConfidence,
        float? visionConfidence);
}
