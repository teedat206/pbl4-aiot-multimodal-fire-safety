namespace Pbl4.FireSafety.Application.DTOs;

/// <summary>
/// Biểu diễn một khung chữ nhật (Bounding Box) phát hiện được từ mô hình YOLOv8.
/// </summary>
public record YoloBoundingBox(
    int ClassId,
    string Label,
    float Confidence,
    float X,
    float Y,
    float Width,
    float Height
);

/// <summary>
/// Kết quả tổng hợp sau khi xử lý một frame ảnh qua YOLOv8 + Non-Maximum Suppression (NMS).
/// </summary>
public record YoloDetectionResult
{
    /// <summary>Có phát hiện ngọn lửa với độ tin cậy vượt ngưỡng không.</summary>
    public bool HasFire { get; init; }

    /// <summary>Có phát hiện khói với độ tin cậy vượt ngưỡng không.</summary>
    public bool HasSmoke { get; init; }

    /// <summary>Độ tin cậy cao nhất của ngọn lửa (0.0 - 1.0).</summary>
    public float MaxFireConfidence { get; init; }

    /// <summary>Độ tin cậy cao nhất của khói (0.0 - 1.0).</summary>
    public float MaxSmokeConfidence { get; init; }

    /// <summary>Danh sách tất cả các bounding box sau khi đã lọc qua NMS.</summary>
    public IReadOnlyList<YoloBoundingBox> Detections { get; init; } = Array.Empty<YoloBoundingBox>();

    /// <summary>Nguồn của mô hình (Onnx-YOLOv8 hoặc Fallback-Heuristic).</summary>
    public string ModelSource { get; init; } = "Onnx-YOLOv8";

    /// <summary>Thời gian suy luận (milliseconds).</summary>
    public long InferenceTimeMs { get; init; }
}
