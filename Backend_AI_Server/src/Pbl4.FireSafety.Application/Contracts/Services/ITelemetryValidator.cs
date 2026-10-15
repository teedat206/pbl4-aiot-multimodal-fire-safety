using Pbl4.FireSafety.Application.DTOs;

namespace Pbl4.FireSafety.Application.Contracts.Services;

/// <summary>
/// Hợp đồng kiểm định tính hợp lệ của gói tin Telemetry nhận từ Edge ESP32.
/// </summary>
public interface ITelemetryValidator
{
    /// <summary>
    /// Kiểm tra toàn diện các trường dữ liệu, chống số liệu rác, NaN, hoặc cảm biến hỏng.
    /// </summary>
    /// <param name="payload">Gói tin DTO đã parse từ JSON</param>
    /// <param name="errorMessage">Mô tả lỗi nếu không hợp lệ</param>
    /// <returns>True nếu hợp lệ, False nếu vi phạm Data Contract</returns>
    bool Validate(TelemetryPayloadDto? payload, out string errorMessage);
}
