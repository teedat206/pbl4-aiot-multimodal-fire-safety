using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Pbl4.FireSafety.API.Hubs;

/// <summary>
/// SignalR Hub trung tâm cho hệ thống giám sát hỏa hoạn AIoT PBL4.
/// Mobile App kết nối tới endpoint /hubs/firesafety để nhận dữ liệu realtime.
///
/// Server → Client events:
///   - "ReceiveTelemetry" : TelemetryBroadcastDto — dữ liệu cảm biến + AI class mỗi chu kỳ
///   - "ReceiveAlert"     : AlertBroadcastDto     — cảnh báo khẩn cấp khi Class >= HighRisk
///
/// Client → Server calls (tùy chọn, dùng để client đăng ký nhóm hoặc gửi lệnh):
///   - Ping() → Server trả "Pong" để kiểm tra kết nối còn sống
/// </summary>
public class FireSafetyHub : Hub
{
    private readonly ILogger<FireSafetyHub> _logger;

    public FireSafetyHub(ILogger<FireSafetyHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gọi khi Mobile App kết nối mới vào Hub.
    /// Log ConnectionId để theo dõi số client đang online.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "📱 Client kết nối SignalR Hub | ConnectionId: {ConnectionId} | IP: {IP}",
            Context.ConnectionId,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Gọi khi Mobile App ngắt kết nối (đóng app, mất mạng, timeout).
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                "⚠️ Client ngắt kết nối bất thường | ConnectionId: {ConnectionId} | Lý do: {Error}",
                Context.ConnectionId,
                exception.Message);
        }
        else
        {
            _logger.LogInformation(
                "📴 Client ngắt kết nối bình thường | ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client gọi để kiểm tra kết nối Hub còn sống không.
    /// App Flutter gọi: await connection.invoke("Ping");
    /// </summary>
    public async Task Ping()
    {
        _logger.LogDebug("🏓 Ping nhận được từ ConnectionId: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}
