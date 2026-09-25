namespace Pbl4.FireSafety.Application.Options;

/// <summary>
/// Cấu hình Firebase Admin SDK để gửi FCM Push Notification.
/// Bind từ section "Firebase" trong appsettings.json.
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// Đường dẫn đến file JSON Service Account của Firebase Admin SDK.
    /// Không được commit file này lên Git.
    /// </summary>
    public string ServiceAccountJsonPath { get; set; } = "firebase-adminsdk.json";

    /// <summary>Project ID của Firebase project.</summary>
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>
/// Cấu hình kết nối SignalR Hub.
/// Bind từ section "SignalR" trong appsettings.json.
/// </summary>
public sealed class SignalROptions
{
    public const string SectionName = "SignalR";

    /// <summary>Đường dẫn URL của Fire Safety Hub (VD: "/hubs/firesafety").</summary>
    public string HubPath { get; set; } = "/hubs/firesafety";

    /// <summary>Timeout (giây) trước khi server coi client đã ngắt kết nối.</summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>Interval (giây) gửi keepalive ping đến client.</summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;
}
