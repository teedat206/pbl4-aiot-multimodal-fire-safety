using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.API.Hubs;

namespace Pbl4.FireSafety.API.Services;

/// <summary>
/// Implementation của IFireSafetyHubService — đặt trong API layer để có thể
/// inject trực tiếp IHubContext của FireSafetyHub mà không tạo circular dependency.
///
/// Được đăng ký trong Program.cs sau khi Hub class đã được định nghĩa.
/// </summary>
public sealed class FireSafetyHubService : IFireSafetyHubService
{
    private readonly IHubContext<FireSafetyHub> _hubContext;
    private readonly ILogger<FireSafetyHubService> _logger;

    public FireSafetyHubService(
        IHubContext<FireSafetyHub> hubContext,
        ILogger<FireSafetyHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast dữ liệu telemetry realtime đến TẤT CẢ client đang kết nối.
    /// Mobile App lắng nghe sự kiện "ReceiveTelemetry".
    /// </summary>
    public async Task BroadcastTelemetryAsync(TelemetryBroadcastDto payload, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", payload, ct);

            _logger.LogDebug(
                "📡 [SignalR] Broadcast Telemetry → [{DeviceId}] Class={Class} ({ClassLabel}) | Temp={Temp}°C",
                payload.DeviceId,
                payload.Class,
                payload.ClassLabel,
                payload.Temp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [SignalR] Lỗi khi broadcast Telemetry cho [{DeviceId}]", payload.DeviceId);
        }
    }

    /// <summary>
    /// Broadcast cảnh báo khẩn cấp đến TẤT CẢ client đang kết nối.
    /// Mobile App lắng nghe sự kiện "ReceiveAlert".
    /// </summary>
    public async Task BroadcastAlertAsync(AlertBroadcastDto alert, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, ct);

            _logger.LogWarning(
                "🚨 [SignalR] Broadcast CẢNH BÁO → [{DeviceId}] Class={TriggerClass} | Tin cậy: {Conf:P0} | {Message}",
                alert.DeviceId,
                alert.TriggerClass,
                alert.SensorConfidence,
                alert.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [SignalR] Lỗi khi broadcast Alert cho [{DeviceId}]", alert.DeviceId);
        }
    }
}
