using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.Contracts.Services;

/// <summary>
/// Kết quả suy luận từ mô hình Random Forest đối với dữ liệu cảm biến đa biến.
/// </summary>
public record SensorInferenceResult
{
    /// <summary>Cấp độ nguy cơ dự đoán từ 0 (Safe) đến 4 (FireConfirmed).</summary>
    public FireRiskClass RiskClass { get; init; } = FireRiskClass.Safe;

    /// <summary>Độ tin cậy xác suất của class được chọn (từ 0.0 đến 1.0).</summary>
    public float Confidence { get; init; }

    /// <summary>Phân phối xác suất chi tiết của từng class [0..4].</summary>
    public float[] Probabilities { get; init; } = Array.Empty<float>();

    /// <summary>Tên hoặc phiên bản mô hình thực thi suy luận.</summary>
    public string ModelSource { get; init; } = string.Empty;
}

/// <summary>
/// Hợp đồng dịch vụ thực thi mô hình máy học Random Forest (ONNX) cho dung hợp dữ liệu cảm biến.
/// Hỗ trợ cơ chế Hot-Reloading thay thế nóng file ONNX khi đang chạy.
/// </summary>
public interface IRandomForestService
{
    /// <summary>
    /// Thực hiện suy luận dự đoán rủi ro hỏa hoạn từ vector 4 chỉ số cảm biến [smoke, gas, co, temp].
    /// </summary>
    SensorInferenceResult Predict(float smoke, float gas, float co, float temp);

    /// <summary>
    /// Kiểm tra xem file mô hình ONNX thật đã được nạp thành công hay đang chạy ở chế độ fallback.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Kích hoạt nạp lại mô hình ONNX từ ổ đĩa (Hot-Reload).
    /// </summary>
    Task<bool> ReloadModelAsync();
}
