namespace Pbl4.FireSafety.Application.Options;

/// <summary>
/// Cấu hình kết nối MQTT Broker.
/// Bind từ section "Mqtt" trong appsettings.json.
/// </summary>
public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>Địa chỉ IP hoặc hostname của MQTT Broker (VD: "localhost" hoặc "192.168.1.100").</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Port của MQTT Broker. Mặc định 1883 (non-TLS).</summary>
    public int Port { get; set; } = 1883;

    /// <summary>Client ID định danh Backend trên Broker.</summary>
    public string ClientId { get; set; } = "pbl4-backend-server";

    /// <summary>Topic lắng nghe dữ liệu cảm biến từ Edge.</summary>
    public string TelemetryTopic { get; set; } = "pbl4/telemetry";

    /// <summary>Topic nhận frame ảnh từ ESP32-CAM.</summary>
    public string CameraFrameTopic { get; set; } = "pbl4/camera/frame";

    /// <summary>Topic nhận kết quả thị giác YOLOv8 (JSON) từ AI Server.</summary>
    public string VisionTopic { get; set; } = "pbl4/vision";

    /// <summary>Username xác thực Broker (nếu có).</summary>
    public string? Username { get; set; }

    /// <summary>Password xác thực Broker (nếu có). Không commit vào Git.</summary>
    public string? Password { get; set; }

    /// <summary>Bật/tắt TLS khi kết nối Broker.</summary>
    public bool UseTls { get; set; } = false;

    /// <summary>Thời gian (giây) keepalive gửi PINGREQ. Mặc định 60s.</summary>
    public int KeepAliveSeconds { get; set; } = 60;
}
