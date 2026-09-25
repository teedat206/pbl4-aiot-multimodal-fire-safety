namespace Pbl4.FireSafety.Application.Options;

/// <summary>
/// Cấu hình đường dẫn và hành vi các mô hình AI (ONNX).
/// Bind từ section "AiModels" trong appsettings.json.
/// </summary>
public sealed class AiModelOptions
{
    public const string SectionName = "AiModels";

    /// <summary>Đường dẫn tương đối đến file ONNX của Random Forest (sensor fusion).</summary>
    public string RandomForestOnnxPath { get; set; } = "models/rf_model.onnx";

    /// <summary>Đường dẫn tương đối đến file ONNX của YOLOv8 (fire/smoke detection).</summary>
    public string YoloOnnxPath { get; set; } = "models/yolov8n.onnx";

    /// <summary>
    /// Ngưỡng confidence tối thiểu cho YOLOv8 để chấp nhận kết quả detection.
    /// </summary>
    public float YoloConfidenceThreshold { get; set; } = 0.45f;

    /// <summary>Ngưỡng IoU cho Non-Maximum Suppression của YOLOv8.</summary>
    public float YoloIouThreshold { get; set; } = 0.45f;

    /// <summary>
    /// Bật cơ chế Hot-Reload: tự động nạp lại model ONNX khi file thay đổi
    /// mà không cần khởi động lại Server (dùng FileSystemWatcher).
    /// </summary>
    public bool EnableHotReload { get; set; } = true;
}
