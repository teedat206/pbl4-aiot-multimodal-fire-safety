using Pbl4.FireSafety.Application.DTOs;

namespace Pbl4.FireSafety.Application.Contracts.Services;

/// <summary>
/// Hợp đồng dịch vụ phân tích thị giác máy tính sử dụng YOLOv8 ONNX Runtime.
/// Tiếp nhận raw byte stream của ảnh từ ESP32-S3 Camera, tiền xử lý và chạy suy luận.
/// </summary>
public interface IYoloService
{
    /// <summary>
    /// Thực hiện phát hiện ngọn lửa và khói từ mảng byte ảnh JPEG/PNG (đồng bộ).
    /// </summary>
    YoloDetectionResult Detect(byte[] imageBytes);

    /// <summary>
    /// Thực hiện phát hiện ngọn lửa và khói từ mảng byte ảnh JPEG/PNG (bất đồng bộ).
    /// </summary>
    Task<YoloDetectionResult> DetectAsync(byte[] imageBytes, CancellationToken ct = default);

    /// <summary>
    /// Trạng thái mô hình ONNX đã được nạp thành công vào bộ nhớ hay chưa.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Nạp lại file mô hình ONNX từ đĩa cứng theo yêu cầu (Manual Hot-Reload).
    /// </summary>
    Task<bool> ReloadModelAsync();
}
