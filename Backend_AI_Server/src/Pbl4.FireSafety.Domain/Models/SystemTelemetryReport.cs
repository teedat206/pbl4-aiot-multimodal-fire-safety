using System.Text.Json.Serialization;

namespace Pbl4.FireSafety.Domain.Models;

/// <summary>
/// Khuôn dữ liệu chuẩn (Data Contract) thống nhất giữa Backend, Edge Firmware (ESP32-S3), và Mobile App.
/// </summary>
public record SystemTelemetryReport
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ONLINE";

    [JsonPropertyName("sensitivity_mode")]
    public string SensitivityMode { get; init; } = "STANDARD";

    [JsonPropertyName("ai_result")]
    public AiInferenceResult AiResult { get; init; } = new();

    [JsonPropertyName("sensors")]
    public SensorMetrics Sensors { get; init; } = new();
}

/// <summary>
/// Kết quả suy luận đa phương thức từ AI (Random Forest cảm biến & YOLOv8 thị giác).
/// </summary>
public record AiInferenceResult
{
    [JsonPropertyName("predicted_class")]
    public int PredictedClass { get; init; } = 0;

    [JsonPropertyName("class_label")]
    public string ClassLabel { get; init; } = "SAFE";

    [JsonPropertyName("yolo_fire_conf")]
    public double YoloFireConfidence { get; init; } = 0.0;

    [JsonPropertyName("yolo_smoke_conf")]
    public double YoloSmokeConfidence { get; init; } = 0.0;
}

/// <summary>
/// Các chỉ số đo đạc từ cụm cảm biến môi trường (DHT20, MQ-2, MQ-135, MQ-7).
/// </summary>
public record SensorMetrics
{
    [JsonPropertyName("temp")]
    public double Temp { get; init; } = 28.5;

    [JsonPropertyName("humidity")]
    public double Humidity { get; init; } = 68.0;

    [JsonPropertyName("gas_mq2")]
    public double GasMq2 { get; init; } = 120.5;

    [JsonPropertyName("smoke_mq135")]
    public double SmokeMq135 { get; init; } = 45.0;

    [JsonPropertyName("co_mq7")]
    public double CoMq7 { get; init; } = 12.4;
}
