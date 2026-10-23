using Microsoft.AspNetCore.Mvc;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Domain.Models;

namespace Pbl4.FireSafety.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly IMockTelemetryStore _mockStore;

    public TelemetryController(IMockTelemetryStore mockStore)
    {
        _mockStore = mockStore;
    }

    /// <summary>
    /// Lấy bản ghi dữ liệu cảm biến mới nhất thời gian thực vừa được sinh ra.
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(SystemTelemetryReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetLatest()
    {
        var latest = _mockStore.GetLatest();
        if (latest == null)
        {
            return NotFound(new { message = "Chưa có dữ liệu cảm biến nào được ghi nhận." });
        }

        return Ok(latest);
    }

    /// <summary>
    /// Lấy danh sách 60 bản ghi cảm biến gần nhất lưu trên RAM phục vụ vẽ biểu đồ (Sliding Window 2 phút).
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemTelemetryReport>), StatusCodes.Status200OK)]
    public IActionResult GetRecent([FromQuery] int count = 60)
    {
        var recent = _mockStore.GetRecent(Math.Clamp(count, 1, 60));
        return Ok(new
        {
            total = recent.Count,
            data = recent
        });
    }
}
