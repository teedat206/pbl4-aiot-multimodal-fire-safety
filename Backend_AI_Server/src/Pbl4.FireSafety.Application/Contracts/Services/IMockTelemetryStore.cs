using Pbl4.FireSafety.Domain.Models;

namespace Pbl4.FireSafety.Application.Contracts.Services;

/// <summary>
/// Hợp đồng lưu trữ tạm dữ liệu Telemetry trên bộ nhớ RAM (In-Memory Ring Buffer).
/// Lưu trữ tối đa 60 bản ghi gần nhất phục vụ vẽ biểu đồ và kiểm thử khi chưa có Database.
/// </summary>
public interface IMockTelemetryStore
{
    /// <summary>
    /// Thêm một bản ghi mới vào bộ đệm RAM. Tự động đẩy phần tử cũ nhất nếu vượt quá dung lượng.
    /// </summary>
    void Add(SystemTelemetryReport report);

    /// <summary>
    /// Lấy danh sách các bản ghi lịch sử gần nhất theo thứ tự thời gian.
    /// </summary>
    IReadOnlyList<SystemTelemetryReport> GetRecent(int count = 60);

    /// <summary>
    /// Lấy bản ghi mới nhất vừa sinh ra.
    /// </summary>
    SystemTelemetryReport? GetLatest();

    /// <summary>
    /// Số lượng bản ghi hiện có trong RAM.
    /// </summary>
    int Count { get; }
}
